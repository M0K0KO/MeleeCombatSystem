using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemListTest))]
public class ItemListTestEditor : Editor
{
    private SerializedProperty itemsProp;

    private void OnEnable()
    {
        itemsProp = serializedObject.FindProperty("items");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.LabelField("My Items", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Item"))
            {
                itemsProp.arraySize++;
            }
        }
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < itemsProp.arraySize; i++)
        {
            SerializedProperty elem = itemsProp.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.BeginHorizontal();
                {
                    elem.isExpanded = EditorGUILayout.Foldout(elem.isExpanded, "Item " + i);

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        itemsProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (elem.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    {
                        SerializedProperty copy = elem.Copy();
                        SerializedProperty end = elem.GetEndProperty();

                        bool enter = true;
                        while (copy.NextVisible(enter) && !SerializedProperty.EqualContents(copy, end))
                        {
                            enter = false;
                            EditorGUILayout.PropertyField(copy, true);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
        }



        serializedObject.ApplyModifiedProperties();
    }
}