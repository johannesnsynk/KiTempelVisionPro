using UnityEngine;

public class PickupObject : MonoBehaviour
{
    [SerializeField] private Transform grabTriggerSource; // die Hand / Collider-Quelle
    [SerializeField] private Transform grabPoint;         // Zielposition
    private bool grabbed = false;

    private void OnTriggerEnter(Collider other)
    {
        if (grabbed) return;
        if (other.transform == grabTriggerSource || 
            other.transform.IsChildOf(grabTriggerSource))
        {
            grabbed = true;
            transform.SetParent(grabPoint);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
    }
}