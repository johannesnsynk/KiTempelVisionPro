using UnityEngine;
using UnityEditor;
using System.IO;

public class FixArmatureHierarchy : EditorWindow
{
    [MenuItem("Tools/Fix Armature Hierarchy")]
    static void FixHierarchy()
    {
        GameObject selectedObject = Selection.activeGameObject;
        
        if (selectedObject == null)
        {
            Debug.LogError("Please select your character prefab in the scene");
            return;
        }

        // Find the Armature child
        Transform armature = selectedObject.transform.Find("Armature");
        
        if (armature == null)
        {
            Debug.LogError("No Armature found as child");
            return;
        }

        // Find mixamorig:Hips under Armature
        Transform hips = armature.Find("mixamorig:Hips");
        
        if (hips == null)
        {
            Debug.LogError("No mixamorig:Hips found under Armature");
            return;
        }

        // Reparent Hips directly under the root
        hips.SetParent(selectedObject.transform);
        
        // Move mesh to be under Hips instead
        SkinnedMeshRenderer mesh = armature.GetComponentInChildren<SkinnedMeshRenderer>();
        if (mesh != null)
        {
            mesh.transform.SetParent(selectedObject.transform);
        }

        // Delete the now-empty Armature
        DestroyImmediate(armature.gameObject);
        
        Debug.Log("Fixed! Hips is now directly under root.");
    }
}