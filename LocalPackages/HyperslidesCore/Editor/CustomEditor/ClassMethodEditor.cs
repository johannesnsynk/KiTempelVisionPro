using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NSYNK.HyperSlides.EditorScripts
{
    /// <summary>
    /// This class is used to create a custom editor for the ClassMethod class.
    /// It allows you to select a class and a method from the class to be called.
    /// Important note: This class relies on an instance of the class being present in the scene.
    /// </summary>
    public class ClassMethodEditor : Editor
    {
        /// <summary>
        /// This is the object that you want to reflect.
        /// </summary>
        public Object selectedClass;
        /// <summary>
        /// This is a flag to reassign the method to be called.
        /// </summary>
        public bool reassignMethod = false;
        /// <summary>
        /// This is the index of the selected method in the list of all methods.
        /// It is hidden from the inspector.
        /// </summary>
        public int selected = 0;

        /// <summary>
        /// This is a list of all methods in the selected class.
        /// </summary>
        readonly List<string> allMethods = new();

        SerializedProperty currentClassName;
        SerializedProperty currentClass;
        SerializedProperty currentMethod;

        private void OnEnable()
        {
            currentClassName = serializedObject.FindProperty("currentClassName");
            currentClass = serializedObject.FindProperty("currentClass");
            currentMethod = serializedObject.FindProperty("currentMethod");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (currentClass != null)
                selectedClass = currentClass.objectReferenceValue;

            selectedClass = EditorGUILayout.ObjectField("Reflected class", selectedClass, typeof(Object), true);

            if (!reassignMethod)
                EditorGUILayout.LabelField("Calling method", currentMethod.stringValue);

            if (selectedClass && (string.IsNullOrEmpty(currentMethod.stringValue) || reassignMethod))
            {
                allMethods.Clear();

                var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public;
                (selectedClass as MonoScript).GetClass().GetMethods(flags).ToList().ForEach(m => allMethods.Add(m.Name));

                string foundMethod = allMethods.Find(m => m == currentMethod.stringValue);

                selected = string.IsNullOrEmpty(foundMethod) ? 0 : allMethods.IndexOf(foundMethod);
                selected = EditorGUILayout.Popup("Method to be called", selected, allMethods.ToArray());

                if (allMethods.Count > 0)
                    currentMethod.stringValue = allMethods[selected];

                currentClass.objectReferenceValue = selectedClass;
                currentClassName.stringValue = ((MonoScript)selectedClass).GetClass().ToString();
            }
            else if (GUILayout.Button("Reassign method"))
                reassignMethod = !reassignMethod;

            serializedObject.ApplyModifiedProperties();
        }
    }
}