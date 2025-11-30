using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Transform))]
public class SimpleEditorTest : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();


        EditorGUILayout.BeginVertical("box");
        {
            EditorGUILayout.LabelField("This is a box", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("And this is inside the box");
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }
}
