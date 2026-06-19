using UnityEngine;

/// <summary>
/// A generic singleton base class for MonoBehaviour-derived classes.
/// </summary>
/// <typeparam name="T"></typeparam>
[DefaultExecutionOrder(-1000)] // Ensure singletons initialize early
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<T>(FindObjectsInactive.Include);

            return _instance;
        }
    }

    protected void Awake()
    {
        if (_instance == null)
            _instance = this as T;

        OnSingletonAwake();
    }

    // Derived classes override this instead of Awake to add their logic.
    protected virtual void OnSingletonAwake() { }
}