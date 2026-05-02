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
            string assemblyName = currentClassName + ", NSYNK.Hyperslides.Runtime";
            Type type = Type.GetType(assemblyName);

            if (type != null)
            {
                FieldInfo fieldInfo = type.BaseType.GetField("Instance");

                if(fieldInfo != null)
                    classRuntimeObject = (fieldInfo.GetValue(currentClass) as MonoBehaviour);
            }
        }

        public void CallMethod()
        {
            if (classRuntimeObject)
            {
                Debug.Log(currentMethod, classRuntimeObject);
                classRuntimeObject.SendMessage(currentMethod);
            }
        }

        public void CallMethod(UnityEngine.Object obj = null)
        {
            if (classRuntimeObject)
            {
                Debug.Log(currentMethod + " => " + obj, classRuntimeObject);
                classRuntimeObject.SendMessage(currentMethod, obj);
            }
        }
    }
}