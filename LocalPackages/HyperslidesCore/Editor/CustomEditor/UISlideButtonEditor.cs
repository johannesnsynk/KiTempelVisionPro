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
    [CustomEditor(typeof(UISlideButton), true)]
    public class UISlideButtonEditor : UIButtonEditor
    {
        SerializedProperty thumbnailImage, activeSlideIndicator, sphereMeshRenderer;

        protected override void OnEnable()
        {
            base.OnEnable();

            thumbnailImage = serializedObject.FindProperty("thumbnailImage");
            activeSlideIndicator = serializedObject.FindProperty("activeSlideIndicator");
            sphereMeshRenderer = serializedObject.FindProperty("sphereMeshRenderer");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            base.OnInspectorGUI();

            EditorGUILayout.PropertyField(thumbnailImage);
            EditorGUILayout.PropertyField(activeSlideIndicator);
            EditorGUILayout.PropertyField(sphereMeshRenderer);

            serializedObject.ApplyModifiedProperties();
        }
    }
}