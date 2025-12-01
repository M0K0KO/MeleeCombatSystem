using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AnimationEventStateBehaviour))]
public class AnimationEventStateBehaviourEditor : Editor
{
    private SerializedProperty eventsProp;

    private void OnEnable()
    {
        if (target == null) return;
        eventsProp = serializedObject.FindProperty("normalizedTimeEvents");
    }

    public override void OnInspectorGUI()
    {
        if (target == null)
        {
            EditorGUILayout.HelpBox("No target.", MessageType.Info);
            return;
        }

        serializedObject.Update();

        EditorGUILayout.LabelField("Animation Event State", EditorStyles.boldLabel);

        if (eventsProp != null)
        {
            EditorGUILayout.LabelField($"Events ({eventsProp.arraySize})");

            EditorGUI.indentLevel++;

            // 요약 라인 표시
            for (int i = 0; i < eventsProp.arraySize; i++)
            {
                SerializedProperty evt = eventsProp.GetArrayElementAtIndex(i);

                SerializedProperty timeProp = evt.FindPropertyRelative("time");
                SerializedProperty nameProp = evt.FindPropertyRelative("name");

                float time = timeProp.floatValue;
                string name = nameProp.stringValue;

                EditorGUILayout.LabelField($"• {time:F2}   {name}");
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Detailed editing is done in the Animation Event Editor window.",
            MessageType.Info);

        if (GUILayout.Button("Open Animation Event Editor"))
        {
            AnimationEventEditorWindow.Open();
        }

        serializedObject.ApplyModifiedProperties();
    }
}