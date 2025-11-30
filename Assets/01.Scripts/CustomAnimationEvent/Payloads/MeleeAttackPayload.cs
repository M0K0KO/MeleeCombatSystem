using System;
using UnityEngine;

[Serializable]
public class MeleeAttackPayload : EventPayload
{
    [SerializeField] public string hitboxName;
    [SerializeField] public bool isGoingToEnable;

    private Collider hitBox;

    public override void Execute(AnimationEventReceiver receiver)
    {
        if (hitBox == null) hitBox = receiver.GetHitBox(hitboxName);
        
        hitBox.enabled = isGoingToEnable;
    }

}