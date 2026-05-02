using UnityEngine;
using System.Collections;

namespace NSYNK
{
    public class KalmanFilter
    {
        private float _q; // Process noise covariance
        private float _r; // Measurement noise covariance
        private Vector3 _x; // Estimated value
        private Vector3 _p; // Estimated error covariance
        private Vector3 _k; // Kalman gain

        public KalmanFilter(float q, float r, Vector3 initialEstimate, Vector3 initialErrorCovariance)
        {
            _q = q;
            _r = r;
            _x = initialEstimate;
            _p = initialErrorCovariance;
        }

        public Vector3 Update(Vector3 measurement)
        {
            // Predict
            _p += new Vector3(_q, _q, _q);

            // Update
            _k = ElementWiseDivision(_p, _p + new Vector3(_r, _r, _r));
            _x = _x + ElementWiseMultiplication(_k, measurement - _x);
            _p = ElementWiseMultiplication(new Vector3(1, 1, 1) - _k, _p);

            return _x;
        }

        private Vector3 ElementWiseDivision(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        }
        private Vector3 ElementWiseMultiplication(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        }
    }

    public class QuaternionKalmanFilter
    {
        private float _q; // Process noise covariance
        private float _r; // Measurement noise covariance
        private Quaternion _x; // Estimated state (quaternion)
        private float _p; // Estimated error covariance
        private float _k; // Kalman gain

        public QuaternionKalmanFilter(float q, float r, Quaternion initialEstimate, float initialErrorCovariance)
        {
            _q = q;
            _r = r;
            _x = initialEstimate;
            _p = initialErrorCovariance;
        }

        public Quaternion Update(Quaternion measurement)
        {
            // Predict
            _p += _q;

            // Update
            _k = _p / (_p + _r);

            // Quaternion interpolation (slerp) for updating the state
            _x = Quaternion.Slerp(_x, measurement, _k);

            _p = (1 - _k) * _p;

            return _x;
        }
    }
}
