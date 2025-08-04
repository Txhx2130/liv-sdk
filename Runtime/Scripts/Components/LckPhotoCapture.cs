using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Liv.Lck.Settings;
using Liv.Lck.Utilities;
using Unity.Collections;
using Unity.Profiling;
using System.Text;
using System.Collections.Generic;

namespace Liv.Lck
{
    internal class LckPhotoCapture : ILckPhotoCapture
    {
        private static readonly string[] ImageFileFormatStrings = { "exr", "jpg", "tga", "png" };
        
        private RenderTexture _renderTexture;
        private Action<LckResult> _onPhotoCaptureSaved;
        private StringBuilder _imageFilePathBuilder = new StringBuilder(256);

        private Queue<Action> _captureQueue = new Queue<Action>();
        private bool _isCapturing = false;

        static readonly ProfilerMarker _copyOutputFileToNativeGalleryProfileMarker = new ProfilerMarker("LckPhotoCapture.CopyOutputFileToPhotoGallery");
        static readonly ProfilerMarker _captureProfileMarker = new ProfilerMarker("LckPhotoCapture.Capture");
        static readonly ProfilerMarker _asyncCallbackProfileMarker = new ProfilerMarker("LckPhotoCapture.AsyncCallback");

        public LckPhotoCapture(RenderTexture renderTexture, Action<LckResult> onPhotoCaptureSaved)
        {
            _renderTexture = renderTexture;
            _onPhotoCaptureSaved = onPhotoCaptureSaved;
        }

        public LckResult Capture()
        {
            _captureQueue.Enqueue(() =>
            {
                using (_captureProfileMarker.Auto())
                {
                    var imageFormat = LckSettings.Instance.ImageCaptureFileFormat;
                    _imageFilePathBuilder.Clear();
                    _imageFilePathBuilder.Append(Path.Combine(Application.temporaryCachePath, FileUtility.GenerateFilename(ImageFileFormatStrings[(int)imageFormat])));

                    SaveRenderTextureToFile(_imageFilePathBuilder.ToString(), LckSettings.Instance.ImageCaptureFileFormat, OnCaptureComplete);
                }
            });

            if (!_isCapturing)
            {
                ProcessQueue();
            }

            return LckResult.NewSuccess();
        }

        private void ProcessQueue()
        {
            if (_captureQueue.Count > 0 && !_isCapturing)
            {
                _isCapturing = true;
                var captureAction = _captureQueue.Dequeue();
                captureAction.Invoke();
            }
        }

        private void OnCaptureComplete(LckResult result)
        {
            if (result.Success)
            {
                LckMonoBehaviourMediator.StartCoroutine("CopyImageToGalleryWhenReady", CopyImageToGalleryWhenReady());
            }
            else
            {
                _onPhotoCaptureSaved.Invoke(result);
                _isCapturing = false;
                ProcessQueue();
            }
        }

        WaitForSecondsRealtime _copyPhotoSpinWait = new WaitForSecondsRealtime(0.1f);
        private IEnumerator CopyImageToGalleryWhenReady()
        {
            while (FileUtility.IsFileLocked(_imageFilePathBuilder.ToString()) && File.Exists(_imageFilePathBuilder.ToString()))
            {
                yield return _copyPhotoSpinWait;
            }

            using (_copyOutputFileToNativeGalleryProfileMarker.Auto())
            {
                Task task = FileUtility.CopyToGallery(_imageFilePathBuilder.ToString(), LckSettings.Instance.RecordingAlbumName,
                    (success, path) =>
                    {
                        LckMonoBehaviourMediator.Instance.EnqueueMainThreadAction(() =>
                        {
                            if (success)
                            {
                                LckLog.Log("LCK Photo saved to gallery: " + path);
                                _onPhotoCaptureSaved.Invoke(LckResult.NewSuccess());
                            }
                            else
                            {
                                _onPhotoCaptureSaved.Invoke(
                                    LckResult.NewError(LckError.FailedToCopyPhotoToGallery,
                                        "Failed to copy photo to Gallery"));
                                LckLog.LogError("LCK Failed to save photo to gallery");
                            }

                            _isCapturing = false;
                            ProcessQueue();
                        });
                    });

                yield return new WaitUntil(() => task.IsCompleted);
            }
        }

        public void SetRenderTexture(RenderTexture renderTexture)
        {
            _renderTexture = renderTexture;
        }

        private void SaveRenderTextureToFile(
            string filePath,
            LckSettings.ImageFileFormat fileFormat,
            Action<LckResult> onCaptureComplete)
        {
            if (_renderTexture == null)
            {
                onCaptureComplete?.Invoke(LckResult.NewError(LckError.PhotoCaptureError, "RenderTexture is null"));
                return;
            }

            var width = _renderTexture.width;
            var height = _renderTexture.height;

            var renderTextureGraphicsFormat = _renderTexture.graphicsFormat;
            var narray = new NativeArray<byte>(width * height * 4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            var request = AsyncGPUReadback.RequestIntoNativeArray(ref narray, _renderTexture, 0, (AsyncGPUReadbackRequest request) =>
            {
                using (_asyncCallbackProfileMarker.Auto())
                {
                    if (!request.hasError)
                    {
                        Task.Run(() =>
                        {
                            NativeArray<byte> encoded = default;
                            FillAlphaChannel(narray);
                            
                            try
                            {
                                switch (fileFormat)
                                {
                                    case LckSettings.ImageFileFormat.EXR:
                                        encoded = ImageConversion.EncodeNativeArrayToEXR(narray,
                                            renderTextureGraphicsFormat, (uint)width, (uint)height);
                                        break;
                                    case LckSettings.ImageFileFormat.JPG:
                                        encoded = ImageConversion.EncodeNativeArrayToJPG(narray,
                                            renderTextureGraphicsFormat, (uint)width, (uint)height, 0, 95);
                                        break;
                                    case LckSettings.ImageFileFormat.TGA:
                                        encoded = ImageConversion.EncodeNativeArrayToTGA(narray,
                                            renderTextureGraphicsFormat, (uint)width, (uint)height);
                                        break;
                                    default:
                                        encoded = ImageConversion.EncodeNativeArrayToPNG(narray,
                                            renderTextureGraphicsFormat, (uint)width, (uint)height);
                                        break;
                                }

                                File.WriteAllBytes(filePath, encoded.ToArray());
                            }
                            catch
                            {
                                LckLog.LogError("LCK Failed to encode image during Photo Capture");
                                LckMonoBehaviourMediator.Instance.EnqueueMainThreadAction(() => onCaptureComplete?.Invoke(LckResult.NewError(LckError.PhotoCaptureError, "Failed to save photo to gallery")));
                            }
                            finally
                            {
                                if (encoded.IsCreated)
                                {
                                    encoded.Dispose();
                                }
                                
                                narray.Dispose();
                                LckMonoBehaviourMediator.Instance.EnqueueMainThreadAction(() => onCaptureComplete?.Invoke(LckResult.NewSuccess()));
                            }
                        });
                    }
                    else
                    {
                        narray.Dispose();
                        LckMonoBehaviourMediator.Instance.EnqueueMainThreadAction(() => onCaptureComplete?.Invoke(LckResult.NewError(LckError.PhotoCaptureError, "AsyncGPUReadback.RequestIntoNativeArray Failed")));
                    }
                }
            });
        }

        private static void FillAlphaChannel(NativeArray<byte> narray)
        {
            for (int i = 0; i < narray.Length; i += 4)
            {
                narray[i + 3] = 255;
            }
        }
    }
}