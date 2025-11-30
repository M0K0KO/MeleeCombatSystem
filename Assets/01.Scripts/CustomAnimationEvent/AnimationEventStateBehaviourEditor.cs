using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.Animations;
using UnityEngine;

[CustomEditor(typeof(AnimationEventStateBehaviour))]
public class AnimationEventStateBehaviourEditor : Editor
{
    bool Validate(AnimationEventStateBehaviour stateBehaviour, out string errorMessage)
    {
        AnimatorController animatorController = GetValidAnimatorController(out errorMessage);
        if (animatorController == null) return false;

        ChildAnimatorState matchingState = animatorController.layers.SelectMany(layer => layer.stateMachine.states)
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
    
    private AnimationClip previewClip;
    private float globalPreviewNormalizedTime = 0f;
    private bool isPreviewing;

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
        if (previewClip == null || !isPreviewing) return;

        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();

        GameObject go = Selection.activeGameObject;
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

        SceneView.RepaintAll();
    }


    private SerializedProperty eventsProp;
    
    private void OnEnable()
    {
        eventsProp = serializedObject.FindProperty("events");
    }

    public override void OnInspectorGUI()
    {
        #region AnimationPreview
        AnimationEventStateBehaviour stateBehaviour = (AnimationEventStateBehaviour)target;
        if (Validate(stateBehaviour, out string errorMessage))
        {
            GUILayout.Space(10);
            
            if (isPreviewing)
            {
                globalPreviewNormalizedTime = EditorGUILayout.Slider("Normalized Time", globalPreviewNormalizedTime, 0f, 1f);
                
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
            else if (GUILayout.Button("Preview"))
            {
                isPreviewing = true;
            }
            
            EditorGUI.BeginChangeCheck();
            
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll(); 
            }

            GUILayout.Label($"Previewing at {globalPreviewNormalizedTime:F2}s", EditorStyles.helpBox);
        }
        else
        {
            EditorGUILayout.HelpBox(errorMessage, MessageType.Info);
        }
        
        serializedObject.Update();
        #endregion
        
        #region Events
        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Expand All"))
            {
                for (int i = 0; i < eventsProp.arraySize; i++)
                {
                    SerializedProperty evt = eventsProp.GetArrayElementAtIndex(i);
                    evt.isExpanded = true;
                }
            }

            if (GUILayout.Button("Collapse All"))
            {
                for (int i = 0; i < eventsProp.arraySize; i++)
                {
                    SerializedProperty evt = eventsProp.GetArrayElementAtIndex(i);
                    evt.isExpanded = false;
                }
            }

            if (GUILayout.Button("Sort"))
            {
                Undo.RecordObject(target, "Sorting events");
                
                var beh = (AnimationEventStateBehaviour)target;
                beh.events.Sort((a, b) => a.time.CompareTo(b.time));
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("+ Add Event"))
            {
                eventsProp.arraySize++;
            }
        }
        EditorGUILayout.EndHorizontal();
        // ---------------------------------------------------------------------------------

        // ---------------------------------------------------------------------------------
        EditorGUILayout.BeginVertical("box");
        {
            for (int i = 0; i < eventsProp.arraySize; i++)
            {
                SerializedProperty evt = eventsProp.GetArrayElementAtIndex(i);
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
                        if (GUILayout.Button("Toggle Fold"))
                        {
                            evt.isExpanded = !evt.isExpanded;
                        }
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            eventsProp.DeleteArrayElementAtIndex(i);
                            break;
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
                                                    EditorGUILayout.Foldout(payload.isExpanded, className, true);

                                                GUILayout.FlexibleSpace();
                                                if (GUILayout.Button("X", GUILayout.Width(20)))
                                                {
                                                    payloads.DeleteArrayElementAtIndex(j);
                                                    break;
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
                                                SerializedProperty copy = payload.Copy();
                                                SerializedProperty end = payload.GetEndProperty();

                                                bool enterChildren = true;

                                                while (copy.NextVisible(enterChildren) &&
                                                       !SerializedProperty.EqualContents(copy, end))
                                                {
                                                    Debug.Log("Fucked");
                                                    enterChildren = false;
                                                    EditorGUILayout.PropertyField(copy, true);
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
        
        serializedObject.ApplyModifiedProperties();
        #endregion
    }

}