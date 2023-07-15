using System.Collections;
using System.Runtime.CompilerServices;
using Timeline;

namespace TimelineTests;

internal class NonPrimitiveException : Exception{}

internal static class ObjectFieldsComparator
{
    private static bool NullEqualityCheck(object? left, object? right)
    {
        if (left == null && right != null) return false;
        if (left != null && right == null) return false;
        // If both are null or both are not null
        return true;
    }

    public static bool MillisecondsTimeComparision(long left, long right, uint tolerance)
        => Math.Abs(left - right) < tolerance;
    
    public static bool ExtractedTimerCompare(this ExtractedTimelineTimer self, ExtractedTimelineTimer other, uint tolerance = 5U)
    {
        if (self.isPaused != other.isPaused) return false;
        if (!MillisecondsTimeComparision(self.lastPauseTime, other.lastPauseTime, tolerance)) return false;
        if (!MillisecondsTimeComparision(self.lastResumeTime, other.lastResumeTime, tolerance)) return false;
        if (!MillisecondsTimeComparision(self.totalPauseTime, other.totalPauseTime, tolerance)) return false;
        if (!MillisecondsTimeComparision(self.estimatedTimeSinceEpoch, other.estimatedTimeSinceEpoch, tolerance)) return false;
        return true;
    }
    public static bool DeepFieldsCompare(object? left, object? right)
    {
        const double tolerance = 1E-6;
        if (!NullEqualityCheck(left, right)) return false;
        if (left == null && right == null) return true;
        if (!left!.GetType().IsAssignableTo(right!.GetType())) return false;
        var stack = new Stack<(object? innerLeft, object? innerRight)>();

        bool PrimitiveCompare(object innerLeft, object innerRight)
        {
            if (innerLeft is bool) return (bool)innerLeft == (bool)innerRight;
            if (innerLeft is byte) return (byte)innerLeft == (byte)innerRight;
            if (innerLeft is sbyte) return (sbyte)innerLeft == (sbyte)innerRight;
            if (innerLeft is char) return (char)innerLeft == (char)innerRight;
            if (innerLeft is decimal) return (decimal)innerLeft == (decimal)innerRight;
            if (innerLeft is double) return Math.Abs((double)innerLeft - (double)innerRight) < tolerance;
            if (innerLeft is float) return Math.Abs((float)innerLeft - (float)innerRight) < tolerance;
            if (innerLeft is int) return (int)innerLeft == (int)innerRight;
            if (innerLeft is uint) return (uint)innerLeft == (uint)innerRight;
            if (innerLeft is long) return (long)innerLeft == (long)innerRight;
            if (innerLeft is ulong) return (ulong)innerLeft == (ulong)innerRight;
            if (innerLeft is short) return (short)innerLeft == (short)innerRight;
            if (innerLeft is ushort) return (ushort)innerLeft == (ushort)innerRight;
            if (innerLeft is IEnumerable)
            {
                var lIter = ((IEnumerable)innerLeft).GetEnumerator();
                var rIter = ((IEnumerable)innerRight).GetEnumerator();
                while (lIter.MoveNext() && rIter.MoveNext())
                {
                    stack!.Push((lIter.Current, rIter.Current));
                }

                return !lIter.MoveNext() && !rIter.MoveNext();
            }

            throw new NonPrimitiveException();
        }
        
        stack.Push((left, right));
        while (stack.Count > 0)
        {
            var (innerLeft, innerRight) = stack.Pop();
            if (!NullEqualityCheck(innerLeft, innerRight)) return false;
            if (innerLeft == null && innerRight == null) continue;
            if (!innerLeft!.GetType().IsAssignableTo(innerRight!.GetType())) return false;
            // Check if timer, for special comparision
            if (innerLeft is ExtractedTimelineTimer leftTimer)
            {
                if (!leftTimer.ExtractedTimerCompare((ExtractedTimelineTimer)innerRight)) return false;
                else continue;
            }
            // Pre-check, if fail miserably then just carry on
            try
            {
                if (!PrimitiveCompare(innerLeft, innerRight!)) return false;
                continue;
            }
            catch (NonPrimitiveException)
            {
                // Ignored
            }

            var fields = innerRight!.GetType().GetFields();
            foreach (var field in fields)
            {
                var (lField, rField) = (field.GetValue(innerLeft), field.GetValue(innerRight));
                if (!NullEqualityCheck(lField, rField)) return false;
                if (lField == null && rField == null) continue;
                if (!lField!.GetType().IsAssignableTo(rField!.GetType())) return false;
                try
                {
                    if (!PrimitiveCompare(lField, rField)) return false;
                }
                catch (NonPrimitiveException)
                {
                    stack.Push((lField, rField));
                }
            }
        }
        
        return true;
    }

