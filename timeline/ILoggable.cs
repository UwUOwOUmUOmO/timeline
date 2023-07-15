namespace Timeline
{
    public interface ILoggable
    {
        public bool StartTimeline();
        public bool ResumeTimeline();
        public bool PauseTimeline();
        public bool IsTimelineStarted();
        public bool IsTimelinePaused();
        public void AddEvent(ExtractedDataclass data);
    }
}