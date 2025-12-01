using System.Collections.Generic;
using UnityEngine;

public class AnimationEventStateBehaviour : StateMachineBehaviour
{
    private AnimationEventReceiver receiver;
    
    [HideInInspector] public List<NormalizedTimeEvent> normalizedTimeEvents = new List<NormalizedTimeEvent>();
    [HideInInspector] public List<StateLifecycleEvent> lifecycleEvents = new();

    
    private float lastNormalizedTime;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (receiver == null) receiver = animator.GetComponent<AnimationEventReceiver>();
        lastNormalizedTime = 0f;

        foreach (var evt in lifecycleEvents)
        {
            if (evt.triggerType == EventTriggerType.OnEnter)
            {
                evt.Execute(receiver);
            }
        }
    }
    
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        foreach (var evt in lifecycleEvents)
        {
            if (evt.triggerType == EventTriggerType.OnExit)
            {
                evt.Execute(receiver);
            }
        }
    }
    
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        float currentNormalizedTime = stateInfo.normalizedTime % 1f;
        
        foreach (var evt in normalizedTimeEvents)
        {
            if (evt.time > lastNormalizedTime && evt.time <= currentNormalizedTime)
            {
                if (receiver != null) evt.Execute(receiver);
            }
        }
        lastNormalizedTime = currentNormalizedTime;
    }
}
