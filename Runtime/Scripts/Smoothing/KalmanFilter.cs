using UnityEngine;

namespace Liv.Lck.Smoothing
{
    public class KalmanFilter
    {
        private float _estimationErrorCovariance;
        private float _filteredValue;
        private float _kalmanGain;

        public KalmanFilter(
            float initialEstimate = 0,
            float initialCovariance = 1
        )
        {
            _filteredValue = initialEstimate;
            _estimationErrorCovariance = initialCovariance;
        }

        public float Update(float measurement, float deltaTime, float smoothing)
        {
            var processNoiseCovariance = Mathf.Lerp(10.0f, 0.0f, smoothing);
            var measurementNoiseCovariance = Mathf.Lerp(0.0f, 10.0f, smoothing);

            float dt = Mathf.Max(deltaTime, 0.0001f);
            float processNoiseScaled = processNoiseCovariance * dt;

            _estimationErrorCovariance += processNoiseScaled;

            _kalmanGain =
                _estimationErrorCovariance
                / (_estimationErrorCovariance + measurementNoiseCovariance);
            _filteredValue = _filteredValue + _kalmanGain * (measurement - _filteredValue);
            _estimationErrorCovariance = (1.0f - _kalmanGain) * _estimationErrorCovariance;

            return _filteredValue;
        }
    }

    public class KalmanFilterQuaternion
    {
        private float _estimationErrorCovariance;
        private float _kalmanGain;
        private Quaternion _filteredValue;

        public KalmanFilterQuaternion(Quaternion initialEstimate, float initialCovariance = 1)
        {
            _filteredValue = initialEstimate;
            _estimationErrorCovariance = initialCovariance;
        }

        public Quaternion Update(Quaternion measurement, float deltaTime, float smoothing)
        {
            var processNoiseCovariance = Mathf.Lerp(10.0f, 0.0f, smoothing);
            var measurementNoiseCovariance = Mathf.Lerp(0.0f, 10.0f, smoothing);

            float dt = Mathf.Max(deltaTime, 0.0001f);
            float processNoiseScaled = processNoiseCovariance * dt;

            _estimationErrorCovariance += processNoiseScaled;

            _kalmanGain =
                _estimationErrorCovariance
                / (_estimationErrorCovariance + measurementNoiseCovariance);

            _filteredValue = Quaternion.Slerp(_filteredValue, measurement, _kalmanGain);

            _estimationErrorCovariance = (1.0f - _kalmanGain) * _estimationErrorCovariance;

            return _filteredValue;
        }
    }

    public class KalmanFilterVector3
    {
        private KalmanFilter _filterX;
        private KalmanFilter _filterY;
        private KalmanFilter _filterZ;

        public KalmanFilterVector3(
            Vector3 initialEstimate = new Vector3(),
            float initialCovariance = 1
        )
        {
            _filterX = new KalmanFilter(initialEstimate.x, initialCovariance);
            _filterY = new KalmanFilter(initialEstimate.y, initialCovariance);
            _filterZ = new KalmanFilter(initialEstimate.z, initialCovariance);
        }

        public Vector3 Update(Vector3 measurement, float deltaTime, float smoothing)
        {
            return new Vector3(
                _filterX.Update(measurement.x, deltaTime, smoothing),
                _filterY.Update(measurement.y, deltaTime, smoothing),
                _filterZ.Update(measurement.z, deltaTime, smoothing)
            );
        }
    }
}
