using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public abstract class EventPayload
{
    public abstract void Execute(AnimationEventReceiver receiver);
}