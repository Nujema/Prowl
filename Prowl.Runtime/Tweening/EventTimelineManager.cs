// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System.Collections.Generic;

namespace Prowl.Runtime.Tweening;

internal static class EventTimelineManager
{
    private static readonly List<EventTimeline> _eventTimelines = new();

    public static void Initialize()
    {
        // Test();
    }

    private static void Test()
    {
        var foo = EventTimeline.Create()
            .AppendWait(1.1f)
            .AppendAction(() => Debug.Log(Time.CurrentTime.time.ToString("F1") + " Hello World!"))
            .AppendWait(2.2f)
            .InsertAction(0, () => Debug.Log(Time.CurrentTime.time.ToString("F1") + " This is the start of test!"))
            .AppendAction(() => Debug.Log(Time.CurrentTime.time.ToString("F1") + " This is the end of test!"))
            .ParallelAction(() => Debug.Log(Time.CurrentTime.time.ToString("F1") + " Good bye!"));
        Debug.Log(foo + "\n");
    }

    public static void Update()
    {
        for (int i = _eventTimelines.Count - 1; i >= 0; i--)
        {
            var eventTimeline = _eventTimelines[i];
            eventTimeline.Update();
            if (eventTimeline.HasFinished) _eventTimelines.RemoveAt(i);
        }
    }

    public static void Add(EventTimeline timeline)
    {
        _eventTimelines.Add(timeline);
    }

}
