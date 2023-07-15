using Timeline;

namespace TimelineTests;

[TestFixture]
public class LoggerTests
{
    private readonly MultiTimelineEventLogger _logger = new();

    public LoggerTests()
    {
        /*
         *  F                     #-------->
         *  E               #-----
         *  A ---------------
         *  B   \--<--<
         *  C     \--<--<
         *  D       \--<
         */
        _logger.Initialize();
        // A
        _logger.StartTimeline();
        _logger.AddRandomRubbish();
        _logger.AddRandomRubbish();
        _logger.SplitTimeline();
        // B
        _logger.StartTimeline();
        _logger.AddRandomRubbish();
        _logger.AddRandomRubbish();
        _logger.SplitTimeline();
        // C
        _logger.StartTimeline();
        _logger.AddRandomRubbish();
        _logger.AddRandomRubbish();
        _logger.SplitTimeline();
        // D
        _logger.StartTimeline();
        _logger.AddRandomRubbish();
        _logger.AddRandomRubbish();
        _logger.SplitTimeline();
        // Backtrack to C
        _logger.BacktrackTimeline();
        _logger.AddRandomRubbish();
        // Backtrack to B
        _logger.BacktrackTimeline();
        _logger.AddRandomRubbish();
        // Backtrack to A
        _logger.BacktrackTimeline();
        _logger.AddRandomRubbish();
        _logger.SplitTimeline();
        // E
        _logger.StartTimeline();
        _logger.AddRandomRubbish();
        _logger.AddRandomRubbish();
        _logger.SplitTimeline();
        // F
        _logger.StartTimeline();
        _logger.AddRandomRubbish();
        _logger.AddRandomRubbish();
        _logger.PauseTimeline();
    }

    [Test]
    public void PartialReplicationTest()
    {
        var replication = _logger.Replicate();
        Assert.That(_logger.CompareTo(replication), Is.True);
    }
    
    [Test]
    public void MainTimelineTimerConsistencyTest()
    {
        try
        {
            int iter = -1;
            long lastTime = long.MaxValue;
            for (;; iter--)
            {
                var lastLog = _logger[iter];
                Assert.That(lastLog.timestamp, Is.LessThanOrEqualTo(lastTime));
                lastTime = lastLog.timestamp;
            } 
        }
        catch (IndexOutOfRangeException)
        {
            
        }
    }

    [Test]
    public void MainTimelineIdIntegrityCheck()
    {
        var ids = new HashSet<ulong>();
        var extracted = _logger.ExtractMainTimeline();
        foreach (var log in extracted.logs)
        {
            Assert.That(ids, Does.Not.Contain(log.GetInternalId()));
            ids.Add(log.GetInternalId());
        }
    }
}