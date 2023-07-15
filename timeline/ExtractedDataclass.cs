using System;
using System.Collections.Generic;
using System.Linq;

namespace Timeline
{
    [Serializable]
    public class ExtractedDataclass : ISelfReplicateable<ExtractedDataclass>
    {
        protected T ShallowFieldsReplicate<T>()
        {
            var instance = (T)Activator.CreateInstance(GetType());
            var fields = GetType().GetFields();
            foreach (var field in fields)
            {
                var myValue = field.GetValue(this);
                if (myValue is ExtractedDataclass asDataclass) myValue = asDataclass.Replicate();
                field.SetValue(instance, myValue);
            }

            return instance;
        }

        public virtual ExtractedDataclass Replicate() => ShallowFieldsReplicate<ExtractedDataclass>();
    }
    [Serializable]
    public class ExtractedEventLog : ExtractedDataclass
    {
        public ulong internalId;
        public long timestamp;
        public ExtractedDataclass data;
        public ulong GetInternalId() => internalId;
        public ulong ModifyInternalId(ulong newId) => internalId = newId;
    }

    [Serializable]
    public class ExtractedTimelineTimer : ExtractedDataclass
    {
        public bool isPaused;
        public long lastPauseTime;
        public long lastResumeTime;
        public long totalPauseTime;
        public long estimatedTimeSinceEpoch;
    }
    [Serializable]
    public class ExtractedBranchedTimelineTimer : ExtractedTimelineTimer
    {
        public long splitTimeSinceEpoch;
    }

    [Serializable]
    public class ExtractedEventTimeline : ExtractedDataclass
    {
        public uint internalId;
        public int branchCount;
        public List<ExtractedEventLog> logs;
        public List<uint> owningBranches;
        public ExtractedTimelineTimer timer;

        public override ExtractedDataclass Replicate()
        {
            var instance = ShallowFieldsReplicate<ExtractedEventTimeline>();
            instance.logs = instance.logs.Select(v => (ExtractedEventLog)v.Replicate()).ToList();
            instance.owningBranches = instance.owningBranches.ToList();
            return instance;
        }
        
        public uint GetInternalId() => internalId;

        public void Append(ExtractedEventTimeline other)
        {
            logs.AddRange(other.logs);
            // Branches that are being appended don't own sub-branches
            // owningBranches.AddRange(other.owningBranches);
        }
    }
    [Serializable]
    public class ExtractedMultiTimelines : ExtractedDataclass
    {
        public uint activeTimelineId;
        public uint originalTimelineId;
        public List<ExtractedEventTimeline> allTimelines;
        
        public override ExtractedDataclass Replicate()
        {
            var instance = ShallowFieldsReplicate<ExtractedMultiTimelines>();
            instance.allTimelines = instance.allTimelines.Select(v => (ExtractedEventTimeline)v.Replicate()).ToList();
            return instance;
        }
    }

    [Serializable]
    public class TimelineActivationEvent : ExtractedDataclass
    {
        
    }
    [Serializable]
    public class TimelineSplitEvent : ExtractedDataclass
    {
        public uint splitInto;
    }
}