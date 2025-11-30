using System;
using UnityEngine;

public class SimpleController : MonoBehaviour
{
    private Animator animator;

    private Vector3 startPosition;
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            transform.position = startPosition;
            animator.SetTrigger("AttackTrigger");
        }
    }
}
