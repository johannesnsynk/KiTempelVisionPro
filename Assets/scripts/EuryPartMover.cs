using UnityEngine;

public class EuryPartMover : MonoBehaviour
{
    private Vector3 _centerPoint;
    private Vector3 _orbitAxis;
    private float _orbitRadius;
    private float _orbitSpeed;
    private float _driftSpeed;
    private Vector3 _driftDir;
    private float _fadeDelay;
    private float _fadeDuration;
    private float _timer;
    private Material _mat;
    private float _startAlpha;
    private float _orbitAngle;

    public void Init(Vector3 centerPoint, Vector3 driftDir, float driftSpeed,
                     Vector3 orbitAxis, float orbitSpeed,
                     float fadeDelay, float fadeDuration)
    {
        _centerPoint = centerPoint;
        _driftDir = driftDir;
        _driftSpeed = driftSpeed;
        _orbitAxis = orbitAxis;
        _orbitSpeed = orbitSpeed;
        _fadeDelay = fadeDelay;
        _fadeDuration = fadeDuration;

        _orbitRadius = Vector3.Distance(transform.position, centerPoint);
        _orbitAngle = 0f;

        var mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            _mat = mr.material;
            _startAlpha = _mat.HasProperty("_Color") ? _mat.color.a : 1f;
        }
    }

    void Update()
    {
        _timer += Time.deltaTime;

        // Phase 1: um Mitte rotieren, Radius wächst langsam
        float driftFactor = Mathf.Clamp01(_timer / _fadeDelay);
        _orbitRadius += _driftSpeed * driftFactor * Time.deltaTime;

        // Orbit um Zentrum
        _orbitAngle += _orbitSpeed * Time.deltaTime;
        Vector3 toObj = transform.position - _centerPoint;
        if (toObj.magnitude < 0.001f) toObj = Vector3.right;
        Quaternion rot = Quaternion.AngleAxis(_orbitSpeed * Time.deltaTime, _orbitAxis);
        Vector3 newOffset = rot * toObj.normalized * _orbitRadius;
        transform.position = _centerPoint + newOffset;

        // Eigene Rotation
        transform.Rotate(_orbitAxis, _orbitSpeed * 0.5f * Time.deltaTime, Space.World);

        // Sanft nach unten driften
        _centerPoint.y -= 0.02f * driftFactor * Time.deltaTime;

        // Fade
        if (_timer >= _fadeDelay && _mat != null && _mat.HasProperty("_Color"))
        {
            float t = (_timer - _fadeDelay) / _fadeDuration;
            Color c = _mat.color;
            c.a = Mathf.Clamp01(1f - t) * _startAlpha;
            _mat.color = c;

            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}