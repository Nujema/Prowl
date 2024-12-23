// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Prowl.Runtime.Tweening;

public class EventTimeline
{
    private class TimedEvent(Action eventAction, float invokeTime, float duration) : IComparable<TimedEvent>
    {
        public readonly Action EventAction = eventAction;
        public readonly float InvokeTime = invokeTime;
        public float CompletionTime => InvokeTime + duration;

        public int CompareTo(TimedEvent? other) => other == null ? 1 : InvokeTime.CompareTo(other.InvokeTime);
    }

    private readonly List<TimedEvent> _sortedTimedEvents = [];
    private float _elapsedTime;
    public float SequenceDuration => _sortedTimedEvents.Count == 0 ? 0 : _sortedTimedEvents.Max(te => te.CompletionTime);
    private float LatestInvokeTime => _sortedTimedEvents.Count == 0 ? 0 : _sortedTimedEvents.Max(te => te.InvokeTime);
    internal bool HasFinished => _sortedTimedEvents.Count == 0;

    private EventTimeline() {}
    public static EventTimeline Create()
    {
        var eventTimeline = new EventTimeline();
        EventTimelineManager.Add(eventTimeline);
        return eventTimeline;
    }

    internal void Update()
    {
        _elapsedTime += Time.deltaTimeF;

        while (_sortedTimedEvents.Count > 0 && _sortedTimedEvents[0].InvokeTime <= _elapsedTime)
        {
            var timedEvent = _sortedTimedEvents[0];
            timedEvent.EventAction.Invoke();
            _sortedTimedEvents.RemoveAt(0);
        }
    }

    private EventTimeline Insert(Action action, float invokeTime, float actionDuration)
    {
        var timeEvent = new TimedEvent(action, invokeTime, actionDuration);
        _sortedTimedEvents.Add(timeEvent);
        _sortedTimedEvents.Sort((a, b) => a.CompareTo(b));
        return this;
    }

    public EventTimeline InsertAction(float invokeTime, Action action) => Insert(action, invokeTime, 0);
    public EventTimeline InsertTween(float invokeTime, Tween tween)
    {
        tween.Pause();
        return Insert(tween.Resume, invokeTime, tween.Duration() ?? 0);
    }

    public EventTimeline AppendWait(float seconds) => Insert(() => {}, SequenceDuration, seconds);

    public EventTimeline AppendAction(Action action) => InsertAction(SequenceDuration, action);
    public EventTimeline ParallelAction(Action action) => InsertAction(LatestInvokeTime, action);

    public EventTimeline AppendTween(Tween tween) => InsertTween(SequenceDuration, tween);
    public EventTimeline ParallelTween(Tween tween) => InsertTween(LatestInvokeTime, tween);

    public override string ToString()
        => string.Join('\n', _sortedTimedEvents.Select(te => $"{te.InvokeTime} => {te.CompletionTime}"));
}


