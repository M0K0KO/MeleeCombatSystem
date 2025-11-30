using System;
using System.Collections.Generic;
using UnityEngine;

public class MeleeTracer : MonoBehaviour
{
    [SerializeField] private Transform lineStart;
    [SerializeField] private Transform lineEnd;
    [SerializeField] private float drawDuration = 1.0f;

    [Header("Simulation & Check")]
    [SerializeField] private float resolution = 0.1f;

    [SerializeField] private int maxSteps = 20;
    [SerializeField] private LayerMask hitLayer;

    private bool drawMeleeTrace = false;

    private Vector3 _prevStartPos;
    private Vector3 _prevEndPos;
    private bool _isFirstFrame = true;

    private void LateUpdate()
    {
        if (drawMeleeTrace)
        {
            Vector3 currStart = lineStart.position;
            Vector3 currEnd = lineEnd.position;

            if (!_isFirstFrame)
            {
                float dist = Vector3.Distance(_prevEndPos, currEnd);

                int steps = Mathf.Max(1, Mathf.CeilToInt(dist / resolution));

                Vector3 lastSubStart = _prevStartPos;
                Vector3 lastSubEnd = _prevEndPos;

                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;

                    Vector3 subStart = Vector3.Lerp(_prevStartPos, currStart, t);

                    Vector3 prevDir = _prevEndPos - _prevStartPos;
                    Vector3 currDir = currEnd - currStart;
                    Vector3 subDir = Vector3.Slerp(prevDir, currDir, t);
                    Vector3 subEnd = subStart + subDir;

                    bool isRealKeyframe = (i == steps);
                    Color drawColor = isRealKeyframe ? Color.red : Color.yellow;

                    DrawSweepSegment(lastSubStart, lastSubEnd, subStart, subEnd, drawColor, isRealKeyframe);

                    PerformHitCheck(lastSubStart, lastSubEnd, subStart, subEnd);

                    lastSubStart = subStart;
                    lastSubEnd = subEnd;
                }
            }

            _prevStartPos = currStart;
            _prevEndPos = currEnd;
            _isFirstFrame = false;
        }
    }

    private void DrawSweepSegment(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Color color, bool isKeyframe)
    {
        // p1: 이전 손잡이, p2: 이전 칼끝
        // p3: 현재 손잡이, p4: 현재 칼끝

        if (isKeyframe)
        {
            Debug.DrawLine(p3, p4, color, drawDuration);
        }
        else
        {
            Color dimColor = Color.Lerp(color, Color.clear, 0.5f);
            Debug.DrawLine(p3, p4, dimColor, drawDuration);
        }
    }

    private void PerformHitCheck(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
    {
        // 방법 A: 간단하게 두 칼날 사이를 Linecast (칼날이 얇을 때)
        // 이전 칼 끝 -> 현재 칼 끝 (가장자리 체크)
        // if (Physics.Linecast(p2, p4, out RaycastHit hitEdge, hitLayer)) { ... }

        // 방법 B: 정석적인 면 체크 (BoxCast 또는 Quad Overlap)
        // 완벽한 면 체크를 하려면 Mesh Collider를 굽거나, 중심점에서 BoxOverlap을 해야 합니다.
        // 여기서는 가장 가성비 좋은 "중간 지점 Raycast" 나 "CapsuleCast"를 추천합니다.

        // 예시: 칼의 중심점을 잇는 궤적 검사
        Vector3 prevCenter = (p1 + p2) * 0.5f;
        Vector3 currCenter = (p3 + p4) * 0.5f;
        float bladeLength = Vector3.Distance(p3, p4);

        // 중심점에서 중심점으로 BoxCast나 SphereCast를 쏘는 것이 가장 안전
        if (Physics.Linecast(prevCenter, currCenter, out RaycastHit hit, hitLayer))
        {
            // 히트 처리 (중복 히트 방지 로직 필요)
            Debug.DrawLine(prevCenter, hit.point, Color.cyan, drawDuration); // 히트 지점 표시
            // Debug.Log($"Hit: {hit.collider.name}");
        }
    }


    public void EnableDrawer()
    {
        drawMeleeTrace = true;
        _isFirstFrame = true;
    }

    public void DisableDrawer() => drawMeleeTrace = false;
}