using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StateLifecycleEvent
{
    public string name;
    public EventTriggerType triggerType;
    [SerializeReference] public List<EventPayload> payloads = new List<EventPayload>();
    
    public void Execute(AnimationEventReceiver receiver)
    {
        foreach (var payload in payloads)
        {
            if (payload != null) payload.Execute(receiver);
        }
    }
}

[Serializable]
public enum EventTriggerType
{
    OnEnter,
    OnExit,
}
