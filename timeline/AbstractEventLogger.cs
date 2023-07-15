using System;
using System.Collections;
using System.Collections.Generic;

namespace Timeline
{
    public class TimelineNotReadyException : Exception{}
    public abstract class AbstractTimelineTimer :
        ISelfExtractable<ExtractedTimelineTimer>,
        ISelfReconstructable<ExtractedTimelineTimer>
    {
        private bool _isPaused;
        private long _pausedTime;
        private long _resumedTime;
        protected long Now => DateTimeOffset.Now.ToUnixTimeMilliseconds();
        protected long PausedTime => _pausedTime;
        protected long ResumedTime => _resumedTime;

        protected abstract void OnPause();

        protected abstract void OnResume();
        public void Pause()
        {
            if (_isPaused) return;
            _isPaused = !_isPaused;
            _pausedTime = GetTimeSinceEpoch();
            OnPause();
        }
        public void Unpause()
        {
            if (!_isPaused) return;
            _isPaused = !_isPaused;
            _resumedTime = GetTimeSinceEpoch();
            OnResume();
        }

        public bool IsPaused => _isPaused;

        protected abstract long GetEpoch();
        protected abstract long GetTotalPauseTime();
        protected abstract long GetTimeSinceEpoch();
        public abstract long GetTimeProcessed();
        public abstract void StartTimer();
        public virtual ExtractedTimelineTimer Extract()
        {
            return new()
            {
                isPaused = IsPaused,
                lastPauseTime = PausedTime,
                lastResumeTime = ResumedTime,
                totalPauseTime = GetTotalPauseTime(),
                estimatedTimeSinceEpoch = GetTimeSinceEpoch()
            };
        }

        public virtual void Reconstruct(ExtractedTimelineTimer extracted)
        {
            _isPaused = extracted.isPaused;
            _pausedTime = extracted.lastPauseTime;
            _resumedTime = extracted.lastResumeTime;
        }
    }

    public abstract class AbstractEventTimeline : 
        ILoggable,
        ISelfExtractable<ExtractedEventTimeline>,
        ISelfReconstructable<ExtractedEventTimeline>,
        IEnumerable<ExtractedEventLog>,
        ISelfReplicateable<AbstractEventTimeline>
    {
        private uint _internalId;
        protected void SetInternalId(uint newId) => _internalId = newId;
        protected AbstractEventTimeline(uint internalId)
        {
            _internalId = internalId;
        }

        protected abstract void MatchAllocationId(ulong newId);
        public abstract ulong GetEarliestAllocationId();
        public abstract ulong GetLatestAllocationId();
        
        protected abstract AbstractTimelineTimer GetTimer();
        public long GetTime() => GetTimer().GetTimeProcessed();
        public ExtractedEventLog this[int idx] => Peek(idx);
        public int Count => GetLogsCount();
        public bool StartTimeline()
        {
            if (IsTimelineStarted()) return false;
            GetTimer().StartTimer();;
            AddEvent(new TimelineActivationEvent());
            StartTimelineInternal();
            return true;
        }
        
        protected abstract void StartTimelineInternal();
        public abstract bool OwnBranch(uint branchId);
        public abstract bool IsTimelineStarted();
        public abstract ExtractedEventTimeline Extract();
        public abstract void Reconstruct(ExtractedEventTimeline extracted);
        public abstract void ClearAllEvents();
        public abstract IEnumerator<ExtractedEventLog> GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public abstract ExtractedEventLog GetLastEvent();
        public abstract ExtractedEventLog Peek(int idx);
        public abstract void AddEvent(ExtractedDataclass data);
        public abstract void RemoveEvent(uint eventId);
        public abstract bool MergeTimeline(AbstractEventTimeline branchTimeline);
        public abstract bool IsTimelineMergeableFromTip(AbstractEventTimeline branchTimeline);
        protected abstract int GetLogsCount();

        public virtual bool PauseTimeline()
        {
            GetTimer().Pause();
            return true;
        }

        public virtual bool ResumeTimeline()
        {
            GetTimer().Unpause();
            return true;
        }
        public virtual bool IsTimelinePaused() => GetTimer().IsPaused;
        public uint GetInternalId() => _internalId;
        public abstract AbstractEventTimeline PrepareReplication();
        public AbstractEventTimeline Replicate()
        {
            var instance = PrepareReplication();
            var extracted = Extract();
            instance.Reconstruct(extracted);
            return instance;
        }
    }
    public abstract class AbstractEventLogger : 
        ILoggable,
        ISelfExtractable<ExtractedEventTimeline>, 
        IExtractable<AbstractEventTimeline, ExtractedEventTimeline>
    {
        public ExtractedEventLog this[int idx] => Peek(idx);
        protected abstract AbstractEventTimeline GetActiveTimeline();
        public abstract bool Initialize();
        public abstract bool StartTimeline();
        public abstract bool ResumeTimeline();
        public abstract bool PauseTimeline();
        public bool IsTimelineStarted() => GetActiveTimeline().IsTimelineStarted();
        public abstract ExtractedEventLog Peek(int idx);
        public virtual bool IsTimelinePaused() => GetActiveTimeline().IsTimelinePaused();
        public ExtractedEventTimeline Extract() => Extract(GetActiveTimeline());
        public virtual ExtractedEventTimeline Extract(AbstractEventTimeline original) => original.Extract();

        public virtual void AddEvent(ExtractedDataclass data)
        {
            var curr = GetActiveTimeline();
            if (!curr.IsTimelineStarted() || curr.IsTimelinePaused()) throw new TimelineNotReadyException();
            curr.AddEvent(data);
        }
    }
}