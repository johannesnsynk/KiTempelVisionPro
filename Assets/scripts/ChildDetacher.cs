using UnityEngine;

[HelpURL("https://docs.unity3d.com/ScriptReference/Transform.SetParent.html")]
public class ChildDetacher : MonoBehaviour
{
    [Header("Detachment Settings")]
    [Tooltip("The child object to detach. Drag from hierarchy.")]
    public GameObject childToDetach;

    [Tooltip("Should the object use physics after detachment?")]
    public bool enablePhysics = true;

    [Tooltip("Maintain world position/rotation when detaching?")]
    public bool maintainWorldTransform = true;

    [Tooltip("Copy parent's velocity to child on detachment?")]
    public bool inheritParentVelocity = true;

    [Header("Post-Detachment Options")]
    [Tooltip("Optional force to apply after detachment")]
    public Vector3 initialForce = Vector3.zero;

    [Tooltip("Force application mode")]
    public ForceMode forceMode = ForceMode.Impulse;

    /// <summary>
    /// Detaches the configured child object with physics settings
    /// </summary>
    public void DetachChild()
    {
        if (childToDetach == null)
        {
            Debug.LogError("No child object assigned to detach!", this);
            return;
        }

        Detach(childToDetach);
    }

    /// <summary>
    /// Detaches a specific child object (alternative to configured child)
    /// </summary>
    /// <param name="specificChild">The child to detach</param>
    public void Detach(GameObject specificChild)
    {
        if (specificChild == null) return;

        // Handle Rigidbody if exists
        Rigidbody rb = specificChild.GetComponent<Rigidbody>();
        if (rb != null && enablePhysics)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            // Inherit parent velocity if requested
            if (inheritParentVelocity && transform.parent != null)
            {
                Rigidbody parentRb = transform.parent.GetComponent<Rigidbody>();
                if (parentRb != null)
                {
                    rb.linearVelocity = parentRb.linearVelocity;
                    rb.angularVelocity = parentRb.angularVelocity;
                }
            }

            // Apply initial force if configured
            if (initialForce != Vector3.zero)
            {
                rb.AddForce(initialForce, forceMode);
            }
        }

        // Perform detachment
        specificChild.transform.SetParent(null, maintainWorldTransform);

        Debug.Log($"Detached {specificChild.name}", specificChild);
    }

    /// <summary>
    /// Detaches all children with optional physics
    /// </summary>
    public void DetachAllChildren()
    {
        foreach (Transform child in transform)
        {
            Detach(child.gameObject);
        }
    }
}