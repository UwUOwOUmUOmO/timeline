using System;
using System.Collections.Generic;
using System.Linq;

namespace Timeline
{
    public class BranchedTimelineTimer : StandardTimelineTimer
    {
        private long _splitTimeSinceEpoch;
        public void SetSplitEpoch(long time) => _splitTimeSinceEpoch = time;
        public override long GetTimeProcessed() => base.GetTimeProcessed() + _splitTimeSinceEpoch;
        public override ExtractedTimelineTimer Extract()
        {
            return new ExtractedBranchedTimelineTimer
            {
                isPaused = IsPaused,
                lastPauseTime = PausedTime,
                lastResumeTime = ResumedTime,
                totalPauseTime = GetTotalPauseTime(),
                estimatedTimeSinceEpoch = GetTimeSinceEpoch(),
                splitTimeSinceEpoch = _splitTimeSinceEpoch
            };
        }

        public override void Reconstruct(ExtractedTimelineTimer extracted)
        {
            if (extracted is not ExtractedBranchedTimelineTimer asBranchedTimer)
                throw new ArgumentException("'reduced' must be of type ReducedBranchedTimelineTimer");
            base.Reconstruct(asBranchedTimer);
            _splitTimeSinceEpoch = asBranchedTimer.splitTimeSinceEpoch;
        }
    }

    public class BranchedEventTimeline : StandardEventTimeline
    {
        private readonly BranchedTimelineTimer _timer;
        private readonly ulong _startingId;
        private bool _started;
        
        public BranchedEventTimeline(uint internalId, ulong startingAllocationId) : base(internalId)
        {
            _timer = new BranchedTimelineTimer();
            _startingId = startingAllocationId;
        }

        public override ulong GetEarliestAllocationId() => _startingId;
        public void SetSplitEpoch(long time)
        {
            if (IsTimelineStarted()) return;
            _timer.SetSplitEpoch(time);
        }

        protected override AbstractTimelineTimer GetTimer() => _timer;
        public override bool IsTimelineStarted() => _started;
        public ExtractedEventTimeline ReduceThisTimeline() => base.Extract();

        public override AbstractEventTimeline PrepareReplication() =>
            new BranchedEventTimeline(GetInternalId(), _startingId);
    }
    
