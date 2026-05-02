using UnityEngine;

public class CubeAnimator : MonoBehaviour
{
    public string boolName;

    public void UpdateBoolValue(bool activate)
    {
        if (!string.IsNullOrEmpty(boolName))
            GetComponent<Animator>().SetBool(boolName, activate);
    }
}
