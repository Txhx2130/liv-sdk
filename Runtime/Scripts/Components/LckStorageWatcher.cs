using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Liv.Lck
{
    internal class LckStorageWatcher : ILckStorageWatcher
    {
        private readonly Action<LckResult> _onLowStorageSpace;
        private const long StorageThreshold = 500 * 1024 * 1024; // 500Mb
        private const float PollIntervalInSeconds = 5f;
        private long _freeSpace = long.MaxValue;
        
        public LckStorageWatcher(Action<LckResult> onLowStorageSpace)
        {
            _onLowStorageSpace = onLowStorageSpace;
            LckMonoBehaviourMediator.StartCoroutine("LckStorageWatcher:Update", Update());
        }
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetDiskFreeSpaceEx(
            string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);
        
        private IEnumerator Update()
        {
            while (true)
            {
                yield return new WaitForSeconds(PollIntervalInSeconds);
                CheckStorageSpace();
            }
        }
        
        private void CheckStorageSpace()
        {
            _freeSpace = GetAvailableStorageSpace();

            if (_freeSpace < StorageThreshold)
            {
                _onLowStorageSpace?.Invoke(LckResult.NewSuccess());
            }
        }
        
        private long GetAvailableStorageSpace()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return GetAndroidAvailableStorageSpace();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return GetWindowsAvailableStorageSpace();
#else
            // For other platforms
            return long.MaxValue;
#endif
        }
        
#if UNITY_ANDROID && !UNITY_EDITOR
        private long GetAndroidAvailableStorageSpace()
        {
            try
            {
                using (AndroidJavaClass statFsClass = new AndroidJavaClass("android.os.StatFs"))
                using (AndroidJavaObject statFs = new AndroidJavaObject("android.os.StatFs", Application.temporaryCachePath))
                {
                    long blockSize = statFs.Call<long>("getBlockSizeLong");
                    long availableBlocks = statFs.Call<long>("getAvailableBlocksLong");
                    return blockSize * availableBlocks;
                }
            }
            catch (Exception e)
            {
                LckLog.LogError("LCK Failed to get Android storage space: " + e.Message);
                return -1;
            }
        }
#endif

        public long GetWindowsAvailableStorageSpace()
        {
            try
            {
                string driveRoot = Path.GetPathRoot(Application.temporaryCachePath);
                ulong freeBytesAvailable, totalNumberOfBytes, totalNumberOfFreeBytes;

                if (GetDiskFreeSpaceEx(driveRoot, out freeBytesAvailable, out totalNumberOfBytes, out totalNumberOfFreeBytes))
                {
                    return (long)freeBytesAvailable;
                }
                else
                {
                    LckLog.LogError("Failed to get Windows storage space: " + Marshal.GetLastWin32Error());
                    return -1;
                }
            }
            catch (Exception e)
            {
                LckLog.LogError("Failed to get Windows storage space: " + e.Message);
                return -1;
            }
        }
        
        public bool HasEnoughFreeStorage()
        {
            return _freeSpace > StorageThreshold;
        }

        public void Dispose()
        {
            LckMonoBehaviourMediator.StopCoroutineByName("LckStorageWatcher:Update");
        }
    }
}