    public class MultiTimelineEventLogger : 
        AbstractEventLogger, 
        ISelfReconstructable<ExtractedMultiTimelines>, 
        ISelfReplicateable<MultiTimelineEventLogger>
    {
        private readonly Dictionary<uint, AbstractEventTimeline> _timelinesPool = new();
        private readonly IdAllocator<uint> _idAllocator = new U32IdAllocator();
        private AbstractEventTimeline _originalTimeline;
        private AbstractEventTimeline _activeTimeline;
        

        public virtual uint SplitTimeline(bool startByDefault = true)
        {
            uint newTimelineId = _idAllocator;
            var prevActive = GetActiveTimeline();
            prevActive.AddEvent(new TimelineSplitEvent{ splitInto = newTimelineId});
            var lastTime = prevActive.GetTime();
            prevActive.PauseTimeline();
            var newTimeline = new BranchedEventTimeline(newTimelineId, prevActive.GetLatestAllocationId());
            newTimeline.SetSplitEpoch(lastTime);
            _activeTimeline = newTimeline;
            if (startByDefault) prevActive.StartTimeline();
            return newTimelineId;
        }

        public virtual uint RollbackTimeline(bool autoResume = true)
        {
            if (GetActiveTimeline() is not BranchedEventTimeline activeTimeline ||
                activeTimeline.GetInternalId() == _originalTimeline.GetInternalId()) return 0;
            var re = BacktrackTimeline(autoResume);
            _timelinesPool.Remove(activeTimeline.GetInternalId());
            return re;
        }
        
        public virtual uint BacktrackTimeline(bool autoResume = true)
        {
            if (GetActiveTimeline() is not BranchedEventTimeline activeTimeline ||
                activeTimeline.GetInternalId() == _originalTimeline.GetInternalId()) return 0;
            activeTimeline.PauseTimeline();
            var newActiveTimeline = GetRootBranch(activeTimeline);
            if (newActiveTimeline == null) return 0;
            _activeTimeline = newActiveTimeline;
            if (autoResume) _activeTimeline.ResumeTimeline();
            return _activeTimeline.GetInternalId();
        }

        public bool MergeTimeline(bool autoResume = true)
        {
            if (_activeTimeline is not BranchedEventTimeline asBranch) return false;
            var rootBranch = GetRootBranch(asBranch);
            if (rootBranch == null) return false;
            // if (!(rootBranch != null && rootBranch.IsTimelineMergeableFromTip(_activeTimeline))) return false;
            rootBranch.MergeTimeline(_activeTimeline);
            _activeTimeline.PauseTimeline();
            _timelinesPool.Remove(_activeTimeline.GetInternalId());
            _activeTimeline = rootBranch;
            if (autoResume) _activeTimeline.ResumeTimeline();
            return true;
        }

        public void TrimInactiveBranches()
        {
            var onMainBranch = new HashSet<uint>();
            var iterating = _activeTimeline;
            while (iterating != null)
            {
                onMainBranch.Add(iterating.GetInternalId());
                iterating = GetRootBranch(iterating);
            }

            foreach (var id in _timelinesPool.Keys.Where(id => !onMainBranch.Contains(id)))
            {
                _timelinesPool.Remove(id);
            }
        }

        private AbstractEventTimeline GetRootBranch(AbstractEventTimeline ofBranch)
        {
            return (from pair in _timelinesPool where pair.Value.OwnBranch(ofBranch.GetInternalId()) select pair.Value).FirstOrDefault();
        }
        public bool IsMergeable()
        {
            if (_activeTimeline is not BranchedEventTimeline asBranch) return false;
            var rootBranch = GetRootBranch(asBranch);
            return rootBranch != null && rootBranch.IsTimelineMergeableFromTip(_activeTimeline);
        }
        public ExtractedMultiTimelines ExtractAllTimelines()
        {
            var re = new ExtractedMultiTimelines
            {
                activeTimelineId = GetActiveTimeline().GetInternalId(),
                originalTimelineId = _originalTimeline.GetInternalId(),
                allTimelines = new(_timelinesPool.Count)
            };
            var i = 0;
            foreach (var reduced in _timelinesPool.Select(pair => pair.Value).Select(timeline => (timeline as BranchedEventTimeline)?.ReduceThisTimeline() ?? timeline.Extract()))
            {
                re.allTimelines[i++] = reduced;
            }
            return re;
        }
        protected override AbstractEventTimeline GetActiveTimeline() => _activeTimeline;
        public override bool Initialize()
        {
            if (_activeTimeline != null) return false;
            uint mainTimelineId = _idAllocator;
            _originalTimeline = new StandardEventTimeline(mainTimelineId);
            _timelinesPool[mainTimelineId] = _originalTimeline;
            _activeTimeline = _originalTimeline;
            return true;
        }

        public override bool StartTimeline()
        {
            var active = GetActiveTimeline();
            if (active.IsTimelineStarted()) return false;
            active.StartTimeline();
            return true;
        }
        public override bool ResumeTimeline()
        {
            var active = GetActiveTimeline();
            if (active.IsTimelinePaused()) return false;
            active.ResumeTimeline();
            return true;
        }

        public override bool PauseTimeline()
        {
            var active = GetActiveTimeline();
            if (!active.IsTimelinePaused()) return false;
            active.PauseTimeline();
            return true;
        }
        
        public ExtractedEventTimeline ExtractMainTimeline(AbstractEventTimeline original = null)
        {
            if (original == null) original = _activeTimeline;
            var re = original.Extract();
            if (original is not BranchedEventTimeline asBranch) return re;
            var rootBranch = GetRootBranch(asBranch);
            if (rootBranch == null) return re;
            var rootBranchReduced = ExtractMainTimeline(rootBranch);
            rootBranchReduced.Append(re);
            re = rootBranchReduced;
            return re;
        }
        
        // Reconstruct the timelines based on serialized data
        // All timeline will be paused on reconstruction
        public virtual void Reconstruct(ExtractedMultiTimelines reduced)
        {
            // Create caches
            Dictionary<uint, ExtractedEventTimeline> timelines = new();
            ExtractedEventTimeline originalTimelineData = null;
            ExtractedEventTimeline activeTimelineData = null;
            uint maxId = 0;
            // Add all serialized timelines to caches for faster reading
            foreach (var timeline in reduced.allTimelines)
            {
                // If the iterating timeline has the same ID as the original timeline from the serialized data, mark it
                if (timeline.GetInternalId() == reduced.originalTimelineId) originalTimelineData = timeline;
                if (timeline.GetInternalId() == reduced.activeTimelineId) activeTimelineData = timeline;
                timelines[timeline.GetInternalId()] = timeline;
                maxId = timeline.GetInternalId() > maxId ? timeline.GetInternalId() : maxId;
            }
            // If not original timeline was marked, throw an exception
            if (originalTimelineData == null || activeTimelineData == null)
                throw new Exception("Either active timeline or original timeline was not found");
            // ------------------------------------------------------------------------------
            // Remove the active timeline and clear the timeline pool
            _activeTimeline.PauseTimeline();
            _activeTimeline = null;
            _timelinesPool.Clear();
            // ------------------------------------------------------------------------------
            // Enlist a queue, add the original timeline as its first member
            // Queue element contains a root ID, which is the ID of the original timeline
            // and the serialized timeline data
            // If the ID is 0, this is a main time line (or original timeline)
            var iterationQueue = new Queue<(uint timelineId, ulong splitId, ExtractedEventTimeline tl)>();
            iterationQueue.Enqueue((0U, 0U, originalTimelineData));
            // Iterate over all reachable timelines from the original timeline
            while (iterationQueue.Any())
            {
                var (rootId, currentSplitId, iterating) = iterationQueue.Dequeue();
                try
                {
                    foreach (var clue in iterating.logs)
                    {
                        if (clue.data is not TimelineSplitEvent splitEvent) continue;
                        if (!timelines.TryGetValue(splitEvent.splitInto, out var fetchedTimeline))
                        {
                            throw new Exception(
                                $"Error while reconstructing timeline No.{iterating.GetInternalId()}: Timeline unknown: ${splitEvent.splitInto}");
                        }
                        // Enqueue the selected timeline data, with this timeline ID as the root timeline ID
                        iterationQueue.Enqueue((iterating.GetInternalId(), clue.GetInternalId(), fetchedTimeline));
                    }
                }
                catch (Exception)
                {
                    foreach (var pair in _timelinesPool)
                    {
                        pair.Value.PauseTimeline();
                    }
                    _timelinesPool.Clear();
                    _activeTimeline = null;
                    _originalTimeline = null;
                    throw;
                }

                // If the root ID is 0, construct currentTimeline as a standard timeline
                // If not, construct it as a branched timeline
                AbstractEventTimeline currentTimeline = rootId == 0U
                    ? new StandardEventTimeline(iterating.GetInternalId())
                    : new BranchedEventTimeline(iterating.GetInternalId(), currentSplitId);
                currentTimeline.Reconstruct(iterating);
                _timelinesPool[currentTimeline.GetInternalId()] = currentTimeline;
            }
            // After iterating over all reachable timeline,
            // set the original timeline and active timeline in the object
            _originalTimeline = _timelinesPool[reduced.originalTimelineId];
            _activeTimeline = _timelinesPool[reduced.activeTimelineId];
            _idAllocator.Reset();
            while (_idAllocator < maxId)
            {
                // Make sure the next allocated ID will be bigger than maxId
                // This loop does nothing since IdAllocator<T> automatically increment its counter on implicit conversion
            }
        }

        public virtual MultiTimelineEventLogger PrepareReplication() => new MultiTimelineEventLogger();
        public MultiTimelineEventLogger Replicate()
        {
            var instance = PrepareReplication();
            var extracted = ExtractAllTimelines();
            instance.Reconstruct(extracted);
            return instance;
        }
    }
}