using NSYNK.HyperSlides.UI;
using UnityEditor;
using UnityEditor.UI;

namespace NSYNK.HyperSlides.EditorScripts
{
    /// <summary>
    /// This class is used to create a custom editor for the UIButton class.
    /// It allows you to extend the default functionality of the Button class while adding
    /// additional features like confirmation prompts and global timeouts.
    /// </summary>
    [CustomEditor(typeof(UIButton), true)]
    public class UIButtonEditor : ButtonEditor
    {
        SerializedProperty needsConfirmation, confirmationPrompt, useGlobalTimeout, trackingMaterial, canBeToggled, toggleOnColor, toggleOffColor;

        protected override void OnEnable()
        {
            base.OnEnable();

            useGlobalTimeout = serializedObject.FindProperty("useGlobalTimeout");
            needsConfirmation = serializedObject.FindProperty("needsConfirmation");
            confirmationPrompt = serializedObject.FindProperty("confirmationPrompt");
            trackingMaterial = serializedObject.FindProperty("trackingMaterial");
            canBeToggled = serializedObject.FindProperty("canBeToggled");
            toggleOnColor = serializedObject.FindProperty("toggleOnColor");
            toggleOffColor = serializedObject.FindProperty("toggleOffColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            base.OnInspectorGUI();

            EditorGUILayout.PropertyField(useGlobalTimeout);
            EditorGUILayout.PropertyField(needsConfirmation);

            if (needsConfirmation.boolValue == true)
                EditorGUILayout.PropertyField(confirmationPrompt);

            EditorGUILayout.PropertyField(canBeToggled);

            if (trackingMaterial != null)
                EditorGUILayout.PropertyField(trackingMaterial);

            if (canBeToggled.boolValue)
            {
                EditorGUILayout.PropertyField(toggleOnColor);
                EditorGUILayout.PropertyField(toggleOffColor);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}