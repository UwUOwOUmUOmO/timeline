using Timeline;

namespace TimelineTests;

[TestFixture]
public class TimelineTests
{
    private readonly StandardEventTimeline _timeline = new(1U);

    public TimelineTests()
    {
        // 3 events, excluding genesis
        _timeline.StartTimeline();
        _timeline.AddRandomRubbish();
        Thread.Sleep(Crypto.GenerateSecureBytes(1)[0] / 2);
        _timeline.AddRandomRubbish();
        Thread.Sleep(Crypto.GenerateSecureBytes(1)[0] / 2);
        _timeline.AddRandomRubbish();
        _timeline.PauseTimeline();;
    }

    [Test]
    public void GeneralReplicationTest()
    {
        // Does not include timer comparision
        var replicated = _timeline.Replicate();
        Assert.That(_timeline.CompareTo(replicated), Is.True);
    }

    [Test]
    public void SerializationComparisionTest()
    {
        // Does include timer comparision
        var replicated = _timeline.Replicate();
        var oldExtraction = _timeline.Extract();
        var newExtraction = replicated.Extract();
        Assert.That(oldExtraction.DeepFieldsCompareTo(newExtraction), Is.True);
    }

    [Test]
    public void PeekTest()
    {
        var extraction = _timeline.Extract();
        Assert.Multiple(() =>
        {
            Assert.That(extraction.logs[2].DeepFieldsCompareTo(_timeline[2]), Is.True);
            Assert.That(extraction.logs[^2].DeepFieldsCompareTo(_timeline[-2]), Is.True);
        });
    }

    public void TimerStateTest()
    {
        var timeline = new StandardEventTimeline(3);
        _timeline.StateChangeTest();
    }
    
    [Test]
    public void ResumptionTest()
    {
        // -4
        const int replicationTolerance = 100;
        const int baseTolerance = 50;
        const int pauseTolerance = 140;
        const int pause = 100;
        
        Thread.Sleep(pause * 3);
        var replicated = _timeline.Replicate();
        replicated.ResumeTimeline();
        replicated.AddRandomRubbish(); // -3
        Thread.Sleep(pause);
        replicated.AddRandomRubbish(); // -2
        replicated.PauseTimeline();
        Thread.Sleep(pause);
        replicated.ResumeTimeline();
        replicated.AddRandomRubbish(); // -1
        Assert.Multiple(() =>
        {
            // -1 and -2: paused so must be bellow tolerance
            Assert.That(replicated[-1].timestamp - replicated[-2].timestamp, Is.LessThan(baseTolerance));
            // -2 to -3: unpaused so must be bellow pause tolerane
            Assert.That(replicated[-2].timestamp - replicated[-3].timestamp, Is.LessThan(pauseTolerance));
            // -3 to -4: paused and replicated so must be bellow replication tolerance
            Assert.That(replicated[-3].timestamp - replicated[-4].timestamp, Is.LessThan(replicationTolerance));
        });
    }
}