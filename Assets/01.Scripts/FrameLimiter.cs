using System;
using UnityEngine;

public class FrameLimiter : MonoBehaviour
{
    [SerializeField] private bool limitFPS = true;
    [SerializeField] private int targetFrameRate = 60;

    private void Awake()
    {
        if (limitFPS) Application.targetFrameRate = targetFrameRate;
        else Application.targetFrameRate = 1000;
    }

    private void OnValidate()
    {
        if (limitFPS) Application.targetFrameRate = targetFrameRate;
        else Application.targetFrameRate = 1000;
    }
}
