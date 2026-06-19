using UnityEngine;

namespace NSYNK
{
    public class OneEuroFilter : IFilter
    {
        private float _freq;
        private float _mincutoff;
        private float _beta;
        private float _dcutoff;
        private LowPassFilterVector3 _x;
        private LowPassFilterVector3 _dx;
        private float _lasttime;
        private bool _isLastTimeUndefined = true;

        public OneEuroFilter(float freq, float mincutoff = 1.0f, float beta = 0.0f, float dcutoff = 1.0f)
        {
            _freq = freq;
            _mincutoff = mincutoff;
            _beta = beta;
            _dcutoff = dcutoff;
            _x = new LowPassFilterVector3(Alpha(mincutoff));
            _dx = new LowPassFilterVector3(Alpha(dcutoff));
        }

        public void UpdateParams(float freq, float mincutoff = 1.0f, float beta = 0.0f, float dcutoff = 1.0f)
        {
            _freq = freq;
            _mincutoff = mincutoff;
            _beta = beta;
            _dcutoff = dcutoff;
        }

        private float Alpha(float cutoff)
        {
            float te = 1.0f / _freq;
            float tau = 1.0f / (2.0f * Mathf.PI * cutoff);
            return 1.0f / (1.0f + tau / te);
        }

        public Vector3 Update(Vector3 value, float timestamp = -1f)
        {
            if (timestamp < 0) timestamp = Time.time;

            if (!_isLastTimeUndefined && timestamp > _lasttime)
            {
                _freq = 1.0f / (timestamp - _lasttime);
            }
            _lasttime = timestamp;
            _isLastTimeUndefined = false;

            // Estimate the current variation per second
            Vector3 dvalue = Vector3.zero;
            if (_x.HasLastRawValue())
            {
                // JS: (value - this.x.lastFilteredValue())*this.freq
                dvalue = (value - _x.LastFilteredValue()) * _freq;
            }

            // Filter the derivative
            Vector3 edvalue = _dx.Filter(dvalue, Alpha(_dcutoff));

            // Use it to update the cutoff frequency
            // For Vector3, we use the magnitude of the derivative
            float cutoff = _mincutoff + _beta * edvalue.magnitude;

            // Filter the given value
            return _x.Filter(value, Alpha(cutoff));
        }

        public void Reset()
        {
            _x.Reset();
            _dx.Reset();
            _isLastTimeUndefined = true;
        }
    }

    public class OneEuroFilterQuaternion : IFilter
    {
        private float _freq;
        private float _mincutoff;
        private float _beta;
        private float _dcutoff;
        private Quaternion _x; // Last filtered value
        private bool _initialized = false;

        private LowPassFilter _dx; // Derivative filter (scalar)
        
        private float _lasttime;
        private bool _isLastTimeUndefined = true;

        public OneEuroFilterQuaternion(float freq, float mincutoff = 1.0f, float beta = 0.0f, float dcutoff = 1.0f)
        {
            _freq = freq;
            _mincutoff = mincutoff;
            _beta = beta;
            _dcutoff = dcutoff;
            _dx = new LowPassFilter(Alpha(dcutoff));
            _x = Quaternion.identity;
        }

        public void UpdateParams(float freq, float mincutoff = 1.0f, float beta = 0.0f, float dcutoff = 1.0f)
        {
            _freq = freq;
            _mincutoff = mincutoff;
            _beta = beta;
            _dcutoff = dcutoff;
        }

        private float Alpha(float cutoff)
        {
            float te = 1.0f / _freq;
            float tau = 1.0f / (2.0f * Mathf.PI * cutoff);
            return 1.0f / (1.0f + tau / te);
        }

        public Quaternion Update(Quaternion value, float timestamp = -1f)
        {
            if (timestamp < 0) timestamp = Time.time;

            if (!_isLastTimeUndefined && timestamp > _lasttime)
            {
                _freq = 1.0f / (timestamp - _lasttime);
            }
            _lasttime = timestamp;
            _isLastTimeUndefined = false;

            // Estimate the current variation per second
            float dvalue = 0.0f;
            if (_initialized)
            {
                // Calculate angular difference in degrees
                float angle = Quaternion.Angle(_x, value);
                dvalue = angle * _freq;
            }

            // Filter the derivative
            float edvalue = _dx.Filter(dvalue, Alpha(_dcutoff));

            // Update cutoff
            float cutoff = _mincutoff + _beta * Mathf.Abs(edvalue);

            // Calculate alpha
            float alpha = Alpha(cutoff);

            if (!_initialized)
            {
                _x = value;
                _initialized = true;
                return _x;
            }

            // Filter the value using Slerp
            _x = Quaternion.Slerp(_x, value, alpha);
            return _x;
        }

        public void Reset()
        {
            _initialized = false;
            _dx.Reset();
            _isLastTimeUndefined = true;
            _x = Quaternion.identity;
        }
    }

    internal class LowPassFilter
    {
        private float _y, _s;
        private float _a;
        private bool _initialized;

        public LowPassFilter(float alpha, float initval = 0.0f)
        {
            _y = _s = initval;
            SetAlpha(alpha);
            _initialized = false;
        }

        public void SetAlpha(float alpha)
        {
            if (alpha <= 0.0f) alpha = 0.0f; // Allow 0? JS warns if <=0.
            if (alpha > 1.0f) alpha = 1.0f;
            _a = alpha;
        }

        public float Filter(float value, float alpha)
        {
            SetAlpha(alpha);
            return Filter(value);
        }

        public float Filter(float value)
        {
            float result;
            if (_initialized)
                result = _a * value + (1.0f - _a) * _s;
            else
            {
                result = value;
                _initialized = true;
            }
            _y = value;
            _s = result;
            return result;
        }

        public float LastFilteredValue() => _s;
        public bool HasLastRawValue() => _initialized;
        public void Reset() => _initialized = false;
    }

    internal class LowPassFilterVector3
    {
        private Vector3 _y, _s;
        private float _a;
        private bool _initialized;

        public LowPassFilterVector3(float alpha, Vector3 initval = default)
        {
            _y = _s = initval;
            SetAlpha(alpha);
            _initialized = false;
        }

        public void SetAlpha(float alpha)
        {
            if (alpha <= 0.0f) alpha = 0.0f;
            if (alpha > 1.0f) alpha = 1.0f;
            _a = alpha;
        }

        public Vector3 Filter(Vector3 value, float alpha)
        {
            SetAlpha(alpha);
            return Filter(value);
        }

        public Vector3 Filter(Vector3 value)
        {
            Vector3 result;
            if (_initialized)
                result = Vector3.Lerp(_s, value, _a);
            else
            {
                result = value;
                _initialized = true;
            }
            _y = value;
            _s = result;
            return result;
        }

        public Vector3 LastFilteredValue() => _s;
        public bool HasLastRawValue() => _initialized;
        public void Reset() => _initialized = false;
    }
}
