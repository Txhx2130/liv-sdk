using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Liv.Lck
{
    /// <summary>
    /// Manages a single early update and a single late update callback in the Unity Player Loop.
    /// Use this class to register one early update and one late update system only.
    /// </summary>
    internal static class LckUpdateManager
    {
        private static ILckEarlyUpdate _earlyUpdateSystem;
        private static ILckLateUpdate _lateUpdateSystem;

        /// <summary>
        /// Registers a single system to handle early updates.
        /// If another system is already registered, it will be replaced.
        /// </summary>
        /// <param name="earlyUpdateSystem">The ILckEarlyUpdate system to register for early updates.</param>
        public static void RegisterSingleEarlyUpdate(ILckEarlyUpdate earlyUpdateSystem)
        {
            if(_earlyUpdateSystem != null)
                LckLog.LogWarning($"LCK EarlyUpdateSystem already has a reference ({_earlyUpdateSystem}). Note only one system is supported.");
            
            _earlyUpdateSystem = earlyUpdateSystem;
        }

        /// <summary>
        /// Unregisters the current early update system, if it matches the provided system.
        /// </summary>
        /// <param name="earlyUpdateSystem">The ILckEarlyUpdate system to unregister.</param>
        public static void UnregisterSingleEarlyUpdate(ILckEarlyUpdate earlyUpdateSystem)
        {
            if (_earlyUpdateSystem == earlyUpdateSystem)
                _earlyUpdateSystem = null;
        }

        /// <summary>
        /// Registers a single system to handle late updates.
        /// If another system is already registered, it will be replaced.
        /// </summary>
        /// <param name="lateUpdateSystem">The ILckLateUpdate system to register for late updates.</param>
        public static void RegisterSingleLateUpdate(ILckLateUpdate lateUpdateSystem)
        {
            if(_lateUpdateSystem != null)
                LckLog.LogWarning($"LCK LateUpdateSystem already has a reference ({_lateUpdateSystem}). Note only one system is supported.");
            
            _lateUpdateSystem = lateUpdateSystem;
        }

        /// <summary>
        /// Unregisters the current late update system, if it matches provided system.
        /// </summary>
        /// <param name="lateUpdateSystem">The ILckLateUpdate system to unregister.</param>
        public static void UnregisterSingleLateUpdate(ILckLateUpdate lateUpdateSystem)
        {
            if (_lateUpdateSystem == lateUpdateSystem)
                _lateUpdateSystem = null;
        }

        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            var currentPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();

            var lckEarlyUpdate = new PlayerLoopSystem
            {
                subSystemList = null,
                updateDelegate = OnEarlyUpdate,
                type = typeof(LckEarlyUpdate)
            };

            var lckLateUpdate = new PlayerLoopSystem
            {
                subSystemList = null,
                updateDelegate = OnLateUpdate,
                type = typeof(LckLateUpdate)
            };

            var loopWithLckEarlyUpdate = AddSystem<EarlyUpdate>(in currentPlayerLoop, lckEarlyUpdate);
            var loopWithLckLateUpdate = AddSystem<PostLateUpdate>(in loopWithLckEarlyUpdate, lckLateUpdate);

            PlayerLoop.SetPlayerLoop(loopWithLckLateUpdate);
        }

        private static PlayerLoopSystem AddSystem<T>(in PlayerLoopSystem loopSystem, PlayerLoopSystem systemToAdd) where T : struct
        {
            var newPlayerLoop = new PlayerLoopSystem
            {
                loopConditionFunction = loopSystem.loopConditionFunction,
                type = loopSystem.type,
                updateDelegate = loopSystem.updateDelegate,
                updateFunction = loopSystem.updateFunction
            };

            var targetType = typeof(T);
            var newSubSystemList = new List<PlayerLoopSystem>(loopSystem.subSystemList?.Length ?? 0);

            foreach (var subSystem in loopSystem.subSystemList)
            {
                newSubSystemList.Add(subSystem);

                if (subSystem.type == targetType)
                    newSubSystemList.Add(systemToAdd);
            }

            newPlayerLoop.subSystemList = newSubSystemList.ToArray();
            return newPlayerLoop;
        }

        private static void OnEarlyUpdate()
        {
            _earlyUpdateSystem?.EarlyUpdate();
        }

        private static void OnLateUpdate()
        {
            _lateUpdateSystem?.LateUpdate();
        }
    }
}
