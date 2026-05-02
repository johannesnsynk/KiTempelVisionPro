using UnityEditor;
using UnityEngine;

namespace NSYNK.HyperSlides.EditorScripts
{
    /// <summary>
    /// This class is used to create a custom editor for the AddressableSceneUI class.
    /// It allows you to show and hide addressable scenes in the editor.
    /// </summary>
    [CustomEditor(typeof(AddressableSceneUI))]
    public class AddressablesTrigger : Editor
    {
        public override void OnInspectorGUI()
        {
            // Get the chosen GameObject
            AddressableSceneUI t = target as AddressableSceneUI;

            if (t == null || t.slideElements == null)
                return;

            EditorGUILayout.BeginVertical();

            if (t.slideElements.Count == 0 && GUILayout.Button("Show addressable"))
            {
                t.GetAddressables();
            }

            if (t.slideElements.Count > 0 && GUILayout.Button("Hide addressable"))
            {
                t.HideAddressables();
            }

            EditorGUILayout.EndVertical();
        }
    }
}