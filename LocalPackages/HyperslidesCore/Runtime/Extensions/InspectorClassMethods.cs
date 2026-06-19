using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NSYNK.HyperSlides.Network;
using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// Custom class that reflects all XRSlideManager methods, so we do not need to use prefab => scene relations
    /// </summary>
    public class InspectorClassMethods : MonoBehaviour
    {
        public UnityEngine.Object currentClass = new();
        public string currentClassName = "";
        public string currentMethod = "";

        private MonoBehaviour classRuntimeObject = null;

        private void OnEnable()
        {
            CheckOnRuntimeObject();
        }

        /// <summary>
        /// Calls the method without parameters
        /// </summary>
        public void CallMethod()
        {
            CheckOnRuntimeObject();

            if (classRuntimeObject)
            {
                Debug.Log(currentMethod, classRuntimeObject);
                classRuntimeObject.SendMessage(currentMethod);
            }
        }

        /// <summary>
        /// Calls the method with an object parameter
        /// </summary>
        /// <param name="obj"></param>
        public void CallMethod(UnityEngine.Object obj = null)
        {
            CheckOnRuntimeObject();

            if (classRuntimeObject)
            {
                Debug.Log(currentMethod + " => " + obj, classRuntimeObject);
                classRuntimeObject.SendMessage(currentMethod, obj);
            }
        }

        /// <summary>
        /// Checks if the runtime object is assigned, if not it tries to find it in the scene
        /// </summary>
        private void CheckOnRuntimeObject()
        {
            if (classRuntimeObject == null)
            {
                Type type = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(currentClassName);
                    if (type != null)
                        break;
                }

                if (type != null)
                {
                    classRuntimeObject = FindAnyObjectByType(type.BaseType) as MonoBehaviour;

                    if (classRuntimeObject == null)
                        Debug.LogWarning("InspectorClassMethods: Could not find an instance of " + currentClassName + " in the scene.");
                }
                else
                {
                    Debug.LogWarning("InspectorClassMethods: Could not find type " + currentClassName + " in the assemblies.");
                }
            }
        }
    }
}