using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance;

    public virtual void Awake()
    {
        if (Instance != null)
        {
            string typename = typeof(T).Name;
            Debug.LogWarning($"More that one instance of {typename} found.");
        }
        Instance = this as T;
    }
}