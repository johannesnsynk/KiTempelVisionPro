using NSYNK.HyperSlides.Network;
using UnityEditor;

namespace NSYNK.HyperSlides.EditorScripts
{
    /// <summary>
    /// This class is used to create a custom editor for the NetworkSynced class.
    /// </summary>
    [CustomEditor(typeof(NetworkSynced), true)]
    public class NetworkSyncedEditor : Editor
    {
        SerializedProperty guid, grabbedObject, lookAtUserPosition, restrictAccessTo, syncType, syncedComponent, syncedValueEvent, syncedTransformEvent;

        protected void OnEnable()
        {
            guid = serializedObject.FindProperty("guid");
            grabbedObject = serializedObject.FindProperty("grabbedObject");
            lookAtUserPosition = serializedObject.FindProperty("lookAtUserPosition");
            restrictAccessTo = serializedObject.FindProperty("restrictAccessTo");
            syncType = serializedObject.FindProperty("syncType");
            syncedComponent = serializedObject.FindProperty("syncedComponent");
            syncedValueEvent = serializedObject.FindProperty("syncedValueEvent");
            syncedTransformEvent = serializedObject.FindProperty("syncedTransformEvent");
        }

        public override void OnInspectorGUI()
        {
            NetworkSynced sync = target as NetworkSynced;

            serializedObject.Update();

            EditorGUILayout.PropertyField(guid);

            if(lookAtUserPosition != null)
                EditorGUILayout.PropertyField(lookAtUserPosition);

            if (grabbedObject != null)
                EditorGUILayout.PropertyField(grabbedObject);
                
            EditorGUILayout.PropertyField(restrictAccessTo);
            EditorGUILayout.PropertyField(syncType);

            if (sync.syncType == NetworkSynced.SyncType.Value)
            {
                EditorGUILayout.PropertyField(syncedComponent);
                EditorGUILayout.PropertyField(syncedValueEvent);
            }
            else
            {
                EditorGUILayout.PropertyField(syncedTransformEvent);
            }

            serializedObject.ApplyModifiedProperties();

            //base.OnInspectorGUI();
        }
    }
}