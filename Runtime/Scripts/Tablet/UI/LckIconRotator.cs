using UnityEngine;

namespace Liv.Lck.UI
{
    public class LckIconRotator : MonoBehaviour
    {
        [SerializeField] 
        private float _rotationOffset;
        
        [SerializeField]
        private Transform _iconTransform;

        public void Rotate()
        {
            float rotation = _iconTransform.localEulerAngles.z + _rotationOffset;
            _iconTransform.localEulerAngles = new Vector3(0, 0, rotation);
        }

    }
}

