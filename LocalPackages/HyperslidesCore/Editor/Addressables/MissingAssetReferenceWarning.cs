using UnityEngine;
using UnityEditor;
using NSYNK.HyperSlides.Runtime;

namespace NSYNK.HyperSlides.EditorScripts
{
    /// <summary>
    /// This class is used to display a warning icon in the hierarchy window for GameObjects
    /// that have an invalid asset reference in the XRSlideElement component.
    /// </summary>
    [InitializeOnLoad]
    public class MissingAssetReferenceWarning
    {
        static MissingAssetReferenceWarning()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            GameObject obj = EditorUtility.EntityIdToObject(instanceID) as GameObject;

            if (obj != null && obj.TryGetComponent(out XRSlideElement slideElement))
            {
                if (!slideElement.assetIsValid)
                {
                    Rect iconRect = new Rect(selectionRect.xMax - 20, selectionRect.y, 16, 16);
                    GUI.Label(iconRect, EditorGUIUtility.IconContent("console.warnicon"));
                }
            }
        }
    }
}