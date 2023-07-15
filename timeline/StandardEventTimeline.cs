using System.Collections.Generic;

namespace Timeline
{
    public class StandardTimelineTimer : AbstractTimelineTimer
    {
        private long _startingTime;
        private long _totalPausedAmount;
        protected long RawDeltaTime => Now - _startingTime;
        protected override void OnPause()
        {
            
        }

        protected override void OnResume()
        {
            _totalPausedAmount += ResumedTime - PausedTime;
        }

        protected override long GetEpoch() => _startingTime;
        protected override long GetTotalPauseTime() => _totalPausedAmount;
        protected override long GetTimeSinceEpoch() => RawDeltaTime;
        public override long GetTimeProcessed() => RawDeltaTime - GetTotalPauseTime();
        public override void StartTimer() => _startingTime = Now;
        public override void Reconstruct(ExtractedTimelineTimer extracted)
        {
            base.Reconstruct(extracted);
            var adjustedStartingTime = Now - extracted.estimatedTimeSinceEpoch;
            _totalPausedAmount = extracted.totalPauseTime;
            _startingTime = adjustedStartingTime;
        }
    }

    public class StandardEventTimeline : AbstractEventTimeline
    {
        private readonly AbstractTimelineTimer _timer = new StandardTimelineTimer();
        private readonly LinkedList<ExtractedEventLog> _logs = new();
        private readonly HashSet<uint> _owningBranches = new();
        private readonly IdAllocator<ulong> _idAllocator = new U64IdAllocator();
        private bool _started;
        protected override void MatchAllocationId(ulong newId)
        {
            _idAllocator.Set(newId);
        }

        public override AbstractEventTimeline PrepareReplication() => new StandardEventTimeline(GetInternalId());
        public override ulong GetEarliestAllocationId() => 0;

        public override ulong GetLatestAllocationId() => _idAllocator.CurrentId();

        protected override AbstractTimelineTimer GetTimer() => _timer;

        protected override void StartTimelineInternal()
        {
            _started = true;
            GetTimer().StartTimer();
        }

        public override bool IsTimelineStarted() => _started;

        public override bool OwnBranch(uint branchId) => _owningBranches.Contains(branchId);
        public override void AddEvent(ExtractedDataclass data)
        {
            _logs.AddLast(new ExtractedEventLog
            {
                internalId = _idAllocator,
                timestamp = GetTimer().GetTimeProcessed(),
                data = data
            });
            if (data is TimelineSplitEvent splitEvent)
            {
                _owningBranches.Add(splitEvent.splitInto);
            }
        }

        public override void RemoveEvent(uint eventId)
        {
            for (var it = _logs.Last; it != null; it = it.Previous)
            {
                if (it.Value.GetInternalId() == eventId)
                {
                    if (it.Value.data is TimelineSplitEvent splitEvent)
                    {
                        _owningBranches.Remove(splitEvent.splitInto);
                    }
                    _logs.Remove(it);
                    break;
                }
            }
        }

        private void AddEvent(ExtractedEventLog log, LinkedListNode<ExtractedEventLog> after)
        {
            if (log.data is TimelineSplitEvent splitEvent)
            {
                _owningBranches.Add(splitEvent.splitInto);
            }
            if (after == null) _logs.AddLast(log);
            else _logs.AddAfter(after, log);
        }

        // Does not require anymore epoch correction
        protected virtual void MergeEvents(ExtractedEventTimeline branchingTimeline)
        {
            PauseTimeline();
            foreach (var log in branchingTimeline.logs)
            {
                AddEvent(log, _logs.Last);
            }
            ResumeTimeline();
        }
        private bool MergeTimelineFromTip(AbstractEventTimeline branchTimeline)
        {
            _logs.RemoveLast();
            var targetData = branchTimeline.Extract();
            MergeEvents(targetData);
            return true;
        }

        private bool MergeTimelineFromBase(AbstractEventTimeline branchTimeline)
        {
            LinkedListNode<ExtractedEventLog> currNode = null;
            for (var it = _logs.First; it != null; it = it.Next)
            {
                if (!IsBranchingOut(it.Value, branchTimeline)) continue;
                currNode = it.Next;
                _logs.Remove(it);
                break;
            }

            // Does not own branch
            if (currNode == null) return false;
            foreach (var log in branchTimeline)
            {
                // Add the log after a linked list node
                // If the node is null, append the log
                if (currNode == null) AddEvent(log, null);
                else if (log.timestamp < currNode.Value.timestamp) AddEvent(log, currNode.Previous);
                else currNode = currNode.Next;
            }
            return true;
        }

        public override bool MergeTimeline(AbstractEventTimeline branchTimeline)
        {
            return !IsTimelineMergeableFromTip(branchTimeline) ? MergeTimelineFromBase(branchTimeline) : MergeTimelineFromTip(branchTimeline);
        }

        private bool IsBranchingOut(ExtractedEventLog log, AbstractEventTimeline branchTimeline)
        {
            if (log == null) return false;
            if (log.data is not TimelineSplitEvent splitEvent) return false;
            return splitEvent.splitInto == branchTimeline.GetInternalId();
        }
        // Boring checks
        public override bool IsTimelineMergeableFromTip(AbstractEventTimeline branchTimeline)
        {
            var lastEvent = GetLastEvent();
            return lastEvent != null && IsBranchingOut(lastEvent, branchTimeline);
        }

        public override ExtractedEventTimeline Extract()
        {
            var re = new ExtractedEventTimeline
            {
                internalId = GetInternalId(),
                logs = new(_logs.Count),
                owningBranches = new (_owningBranches.Count),
                timer = GetTimer().Extract(),
            };
            var i = 0;
            foreach (var log in _logs)
            {
                re.logs[i++] = log;
            }
            
            i = 0;
            foreach (var branch in _owningBranches)
            {
                re.owningBranches[i++] = branch;
            }
            return re;
        }

        public override ExtractedEventLog GetLastEvent() => _logs.Last.Value;

        public override void Reconstruct(ExtractedEventTimeline extracted)
        {
            ClearAllEvents();
            GetTimer().Reconstruct(extracted.timer);
            PauseTimeline();
            foreach (var reducedLog in extracted.logs)
            {
                _logs.AddLast(reducedLog);
            }

            foreach (var branch in extracted.owningBranches)
            {
                _owningBranches.Add(branch);
            }
            _started = true;
        }

        public override void ClearAllEvents()
        {
            _logs.Clear();
            _owningBranches.Clear();
        }
        public override IEnumerator<ExtractedEventLog> GetEnumerator() => _logs.GetEnumerator();
        public StandardEventTimeline(uint internalId) : base(internalId)
        {
        }
    }
}