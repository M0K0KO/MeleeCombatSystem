using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
struct HitBox
{
    public string name;
    public Collider hitBox;
}

public class AnimationEventReceiver : MonoBehaviour
{
    [SerializeField] private List<HitBox> hitBoxes = new List<HitBox>();
    private Dictionary<string, Collider> hitBoxesDictionary = new Dictionary<string, Collider>();

    public MeleeTracer meleeTracer { get; private set; }

    private void Awake()
    {
        meleeTracer = GetComponentInChildren<MeleeTracer>();
    }

    private void Start()
    {
        foreach (HitBox hitBox in hitBoxes)
        {
            hitBoxesDictionary.Add(hitBox.name, hitBox.hitBox);
        }
    }

    public Collider GetHitBox(string name)
    {
        if (hitBoxesDictionary.TryGetValue(name, out Collider collider))
        {
            return collider;
        }
        else
        {
            return null;
        }
    }
}
