#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using NSYNK.HyperSlides.Runtime;

namespace NSYNK.HyperSlides.EditorScripts
{
    /// <summary>
    /// This class is used to manage the addressable scenes in the editor.
    /// It allows you to find all addressable scenes in the project and instantiate them in the scene.
    /// It also allows you to hide the instantiated addressables in the scene.
    /// </summary>
    [ExecuteInEditMode]
    public class AddressableSceneUI : MonoBehaviour
    {
        public List<XRSlideElement> slideElements = new List<XRSlideElement>();
        private List<GameObject> instantiatedObjects = new List<GameObject>();

        /// <summary>
        /// This method is used to find all addressable scenes in the project.
        /// It uses the Resources.FindObjectsOfTypeAll method to find all instances of the XRSlideElement class.
        /// It then checks if the asset reference is valid and adds it to the list of slide elements.
        /// Finally, it instantiates the addressable scenes in the scene and sets their parent to the root content.
        /// </summary>
        public void GetAddressables()
        {
            object[] newArray = Resources.FindObjectsOfTypeAll(typeof(XRSlideElement));

            for (int i = 0; i < newArray.Length; i++)
            {
                if (((XRSlideElement)newArray[i]).ValidAsset())
                    slideElements.Add(newArray[i] as XRSlideElement);
            }

            if (slideElements.Count > 0)
            {
                slideElements.ForEach(s =>
                {
                    GameObject scenePrefab = PrefabUtility.InstantiatePrefab(s.assetReference.editorAsset) as GameObject;
                    scenePrefab.transform.SetParent(s.rootContent.transform, false);
                    instantiatedObjects.Add(scenePrefab);
                });
            }
        }

        /// <summary>
        /// This method is used to hide the instantiated addressables in the scene.
        /// It destroys all instantiated objects and clears the list of instantiated objects and slide elements.
        /// </summary>
        public void HideAddressables()
        {
            instantiatedObjects.ForEach(go =>
            {
                DestroyImmediate(go);
            });

            instantiatedObjects.Clear();
            slideElements.Clear();
        }
    }
}
#endif