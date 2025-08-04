using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Object = UnityEngine.Object;

namespace Liv.Lck
{

    [DefaultExecutionOrder(-1000)]
    public class LckMonoBehaviourMediator : MonoBehaviour
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();
        private static LckMonoBehaviourMediator _instance;
        
        public enum ApplicationLifecycleEventType
        {
            Quit,
            Pause
        }
        public delegate void LckApplicationLifecycleEventDelegate(ApplicationLifecycleEventType applicationLifecycleEventType);
        public static event LckApplicationLifecycleEventDelegate OnApplicationLifecycleEvent;
        public static LckMonoBehaviourMediator Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("LckMonoBehaviourMediator");
                    _instance = go.AddComponent<LckMonoBehaviourMediator>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
        
        private const float DurationForHMDToBecomeIdle = 10f;
        private float _hMDIdleTime;
        private Dictionary<string, Coroutine> _activeCoroutines = new Dictionary<string, Coroutine>();
        private bool _hMDFound;
        private bool _hMDWasMoving;
        private InputDevice _hmd;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            OnApplicationLifecycleEvent?.Invoke(ApplicationLifecycleEventType.Pause);
        }

        private void OnApplicationQuit()
        {
            OnApplicationLifecycleEvent?.Invoke(ApplicationLifecycleEventType.Quit);
        }

        private void Update()
        {
            HMDMountedOnHeadStateChange();
            ProcessExectionQueue();
        }
        
        private void HMDMountedOnHeadStateChange()
        {
            if (_hMDFound)
            {
                _hmd.TryGetFeatureValue(CommonUsages.deviceVelocity, out var hMDVelocity);
                var isHmdMoving = hMDVelocity.magnitude > 0.01f;

                if (isHmdMoving)
                {
                    _hMDWasMoving = true;
                    _hMDIdleTime = 0f; 
                }
                else if (_hMDWasMoving)
                {
                    _hMDIdleTime += Time.deltaTime; 

                    if (_hMDIdleTime >= DurationForHMDToBecomeIdle)
                    {
                        OnApplicationLifecycleEvent?.Invoke(ApplicationLifecycleEventType.Pause);
                        _hMDWasMoving = false;
                    }
                }
            }
            else
            {
                var inputDevices = new List<InputDevice>();
                InputDevices.GetDevices(inputDevices);

                foreach (var device in inputDevices)
                {
                    if (device.characteristics.HasFlag(InputDeviceCharacteristics.HeadMounted))
                    {
                        _hmd = device;
                        _hMDFound = true;
                    }
                }
            }
        }
        
        private static void ProcessExectionQueue()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }

        public static T[] FindObjectsOfComponentType<T>() where T : Object
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return null;
#endif
            
            return FindObjectsOfType<T>();
        }

        public static T AddComponentToMediator<T>() where T : UnityEngine.Component
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return null;
#endif
            
            return Instance.gameObject.AddComponent<T>();
        }

        public static Coroutine StartCoroutine(string coroutineName, IEnumerator routine)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return null;
#endif
            
            return Instance.StartCoroutineInternal(coroutineName, routine);
        }

        public static void StopCoroutineByName(string coroutineName)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif
            
            if(_instance != null)
                Instance.StopCoroutineInternal(coroutineName);
        }

        public static void StopAllActiveCoroutines()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif
            
            if(_instance != null)
                Instance.StopAllCoroutinesInternal();
        }
        
        public void EnqueueMainThreadAction(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        private Coroutine StartCoroutineInternal(string coroutineName, IEnumerator routine)
        {
            if (_activeCoroutines.ContainsKey(coroutineName))
            {
                StopCoroutineInternal(coroutineName);
            }
            
            var coroutine = base.StartCoroutine(routine);
            _activeCoroutines[coroutineName] = coroutine;

            return coroutine;
        }

        private void StopCoroutineInternal(string coroutineName)
        {
            if (_activeCoroutines.TryGetValue(coroutineName, out Coroutine coroutine))
            {
                try
                {
                    base.StopCoroutine(coroutine);
                }
                catch (Exception)
                {
                    // ignored
                }

                _activeCoroutines.Remove(coroutineName);
            }
        }

        private void StopAllCoroutinesInternal()
        {
            base.StopAllCoroutines();
            _activeCoroutines.Clear();
        }

        private void OnDestroy()
        {
            StopAllCoroutinesInternal();
        }
    }
}
