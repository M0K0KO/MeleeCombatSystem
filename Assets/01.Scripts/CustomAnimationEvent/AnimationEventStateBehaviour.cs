using System.Collections.Generic;
using UnityEngine;

public class AnimationEventStateBehaviour : StateMachineBehaviour
{
    private AnimationEventReceiver receiver;
    
    [HideInInspector] public List<TimeLineEvent> events = new List<TimeLineEvent>();
    
    private float lastNormalizedTime;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (receiver == null) receiver = animator.GetComponent<AnimationEventReceiver>();
        lastNormalizedTime = 0f;
    }
    
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        float currentNormalizedTime = stateInfo.normalizedTime % 1f;
        
        foreach (var evt in events)
        {
            if (evt.time > lastNormalizedTime && evt.time <= currentNormalizedTime)
            {
                if (receiver != null) evt.Execute(receiver);
            }
        }
        lastNormalizedTime = currentNormalizedTime;
    }
}
