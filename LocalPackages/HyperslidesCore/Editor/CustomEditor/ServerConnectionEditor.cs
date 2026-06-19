using System.IO;
using NSYNK.HyperSlides.Runtime;
using UnityEditor;
using UnityEngine;

namespace NSYNK.HyperSlides.EditorScripts
{
    /// <summary>
    /// Custom editor for the ServerConnection ScriptableObject to add a button that increments a counter.
    /// </summary>
    [CustomEditor(typeof(ServerConnection))]
    public class ServerConnectionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();

            // Reference to the target ScriptableObject
            ServerConnection mySO = (ServerConnection)target;

            // Add a button
            if (GUILayout.Button("Export to JSON"))
            {
                Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(mySO), this);

                string defaultName = mySO.name;
                string extension = "json";
                string path = EditorUtility.SaveFilePanel("Save Your File", "", defaultName, extension);

                if (!string.IsNullOrEmpty(path))
                {
                    File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(mySO));
                    Debug.Log("File saved to: " + path);
                }
                else
                {
                    Debug.Log("Save canceled.");
                }
            }
        }
    }
}