using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class AnimationEventEditorWindow : EditorWindow
{
    private AnimationEventStateBehaviour targetBehaviour;
    private SerializedObject serializedBehaviour;
    private SerializedProperty normalizedEventsProp;
    private SerializedProperty lifecycleEventsProp;

    private AnimationClip previewClip;
    private float globalPreviewNormalizedTime;
    private bool isPreviewing;

    private int selectedEventIndex = -1;
    private int selectedPayloadIndex = -1;

    private Vector2 _scrollPos;

    private bool _assemblyReloading = false;

    private struct PayloadMarker
    {
        public int eventIndex;
        public int payloadIndex;
        public float time;
    }

    [MenuItem("Window/Animation Event Editor")]
    public static void Open()
    {
        var window = GetWindow<AnimationEventEditorWindow>("Animation Event Editor");
        window.Show();
    }

    private void OnEnable()
    {
        UpdateTargetFromSelection();

        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        AssemblyReloadEvents.afterAssemblyReload += AfterReload;
    }

    private void OnDisable()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        AssemblyReloadEvents.afterAssemblyReload -= AfterReload;
    }

    private void BeforeReload()
    {
        _assemblyReloading = true;
    }

    private void AfterReload()
    {
        _assemblyReloading = false;
        UpdateTargetFromSelection();
    }


    private void OnSelectionChange()
    {
        if (EditorApplication.isPlaying)
        {
            targetBehaviour = null;
            serializedBehaviour = null;
            normalizedEventsProp = null;
            return;
        }

        UpdateTargetFromSelection();
    }

    private void UpdateTargetFromSelection()
    {
        if (EditorApplication.isPlaying) return;

        AnimationEventStateBehaviour found = null;

        if (Selection.activeObject is AnimationEventStateBehaviour behAsset)
        {
            found = behAsset;
        }
        else if (Selection.activeObject is AnimatorState state)
        {
            found = state.behaviours
                .OfType<AnimationEventStateBehaviour>()
                .FirstOrDefault();
        }
        else
        {
            found = targetBehaviour;
        }

        SetTarget(found);
    }

    private void SetTarget(AnimationEventStateBehaviour beh)
    {
        if (beh == targetBehaviour) return;


        targetBehaviour = beh;

        if (targetBehaviour != null)
        {
            serializedBehaviour = new SerializedObject(targetBehaviour);
            normalizedEventsProp = serializedBehaviour.FindProperty("normalizedTimeEvents");
            lifecycleEventsProp = serializedBehaviour.FindProperty("lifecycleEvents");
        }
        else
        {
            serializedBehaviour = null;
            normalizedEventsProp = null;
            lifecycleEventsProp = null;
        }

        Repaint();
    }

    bool Validate(AnimationEventStateBehaviour stateBehaviour, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (stateBehaviour == null)
        {
            errorMessage = "No AnimationEventStateBehaviour selected.";
            return false;
        }

        // 기존 GetValidAnimatorController + matchingState 로직 그대로
        AnimatorController animatorController = GetValidAnimatorController(out errorMessage);
        if (animatorController == null) return false;

        ChildAnimatorState matchingState = animatorController.layers
            .SelectMany(layer => layer.stateMachine.states)
            .FirstOrDefault(state => state.state.behaviours.Contains(stateBehaviour));

        previewClip = matchingState.state?.motion as AnimationClip;
        if (previewClip == null)
        {
            errorMessage = "No Valid AnimationClip found for the current state.";
            return false;
        }

        return true;
    }

    AnimatorController GetValidAnimatorController(out string errorMessage)
    {
        errorMessage = string.Empty;

        GameObject targetGameObject = Selection.activeGameObject;
        if (targetGameObject == null)
        {
            errorMessage = "Please select a GameObject with an Animator to preview.";
            return null;
        }

        Animator animator = targetGameObject.GetComponent<Animator>();
        if (animator == null)
        {
            errorMessage = "The selected GameObject does not have an Animator component.";
            return null;
        }

        AnimatorController animatorController = animator.runtimeAnimatorController as AnimatorController;
        if (animatorController == null)
        {
            errorMessage = "The selected Animator does not have a valid AnimatorController.";
            return null;
        }

        return animatorController;
    }

    [MenuItem("GameObject/Enforce T-Pose", false, 0)]
    static void EnforceTPose()
    {
        GameObject selected = Selection.activeGameObject;
        if (!selected || !selected.TryGetComponent(out Animator animator) || !animator.avatar) return;

        SkeletonBone[] skeletonBones = animator.avatar.humanDescription.skeleton;

        foreach (HumanBodyBones hbb in Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (hbb == HumanBodyBones.LastBone) continue;

            Transform boneTransform = animator.GetBoneTransform(hbb);
            if (!boneTransform) continue;

            SkeletonBone skeletonBone = skeletonBones.FirstOrDefault(sb => sb.name == boneTransform.name);
            if (skeletonBone.name == null) continue;

            if (hbb == HumanBodyBones.Hips) boneTransform.localPosition = skeletonBone.position;
            boneTransform.localRotation = skeletonBone.rotation;
        }

        Debug.Log("T-Pose enforced successfully on " + selected.name);
    }

    private void PreviewAnimationClip()
    {
        if (previewClip == null || !isPreviewing || targetBehaviour == null) return;

        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();

        GameObject go = Selection.activeGameObject;
        if (!go) return;

        Vector3 savedPos = go.transform.localPosition;
        Quaternion savedRot = go.transform.localRotation;
        Vector3 savedScale = go.transform.localScale;

        float sampleTime = globalPreviewNormalizedTime * previewClip.length;

        AnimationMode.BeginSampling();

        AnimationMode.SampleAnimationClip(go, previewClip, sampleTime);
        go.transform.localPosition = savedPos;
        go.transform.localRotation = savedRot;
        go.transform.localScale = savedScale;

        AnimationMode.EndSampling();

        AutoHighLightEventForPreview();

        SceneView.RepaintAll();
    }

    private Color GetColorForPayloadType(Type t)
    {
        int hash = t.Name.GetHashCode();
        float h = (Mathf.Abs(hash) % 360) / 360f;
        float s = 0.6f;
        float v = 0.9f;
        return Color.HSVToRGB(h, s, v);
    }

    private void AutoHighLightEventForPreview()
    {
        if (normalizedEventsProp == null || normalizedEventsProp.arraySize == 0) return;

        float t = Mathf.Clamp01(globalPreviewNormalizedTime);

        int bestIndex = -1;
        float bestDist = float.MaxValue;

        for (int i = 0; i < normalizedEventsProp.arraySize; i++)
        {
            SerializedProperty evtProp = normalizedEventsProp.GetArrayElementAtIndex(i);
            SerializedProperty timeProp = evtProp.FindPropertyRelative("time");

            float evTime = Mathf.Clamp01(timeProp.floatValue);
            float dist = Mathf.Abs(evTime - t);

            if (dist < bestDist)
            {
                bestDist = dist;
                bestIndex = i;
            }
        }

        if (bestDist > 0.01f) return;

        if (bestIndex >= 0)
        {
            selectedEventIndex = bestIndex;

            Repaint();
        }
    }

    private void OnGUI()
    {
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Animation Event Editor is disabled while in Play mode.",
                MessageType.Info);
            return;
        }

        if (targetBehaviour == null)
        {
            EditorGUILayout.HelpBox(
                "Select an AnimationEventStateBehaviour in the Inspector,\n" +
                "or drag one here.",
                MessageType.Info);

            var evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (DragAndDrop.objectReferences.Length > 0 &&
                    DragAndDrop.objectReferences[0] is AnimationEventStateBehaviour beh)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        SetTarget(beh);
                    }

                    evt.Use();
                }
            }

            return;
        }

        if (serializedBehaviour == null)
        {
            SetTarget(targetBehaviour);
        }

        if (serializedBehaviour == null)
        {
            EditorGUILayout.HelpBox(
                "SerializedObject is not ready.\nTry reselecting the state or reopening the window.",
                MessageType.Warning);
            return;
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        // --- Animation Preview 영역 ---
        string errorMessage;
        bool valid = Validate(targetBehaviour, out errorMessage);

        EditorGUILayout.Space();

        if (valid)
        {
            if (isPreviewing)
            {
                globalPreviewNormalizedTime = EditorGUILayout.Slider("Normalized Time",
                    globalPreviewNormalizedTime, 0f, 1f);

                if (GUILayout.Button("Stop Preview"))
                {
                    EnforceTPose();
                    isPreviewing = false;
                }
                else
                {
                    PreviewAnimationClip();
                }
            }
            else
            {
                if (GUILayout.Button("Preview"))
                {
                    isPreviewing = true;
                }
            }

            GUILayout.Label($"Previewing at {globalPreviewNormalizedTime:F2}s", EditorStyles.helpBox);
        }
        else
        {
            EditorGUILayout.HelpBox(errorMessage, MessageType.Info);
        }

        EditorGUILayout.Space();

        // --- Serialized 데이터 편집 ---
        serializedBehaviour.Update();

        DrawEventsMiniBar(targetBehaviour);
        DrawPayloadMinibars(targetBehaviour);

        DrawNormalizedTimeEventsSection(targetBehaviour);
        DrawLifecycleEventSection(targetBehaviour);

        serializedBehaviour.ApplyModifiedProperties();

        EditorGUILayout.EndScrollView();
    }

    private void DrawNormalizedTimeEventsSection(AnimationEventStateBehaviour stateBehaviour)
    {
        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.LabelField("NormalizedTime Events", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Expand All"))
            {
                for (int i = 0; i < normalizedEventsProp.arraySize; i++)
                {
                    SerializedProperty evt = normalizedEventsProp.GetArrayElementAtIndex(i);
                    evt.isExpanded = true;
                }
            }

            if (GUILayout.Button("Collapse All"))
            {
                for (int i = 0; i < normalizedEventsProp.arraySize; i++)
                {
                    SerializedProperty evt = normalizedEventsProp.GetArrayElementAtIndex(i);
                    evt.isExpanded = false;
                }
            }

            if (GUILayout.Button("Sort"))
            {
                Undo.RecordObject(stateBehaviour, "Sorting normalizedTimeEvents");

                var beh = (AnimationEventStateBehaviour)stateBehaviour;
                beh.normalizedTimeEvents.Sort((a, b) => a.time.CompareTo(b.time));
                EditorUtility.SetDirty(stateBehaviour);
            }

            if (GUILayout.Button("+ Add Event"))
            {
                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent("Normalized Time Event"), false, () =>
                {
                    if (!targetBehaviour) return;

                    var so = new SerializedObject(targetBehaviour);
                    var eventsProp = so.FindProperty("normalizedTimeEvents");

                    eventsProp.arraySize++;
                    var evt = eventsProp.GetArrayElementAtIndex(eventsProp.arraySize - 1);

                    evt.FindPropertyRelative("name").stringValue = "New Event";
                    evt.FindPropertyRelative("time").floatValue = 0f;
                    evt.FindPropertyRelative("payloads").arraySize = 0;

                    so.ApplyModifiedProperties();
                    Repaint();
                    GUIUtility.ExitGUI();
                });
                
                menu.ShowAsContext();
            }
        }
        EditorGUILayout.EndHorizontal();
        // ---------------------------------------------------------------------------------

        // ---------------------------------------------------------------------------------
        EditorGUILayout.BeginVertical("box");
        {
            for (int i = 0; i < normalizedEventsProp.arraySize; i++)
            {
                SerializedProperty evt = normalizedEventsProp.GetArrayElementAtIndex(i);
                SerializedProperty payloads = evt.FindPropertyRelative("payloads");

                // ---------------------------------------------------------------------------------
                EditorGUILayout.BeginVertical("box");
                {
                    // ---------------------------------------------------------------------------------
                    EditorGUILayout.BeginHorizontal();
                    {
                        string title = evt.FindPropertyRelative("name").stringValue;
                        evt.isExpanded = EditorGUILayout.Foldout(evt.isExpanded, title);


                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Preview"))
                        {
                            SerializedProperty timeProp = evt.FindPropertyRelative("time");
                            float t = Mathf.Clamp01(timeProp.floatValue);

                            globalPreviewNormalizedTime = t;
                            isPreviewing = true;

                            if (Validate(stateBehaviour, out string msg))
                            {
                                PreviewAnimationClip();
                            }
                            else
                            {
                                EditorGUILayout.HelpBox(msg, MessageType.Info);
                            }

                            for (int k = 0; k < normalizedEventsProp.arraySize; k++)
                            {
                                var other = normalizedEventsProp.GetArrayElementAtIndex(k);
                                other.isExpanded = (k == i);
                            }

                            GUI.FocusControl(null);
                            Repaint();
                        }

                        if (GUILayout.Button("Toggle Fold"))
                        {
                            evt.isExpanded = !evt.isExpanded;
                        }

                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            normalizedEventsProp.DeleteArrayElementAtIndex(i);
                            serializedBehaviour.ApplyModifiedProperties();
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    // ---------------------------------------------------------------------------------

                    if (evt.isExpanded)
                    {
                        EditorGUI.indentLevel++;
                        {
                            EditorGUILayout.PropertyField(evt.FindPropertyRelative("name"));
                            EditorGUILayout.PropertyField(evt.FindPropertyRelative("time"));

                            // ---------------------------------------------------------------------------------
                            EditorGUILayout.BeginHorizontal();
                            {
                                EditorGUILayout.LabelField("Payloads", EditorStyles.boldLabel);
                                GUILayout.FlexibleSpace();

                                if (GUILayout.Button("Expand All"))
                                {
                                    for (int j = 0; j < payloads.arraySize; j++)
                                    {
                                        SerializedProperty payload = payloads.GetArrayElementAtIndex(j);
                                        payload.isExpanded = true;
                                    }
                                }

                                if (GUILayout.Button("Collapse All"))
                                {
                                    for (int j = 0; j < payloads.arraySize; j++)
                                    {
                                        SerializedProperty payload = payloads.GetArrayElementAtIndex(j);
                                        payload.isExpanded = false;
                                    }
                                }

                                if (GUILayout.Button("+ Add Payload"))
                                {
                                    GenericMenu menu = new GenericMenu();

                                    var payloadTypes = TypeCache
                                        .GetTypesDerivedFrom<EventPayload>()
                                        .Where(t => !t.IsAbstract)
                                        .OrderBy(t => t.Name);

                                    foreach (Type type in payloadTypes)
                                    {
                                        string menuName =
                                            ObjectNames.NicifyVariableName(type.Name.Replace("Payload", ""));

                                        menu.AddItem(new GUIContent(menuName), false, () =>
                                        {
                                            payloads.serializedObject.Update();

                                            int index = payloads.arraySize;
                                            payloads.arraySize++;

                                            SerializedProperty element = payloads.GetArrayElementAtIndex(index);
                                            element.managedReferenceValue = Activator.CreateInstance(type);

                                            element.isExpanded = true;

                                            payloads.serializedObject.ApplyModifiedProperties();
                                        });
                                    }

                                    menu.ShowAsContext();
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                            // ---------------------------------------------------------------------------------


                            // ---------------------------------------------------------------------------------
                            EditorGUILayout.BeginVertical("box");
                            {
                                EditorGUI.indentLevel++;
                                {
                                    for (int j = 0; j < payloads.arraySize; j++)
                                    {
                                        SerializedProperty payload = payloads.GetArrayElementAtIndex(j);

                                        // ---------------------------------------------------------------------------------
                                        EditorGUILayout.BeginVertical("box");
                                        {
                                            // ---------------------------------------------------------------------------------
                                            EditorGUILayout.BeginHorizontal();
                                            {
                                                string className = ObjectNames.NicifyVariableName(
                                                    payload.managedReferenceValue.GetType().Name.Replace("Payload", "")
                                                );
                                                payload.isExpanded =
                                                    EditorGUILayout.Foldout(payload.isExpanded, className);

                                                GUILayout.FlexibleSpace();
                                                if (GUILayout.Button("X", GUILayout.Width(20)))
                                                {
                                                    payloads.DeleteArrayElementAtIndex(j);
                                                    serializedBehaviour.ApplyModifiedProperties();
                                                    GUIUtility.ExitGUI();
                                                }
                                            }
                                            EditorGUILayout.EndHorizontal();
                                            // ---------------------------------------------------------------------------------
                                        }
                                        EditorGUILayout.EndVertical();
                                        // ---------------------------------------------------------------------------------

                                        if (payload.isExpanded)
                                        {
                                            EditorGUI.indentLevel++;
                                            {
                                                object boxed = payload.managedReferenceValue;

                                                if (boxed == null)
                                                {
                                                    EditorGUILayout.HelpBox("Payload is null", MessageType.Warning);
                                                }
                                                else
                                                {
                                                    Type payloadType = boxed.GetType();

                                                    FieldInfo[] fields = payloadType.GetFields(
                                                        BindingFlags.Instance | BindingFlags.Public |
                                                        BindingFlags.NonPublic);

                                                    foreach (FieldInfo field in fields)
                                                    {
                                                        bool isPublicSerialized =
                                                            field.IsPublic && !Attribute.IsDefined(field,
                                                                typeof(NonSerializedAttribute), true);
                                                        bool hasSerializeField =
                                                            Attribute.IsDefined(field, typeof(SerializeField), true);

                                                        if (!isPublicSerialized && !hasSerializeField)
                                                            continue;

                                                        SerializedProperty fieldProp =
                                                            payload.FindPropertyRelative(field.Name);
                                                        if (fieldProp == null)
                                                            continue;

                                                        string niceName = ObjectNames.NicifyVariableName(field.Name);
                                                        EditorGUILayout.PropertyField(fieldProp,
                                                            new GUIContent(niceName), true);
                                                    }
                                                }
                                            }
                                            EditorGUI.indentLevel--;
                                        }
                                    }
                                }
                                EditorGUI.indentLevel--;
                            }
                            EditorGUILayout.EndVertical();
                            // ---------------------------------------------------------------------------------
                        }
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUILayout.EndVertical();
                // ---------------------------------------------------------------------------------
            }
        }
        EditorGUILayout.EndVertical();
        // ---------------------------------------------------------------------------------
    }

    private void DrawEventsMiniBar(AnimationEventStateBehaviour stateBehaviour)
    {
        if (normalizedEventsProp == null || normalizedEventsProp.arraySize == 0) return;

        GUILayout.Space(4);
        EditorGUILayout.LabelField("Event Mini Bar", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        {
            // 왼쪽 라벨 (Payload Mini Bars랑 동일 스타일)
            GUILayout.Label("Events", GUILayout.Width(120f));

            // 오른쪽에 한 줄짜리 미니바
            Rect rect = GUILayoutUtility.GetRect(0f, 16f, GUILayout.ExpandWidth(true));
            DrawSingleEventMiniBar(rect, stateBehaviour);
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
    }

    private void DrawSingleEventMiniBar(Rect rect, AnimationEventStateBehaviour stateBehaviour)
    {
        if (normalizedEventsProp == null || normalizedEventsProp.arraySize == 0) return;

        Event e = Event.current;

        // 배경
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));

        Rect inner = new Rect(rect.x + 4f, rect.y + 3f, rect.width - 8f, rect.height - 6f);
        EditorGUI.DrawRect(inner, new Color(0.10f, 0.10f, 0.10f, 1f));

        for (int i = 0; i < normalizedEventsProp.arraySize; i++)
        {
            SerializedProperty evtProp = normalizedEventsProp.GetArrayElementAtIndex(i);
            SerializedProperty timeProp = evtProp.FindPropertyRelative("time");

            float t = Mathf.Clamp01(timeProp.floatValue);

            float x = Mathf.Lerp(inner.x, inner.xMax, t);
            Rect markerRect = new Rect(x - 3f, inner.y, 6f, inner.height);

            bool isSelected = (i == selectedEventIndex);

            Color markerColor = isSelected
                ? new Color(0.3f, 0.7f, 1f, 1f) // 선택된 이벤트 색
                : new Color(0.9f, 0.9f, 0.9f, 1f);

            EditorGUI.DrawRect(markerRect, markerColor);

            // 클릭 시 해당 이벤트로 점프 + 펼치기
            if (e.type == EventType.MouseDown && markerRect.Contains(e.mousePosition))
            {
                selectedEventIndex = i;

                // 이벤트 Foldout 정리
                for (int k = 0; k < normalizedEventsProp.arraySize; k++)
                {
                    SerializedProperty otherEvt = normalizedEventsProp.GetArrayElementAtIndex(k);
                    otherEvt.isExpanded = (k == i);
                }

                // 프리뷰 타임 이동
                globalPreviewNormalizedTime = t;
                isPreviewing = true;

                GUI.FocusControl(null);
                GUI.changed = true;

                e.Use();
            }
        }
    }

    private void DrawPayloadMinibars(AnimationEventStateBehaviour stateBehaviour)
    {
        if (normalizedEventsProp == null || normalizedEventsProp.arraySize == 0) return;

        var groups = new Dictionary<Type, List<PayloadMarker>>();

        for (int i = 0; i < normalizedEventsProp.arraySize; i++)
        {
            SerializedProperty evtProp = normalizedEventsProp.GetArrayElementAtIndex(i);
            SerializedProperty timeProp = evtProp.FindPropertyRelative("time");
            SerializedProperty payloadsProp = evtProp.FindPropertyRelative("payloads");

            float t = Mathf.Clamp01(timeProp.floatValue);

            for (int j = 0; j < payloadsProp.arraySize; j++)
            {
                SerializedProperty payloadProp = payloadsProp.GetArrayElementAtIndex(j);
                object boxed = payloadProp.managedReferenceValue;
                if (boxed == null) continue;

                Type payloadType = boxed.GetType();

                if (!groups.TryGetValue(payloadType, out var list))
                {
                    list = new List<PayloadMarker>();
                    groups[payloadType] = list;
                }

                list.Add(new PayloadMarker
                {
                    eventIndex = i,
                    payloadIndex = j,
                    time = t
                });
            }
        }

        if (groups.Count == 0) return;

        GUILayout.Space(4);
        EditorGUILayout.LabelField("Payload Mini Bars", EditorStyles.boldLabel);

        foreach (var kv in groups.OrderBy(k => k.Key.Name))
        {
            Type payloadType = kv.Key;
            List<PayloadMarker> markers = kv.Value;

            string label = ObjectNames.NicifyVariableName(payloadType.Name.Replace("Payload", ""));

            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.Label(label, GUILayout.Width(120f));

                Rect rect = GUILayoutUtility.GetRect(0f, 16f, GUILayout.ExpandWidth(true));
                DrawSinglePayloadMiniBar(rect, payloadType, markers, stateBehaviour);
            }
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(4);
    }

    private void DrawSinglePayloadMiniBar(Rect rect, Type payloadType, List<PayloadMarker> markers,
        AnimationEventStateBehaviour stateBehaviour)
    {
        if (markers == null || markers.Count == 0) return;

        Event e = Event.current;

        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));

        Rect inner = new Rect(rect.x + 4f, rect.y + 3f, rect.width - 8f, rect.height - 6f);
        EditorGUI.DrawRect(inner, new Color(0.10f, 0.10f, 0.10f, 1f));

        Color baseColor = GetColorForPayloadType(payloadType);
        for (int i = 0; i < markers.Count; i++)
        {
            var marker = markers[i];
            float t = Mathf.Clamp01(marker.time);

            float x = Mathf.Lerp(inner.x, inner.xMax, t);
            Rect markerRect = new Rect(x - 3f, inner.y, 6f, inner.height);

            bool isSelected =
                (marker.eventIndex == selectedEventIndex &&
                 marker.payloadIndex == selectedPayloadIndex);

            Color color = isSelected
                ? Color.Lerp(baseColor, Color.white, 0.4f)
                : baseColor;

            EditorGUI.DrawRect(markerRect, color);

            if (e.type == EventType.MouseDown && markerRect.Contains(e.mousePosition))
            {
                selectedEventIndex = marker.eventIndex;
                selectedPayloadIndex = marker.payloadIndex;

                for (int k = 0; k < normalizedEventsProp.arraySize; k++)
                {
                    SerializedProperty evt = normalizedEventsProp.GetArrayElementAtIndex(k);
                    evt.isExpanded = (k == selectedEventIndex);
                }

                globalPreviewNormalizedTime = t;
                isPreviewing = true;

                GUI.FocusControl(null);
                GUI.changed = true;

                e.Use();
            }
        }
    }

    private void DrawLifecycleEventSection(AnimationEventStateBehaviour stateBehaviour)
    {
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.LabelField("Lifecycle Events", EditorStyles.boldLabel);
            
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Expand All"))
            {
                for (int i = 0; i < lifecycleEventsProp.arraySize; i++)
                {
                    SerializedProperty evt = lifecycleEventsProp.GetArrayElementAtIndex(i);
                    evt.isExpanded = true;
                }
            }

            if (GUILayout.Button("Collapse All"))
            {
                for (int i = 0; i < lifecycleEventsProp.arraySize; i++)
                {
                    SerializedProperty evt = lifecycleEventsProp.GetArrayElementAtIndex(i);
                    evt.isExpanded = false;
                }
            }
            
            if (GUILayout.Button("+ Add Event"))
            {
                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent("Lifecycle/OnEnter"), false, () =>
                {
                    if (!targetBehaviour) return;

                    var so = new SerializedObject(targetBehaviour);
                    var lcProp = so.FindProperty("lifecycleEvents");

                    lcProp.arraySize++;
                    var evt = lcProp.GetArrayElementAtIndex(lcProp.arraySize - 1);

                    evt.FindPropertyRelative("name").stringValue = "OnEnter Event";
                    evt.FindPropertyRelative("triggerType").enumValueIndex = 0; 
                    evt.FindPropertyRelative("payloads").arraySize = 0;

                    so.ApplyModifiedProperties();
                    Repaint();
                    GUIUtility.ExitGUI();
                });

                menu.AddItem(new GUIContent("Lifecycle/OnExit"), false, () =>
                {
                    if (!targetBehaviour) return;

                    var so = new SerializedObject(targetBehaviour);
                    var lcProp = so.FindProperty("lifecycleEvents");

                    lcProp.arraySize++;
                    var evt = lcProp.GetArrayElementAtIndex(lcProp.arraySize - 1);

                    evt.FindPropertyRelative("name").stringValue = "OnExit Event";
                    evt.FindPropertyRelative("triggerType").enumValueIndex = 1; 
                    evt.FindPropertyRelative("payloads").arraySize = 0;

                    so.ApplyModifiedProperties();
                    Repaint();
                    GUIUtility.ExitGUI();
                });
                
                menu.ShowAsContext();
            }
        }
        EditorGUILayout.EndHorizontal();

        if (lifecycleEventsProp == null) return;

        EditorGUILayout.BeginVertical("box");
        {
            for (int i = 0; i < lifecycleEventsProp.arraySize; i++)
            {
                SerializedProperty evt = lifecycleEventsProp.GetArrayElementAtIndex(i);
                SerializedProperty triggerTypeProp = evt.FindPropertyRelative("triggerType");
                SerializedProperty payloads = evt.FindPropertyRelative("payloads");

                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        string title = evt.FindPropertyRelative("name").stringValue;
                        evt.isExpanded = EditorGUILayout.Foldout(evt.isExpanded, title);

                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            lifecycleEventsProp.DeleteArrayElementAtIndex(i);
                            serializedBehaviour.ApplyModifiedProperties();
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    if (evt.isExpanded)
                    {
                        EditorGUI.indentLevel++;

                        EditorGUILayout.PropertyField(evt.FindPropertyRelative("name"));
                        EditorGUILayout.PropertyField(triggerTypeProp);

                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.LabelField("Payloads", EditorStyles.boldLabel);
                            GUILayout.FlexibleSpace();

                            if (GUILayout.Button("Expand All"))
                            {
                                for (int j = 0; j < payloads.arraySize; j++)
                                {
                                    SerializedProperty payload = payloads.GetArrayElementAtIndex(j);
                                    payload.isExpanded = true;
                                }
                            }

                            if (GUILayout.Button("Collapse All"))
                            {
                                for (int j = 0; j < payloads.arraySize; j++)
                                {
                                    SerializedProperty payload = payloads.GetArrayElementAtIndex(j);
                                    payload.isExpanded = false;
                                }
                            }

                            if (GUILayout.Button("+ Add Payload"))
                            {
                                GenericMenu menu = new GenericMenu();

                                var payloadTypes = TypeCache
                                    .GetTypesDerivedFrom<EventPayload>()
                                    .Where(t => !t.IsAbstract)
                                    .OrderBy(t => t.Name);

                                foreach (Type type in payloadTypes)
                                {
                                    string menuName = ObjectNames.NicifyVariableName(type.Name.Replace("Payload", ""));

                                    menu.AddItem(new GUIContent(menuName), false, () =>
                                    {
                                        payloads.serializedObject.Update();

                                        int index = payloads.arraySize;
                                        payloads.arraySize++;

                                        SerializedProperty element = payloads.GetArrayElementAtIndex(index);
                                        element.managedReferenceValue = Activator.CreateInstance(type);

                                        element.isExpanded = true;

                                        payloads.serializedObject.ApplyModifiedProperties();
                                    });
                                }

                                menu.ShowAsContext();
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                        // ---------------------------------------------------------------------------------


                        // ---------------------------------------------------------------------------------
                        EditorGUILayout.BeginVertical("box");
                        {
                            EditorGUI.indentLevel++;
                            {
                                for (int j = 0; j < payloads.arraySize; j++)
                                {
                                    SerializedProperty payload = payloads.GetArrayElementAtIndex(j);

                                    // ---------------------------------------------------------------------------------
                                    EditorGUILayout.BeginVertical("box");
                                    {
                                        // ---------------------------------------------------------------------------------
                                        EditorGUILayout.BeginHorizontal();
                                        {
                                            string className = ObjectNames.NicifyVariableName(
                                                payload.managedReferenceValue.GetType().Name.Replace("Payload", "")
                                            );
                                            payload.isExpanded =
                                                EditorGUILayout.Foldout(payload.isExpanded, className);

                                            GUILayout.FlexibleSpace();
                                            if (GUILayout.Button("X", GUILayout.Width(20)))
                                            {
                                                payloads.DeleteArrayElementAtIndex(j);
                                                serializedBehaviour.ApplyModifiedProperties();
                                                GUIUtility.ExitGUI();
                                            }
                                        }
                                        EditorGUILayout.EndHorizontal();
                                        // ---------------------------------------------------------------------------------
                                    }
                                    EditorGUILayout.EndVertical();
                                    // ---------------------------------------------------------------------------------

                                    if (payload.isExpanded)
                                    {
                                        EditorGUI.indentLevel++;
                                        {
                                            object boxed = payload.managedReferenceValue;

                                            if (boxed == null)
                                            {
                                                EditorGUILayout.HelpBox("Payload is null", MessageType.Warning);
                                            }
                                            else
                                            {
                                                Type payloadType = boxed.GetType();

                                                FieldInfo[] fields = payloadType.GetFields(
                                                    BindingFlags.Instance | BindingFlags.Public |
                                                    BindingFlags.NonPublic);

                                                foreach (FieldInfo field in fields)
                                                {
                                                    bool isPublicSerialized =
                                                        field.IsPublic && !Attribute.IsDefined(field,
                                                            typeof(NonSerializedAttribute), true);
                                                    bool hasSerializeField =
                                                        Attribute.IsDefined(field, typeof(SerializeField), true);

                                                    if (!isPublicSerialized && !hasSerializeField)
                                                        continue;

                                                    SerializedProperty fieldProp =
                                                        payload.FindPropertyRelative(field.Name);
                                                    if (fieldProp == null)
                                                        continue;

                                                    string niceName = ObjectNames.NicifyVariableName(field.Name);
                                                    EditorGUILayout.PropertyField(fieldProp, new GUIContent(niceName),
                                                        true);
                                                }
                                            }
                                        }
                                        EditorGUI.indentLevel--;
                                    }
                                }
                            }

                            EditorGUI.indentLevel--;
                        }
                    }

                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
}