    public static bool DeepFieldsCompareTo(this object left, object? right) => DeepFieldsCompare(left, right);
}

internal static class TimelineHelper
{
    public static bool CompareTo(this AbstractEventTimeline self, AbstractEventTimeline? other)
    {
        if (other == null) return false;
        if (self.GetInternalId() != other.GetInternalId()) return false;
        using var lIter = self.GetEnumerator();
        using var rIter = other.GetEnumerator();
        while (lIter.MoveNext() && rIter.MoveNext())
        {
            if (!lIter.Current!.DeepFieldsCompareTo(rIter.Current)) return false;
        }

        return !lIter.MoveNext() && !rIter.MoveNext();
    }
}

internal static class LoggerHelper
{
    public static bool CompareExtraction(this ExtractedMultiTimelines lExtraction, ExtractedMultiTimelines rExtraction)
    {
        if (lExtraction.activeTimelineId != rExtraction.activeTimelineId) return false;
        if (lExtraction.originalTimelineId != rExtraction.originalTimelineId) return false;
        if (lExtraction.allTimelines.Count != rExtraction.allTimelines.Count) return false;
        var lTimelines = new Dictionary<uint, ExtractedEventTimeline>();
        var rTimelines = new Dictionary<uint, ExtractedEventTimeline>();

        foreach (var t in lExtraction.allTimelines)
        {
            lTimelines[t.GetInternalId()] = t;
        }
        foreach (var t in rExtraction.allTimelines)
        {
            rTimelines[t.GetInternalId()] = t;
        }

        foreach (var (id, leftTimeline) in lTimelines)
        {
            if (!rTimelines.TryGetValue(id, out var rightTimeline)) return false;
            if (!leftTimeline.DeepFieldsCompareTo(rightTimeline)) return false;
        }
        
        return true;
    }
    public static bool CompareTo(this MultiTimelineEventLogger self, MultiTimelineEventLogger? other)
    {
        if (other == null) return false;
        var lExtraction = self.ExtractAllTimelines();
        var rExtraction = other.ExtractAllTimelines();
        return lExtraction.CompareExtraction(rExtraction);
    }
}

internal class DumpData : ExtractedDataclass  
{  
    public int Test;  
    public string Wow = "";  
}

internal static class LoggableTools
{
    public static void AddRandomRubbish(this ILoggable self)
    {
        self.AddEvent(new DumpData
        {
            Test = Crypto.GenerateSecureBytes(1)[0],
            Wow = Crypto.GenerateSecureString(32)
        });
        Thread.Sleep(5);
    }

    public static void StateChangeTest(this ILoggable self)
    {
        Assert.That(self.IsTimelineStarted(), Is.False);
        self.StartTimeline();
        Assert.That(self.IsTimelineStarted(), Is.True);
        Assert.That(self.IsTimelinePaused(), Is.False);
        self.PauseTimeline();
        Assert.That(self.IsTimelinePaused(), Is.True);
        self.ResumeTimeline();
        Assert.That(self.IsTimelinePaused(), Is.False);
    }
}
