# Timeline

The Timeline library is a versatile tool for developers to track and manage events within their programs. It enables the creation of timelines that capture various program events, which can be serialized into simplified data classes. Timelines can be split into branches, merged, or pruned, offering flexibility in organizing and analyzing event data. The Timeline library empowers developers with valuable insights into program execution and event history.

## Features

1. Hassle-less Logging:
-   Save any serializable data to the timeline effortlessly.
-   Seamlessly log various program elements onto the timeline.
2. Pauseable Timer:
-   Each timeline has its own independent timer.
-   Pause and resume the timer as needed.
-   Attach timestamps to timeline events for precise tracking.
3. Branching Timelines:
-   Split timelines into separate branches for parallel event tracking.
-   Discard, rollback, or merge branching timelines.
-   Can function as a transaction broker, similar to how database transactions work.
4. Easy Serialization/Deserialization:
-   Convenient methods for extracting stored data from timelines and loggers.
-   Simplify serialization and deserialization processes.
-   Easily restore data using the Reconstruct method.

## Getting Started

### Building the library
To use this library in your projects, follow these steps:

- Make sure that you have .NET 4.8 or higher installed. [Install .NET SDK directly from Microsoft](https://dotnet.microsoft.com/download)
- Clone this repository
- Open the solution in your IDE of choice (I used Rider personally, not sponsored)
- Install the required dependencies by running the following command in the terminal: `dotnet restore`
- Build the "Timeline" project using the "Release" configuration
- The produced library should now be stored in `Timeline/bin/Release`
- Reference the library in your own project to actually use it

### How to use
You should not directly interact with timeline. To create a timeline, initialize a logger (which in turn create a root timeline that is not started yet).
```csharp
// Create logger object
MultiTimelineEventLogger logger = new();
// Create a root timeline
logger.InitializeLogger();
// Start the timeline, now you can actually use it
logger.StartTimeline();
```

The root timeline is currently the active timeline.

### Dataclasses

In Timeline, the product of an `Extract` operation is called `ExtractedDataclass`. Every class that inherit `ExtractedDataclass` must only have fields, as properties will not be serialized at all. It also must not have any cyclic reference, as recursive operations may be performed on these objects. Optionally, they can also have a `Serializable` attribute, which is useful for Unity projects

Let us use this class here for our guide:

```csharp
[Serializable]  
internal class DumpData : ExtractedDataclass  
{  
	public int Test;  
	public string Wow = "";  
}
```

### Add data

To add this class object to our active timeline, simply call `AddEvent`:
```csharp
logger.AddEvent(new DumpData { Test = 42, Wow = "It worked?" });
```

This action will not only store the object to the timeline, but also attach a timestamp to it:
```csharp
_logs.AddLast(new ExtractedEventLog  
{  
	internalId = _idAllocator, // ID is created due to allocator's implicit conversion operation 
	timestamp = GetTimer().GetTimeProcessed(),  
	data = data  
});
```

`GetTimer().GetTimeProcessed()` will only produce correct timestamp if it's started and not paused, if you try to call `AddEvent` from a logger, of which active timeline does not satisfy mentioned conditions then the method will throw `TimelineNotReadyException`.

To pause and resume the active timeline, use the following methods:
```csharp
logger.PauseTimeline();
logger.ResumeTimeline();
```

These methods will return a boolean value, indicating whether or not the action has been carried out successfully. If it's not, then it's most likely the the active timeline is already paused/resumed previously and not because of internal errors.

### Timeline branches

To split the active timeline into a new one, call `SplitTimeline`:
```csharp
logger.SplitTimeline(true); // The first parameter is true by default
```

When `SplitTimeline` is invoked, a `TimelineSplitEvent` will be added to the currently active timeline before it is paused. A new timeline will be created, allocated to an ID, had its epoch setup (to maintain timers' integrity) and marked as the active timeline. If the first parameter, `startByDefault` is true then the new active timeline will be started, before its ID is returned.

To discard this timeline and rollback to a previous one, call `BacktrackTimeline`:
```csharp
logger.RollbackTimeline(true); // The first parameter is true by default
```

When `BacktrackTimeline` is invoked, it find the parent branch of the active timeline. If it is found, pause the currently active timeline, resume the parent one if `autoResume` is true and return its ID. If not, return 0.

This action, however, still retain the inactive timeline, even though it is no longer accessible. To do the same thing as `BacktrackTimeline` but remove the timeline from the timeline pool, call `RollbackTimeline`.

If you accumulated too much inaccessible branches, call `TrimInactiveBranches`. It will prune all branches that are not on the path from the original timeline to the active one.

In case you want to add every events from the branching timeline to its parent instead, use `MergeTimeline`. It will check if the latest event of parent branch is `TimelineSplitEvent`, used to split into the active timeline. If yes, merge the active timeline to its parent's tip. If not, it will merge from where timelines diverge. Both kinds of merge will fix the ID allocation upon merge.

### Data extraction, replication and reconstruction

Both `AbstractEventTimeline` and `MultiTimelineEventLogger` inherit `ISelfExtractable`, `ISelfReconstructable` and `ISelfReplicateable`, which allow serialization and deserialization of data that are being stored
```csharp
// All data from the original timeline to the active one will be condensed into one ExtractedEventTimeline dataclass
var mainTimeline = logger.ExtractMainTimeline(); 
// Every timeline inside the timelines pool will be extracted into an ExtractedMultiTimelines dataclass
var allTimelines = logger.ExtractAllTimelines();
```

These data can now be stored and recover on demand. And if the developer will it, these can be loaded into other `AbstractEventTimeline` or `MultiTimelineEventLogger` to create a near perfect replication of the original object.
```csharp
// Restoring the original timeline from allTimelines
var originalTimelineData = allTimelines.allTimelines[allTimelines.activeTimelineId];
var newTimelineObj = new StandardEventTimeline(originalTimelineData.GetInternalId());
newTimelineObj.Reconstruct(originalTimelineData);
// Restoring the logger
var newLoggerObj = new MultiTimelineEventLogger();
newLoggerObj.Reconstruct(allTimelines);
```

The extracted data is passed-by-reference, which mean that if you accidentally modify it, the data inside the original timeline that it is extract from will be mutated. To prevent this, use `Replicate` to perform a recursive copy on the dataclass itself.
```csharp
// Serialize the logger, and deserialize into a new one
var newLogger = logger.Replicate();
// Serialize the dataclass, which always produce a ExtractedDataclass by default
var oldExtracted = (ExtractedEventTimeline)newTimelineObj.Extract().Replicate();
```

## Unit test

`TimelineTests` is a NUnit project, packed with the `Timeline` project itself. This project carry out fundamental tests on the Timeline's classes, however it is still pretty bare-bones so feel free to add more test cases.
