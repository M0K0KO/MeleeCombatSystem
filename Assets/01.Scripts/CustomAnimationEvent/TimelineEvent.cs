using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TimeLineEvent
{
    public string name = "New Event";
    [Range(0f, 1f)] public float time;
    [SerializeReference] public List<EventPayload> payloads = new List<EventPayload>();

    public void Execute(AnimationEventReceiver receiver)
    {
        foreach (var payload in payloads)
        {
            if (payload != null) payload.Execute(receiver);
        }
    }
}