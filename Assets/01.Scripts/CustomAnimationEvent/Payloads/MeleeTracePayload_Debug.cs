using System;

[Serializable]
public class MeleeTracePayload_Debug : EventPayload
{
    public bool isGoingToEnable = false;
    
    public override void Execute(AnimationEventReceiver receiver)
    {
        if (isGoingToEnable) receiver.meleeTracer.EnableDrawer();
        else receiver.meleeTracer.DisableDrawer();
    }
}
