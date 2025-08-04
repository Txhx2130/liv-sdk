using UnityEngine;
using UnityEngine.Serialization;

namespace Liv.Lck.Smoothing
{
    [DefaultExecutionOrder(1000)]
    public class LckStabilizer : MonoBehaviour
    {
        [Header("Transform References")]
        [SerializeField]
        [FormerlySerializedAs("StabilizationTarget")]
        private Transform _stabilizationTarget;
        
        [SerializeField]
        [FormerlySerializedAs("TargetToFollow")]
        private Transform _targetToFollow;

        [Header("Stabilization Settings")]
        [SerializeField]
        [FormerlySerializedAs("PositionalSmoothing")]
        private float _positionalSmoothing = 0.1f;
        
        [SerializeField]
        [FormerlySerializedAs("RotationalSmoothing")]
        private float _rotationalSmoothing = 0.1f;

        [SerializeField]
        [FormerlySerializedAs("AffectPosition")]
        private bool _affectPosition = true;

        [SerializeField]
        [FormerlySerializedAs("AffectRotation")]
        private bool _affectRotation = true;
        
        [Header("Optional References")]
        [SerializeField]
        [Tooltip("(Optional) Follow target movement relative to this transform will be stabilized. " +
                 "If left unspecified, will stabilize follow target movement in world space.")]
        private Transform _stabilizationSpaceOrigin;

        private KalmanFilterVector3 _positionFilter;
        private KalmanFilterQuaternion _rotationFilter;
        
        public Transform StabilizationTarget
        {
            get => _stabilizationTarget;
            set => _stabilizationTarget = value;
        }

        public Transform TargetToFollow
        {
            get => _targetToFollow;
            set => _targetToFollow = value;
        }
        
        /// <summary>
        /// Optional stabilization space origin <see cref="Transform"/> reference.
        /// If set, <see cref="TargetToFollow"/> movement relative to this transform will be stabilized.
        /// If unset, <see cref="TargetToFollow"/> movement relative to the world space origin will be stabilized.
        /// </summary>
        public Transform StabilizationSpaceOrigin
        {
            get => _stabilizationSpaceOrigin;
            set
            {
                if (_stabilizationSpaceOrigin == value)
                    return;
                
                _stabilizationSpaceOrigin = value;
                HandleStabilizationSpaceChanged();
            }
        }
        
        public float PositionalSmoothing
        {
            get => _positionalSmoothing;
            set => _positionalSmoothing = value;
        }

        public float RotationalSmoothing
        {
            get => _rotationalSmoothing;
            set => _rotationalSmoothing = value;
        }
        
        public bool AffectPosition
        {
            get => _affectPosition;
            set => _affectPosition = value;
        }

        public bool AffectRotation
        {
            get => _affectRotation;
            set => _affectRotation = value;
        }
        
        private KalmanFilterVector3 PositionFilter => _positionFilter ??=
            new KalmanFilterVector3(GetStabilizationSpacePosition(TargetToFollow.position));
        
        private KalmanFilterQuaternion RotationFilter => _rotationFilter ??=
            new KalmanFilterQuaternion(GetStabilizationSpaceRotation(TargetToFollow.rotation));

        private bool HasCustomStabilizationSpace => _stabilizationSpaceOrigin;
        
        private void LateUpdate()
        {
            DoStabilizationUpdate(PositionalSmoothing, RotationalSmoothing);
        }

        public void ReachTargetInstantly()
        {
            DoStabilizationUpdate(0, 0);
        }

        private void DoStabilizationUpdate(float positionalSmoothing, float rotationalSmoothing)
        {
            if (AffectPosition)
            {
                var targetPosInStabilizationSpace = GetStabilizationSpacePosition(TargetToFollow.position);
                var smoothedPosInStabilizationSpace = PositionFilter.Update(targetPosInStabilizationSpace, Time.deltaTime, positionalSmoothing);
                StabilizationTarget.position = GetWorldPosition(smoothedPosInStabilizationSpace);
            }

            if (AffectRotation)
            {
                var targetRotInStabilizationSpace = GetStabilizationSpaceRotation(TargetToFollow.rotation);
                var smoothedRotInStabilizationSpace = RotationFilter.Update(targetRotInStabilizationSpace, Time.deltaTime, rotationalSmoothing);
                StabilizationTarget.rotation = GetWorldRotation(smoothedRotInStabilizationSpace);
            }
        }

        private void HandleStabilizationSpaceChanged()
        {
            // Reset stabilization filters relative to new stabilization space origin
            if (AffectPosition)
            {
                var currentPositionInStabilizationSpace = GetStabilizationSpacePosition(StabilizationTarget.position);
                PositionFilter.Update(currentPositionInStabilizationSpace, Time.deltaTime, 0);
            }

            if (AffectRotation)
            {
                var currentRotationInStabilizationSpace = GetStabilizationSpaceRotation(StabilizationTarget.rotation);
                RotationFilter.Update(currentRotationInStabilizationSpace, Time.deltaTime, 0);
            }
        }

        private Vector3 GetWorldPosition(Vector3 stabilizationSpacePosition)
        {
            return HasCustomStabilizationSpace
                ? StabilizationSpaceOrigin.TransformPoint(stabilizationSpacePosition)
                : stabilizationSpacePosition;
        }

        private Quaternion GetWorldRotation(Quaternion stabilizationSpaceRotation)
        {
            return HasCustomStabilizationSpace
                ? StabilizationSpaceOrigin.rotation * stabilizationSpaceRotation
                : stabilizationSpaceRotation;
        }
        
        private Vector3 GetStabilizationSpacePosition(Vector3 worldPosition)
        {
            return HasCustomStabilizationSpace
                ? StabilizationSpaceOrigin.InverseTransformPoint(worldPosition)
                : worldPosition;
        }

        private Quaternion GetStabilizationSpaceRotation(Quaternion worldRotation)
        {
            return HasCustomStabilizationSpace
                ? Quaternion.Inverse(StabilizationSpaceOrigin.rotation) * worldRotation
                : worldRotation;
        }
    }
}
