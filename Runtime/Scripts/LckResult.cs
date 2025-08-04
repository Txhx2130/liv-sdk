using System;

namespace Liv.Lck
{
    public interface ILckResult
    {
        bool Success { get; }
        string Message { get; }
        Nullable<LckError> Error { get; }
    }

    public enum LckError
    {
        ServiceNotCreated = 1,
        ServiceDisposed = 2,
        InvalidDescriptor = 3,
        CameraIdNotFound = 4,
        MonitorIdNotFound = 5,
        MicrophonePermissionDenied = 6,
        RecordingAlreadyStarted = 7,
        NotCurrentlyRecording = 8,
        RecordingError = 9,
        PhotoCaptureError = 10,
        CantEditSettingsWhileRecording = 11,
        NotEnoughStorageSpace = 12,
        FailedToCopyRecordingToGallery = 13,
        FailedToCopyPhotoToGallery = 14,
        UnsupportedGraphicsApi = 15,
        UnsupportedPlatform = 16,
        MicrophoneError = 17,
        UnknownError = 18,
    }
    
    public class LckResult<T> : ILckResult
    {
        private bool _success;
        private string _message;
        private Nullable<LckError> _error;
        private T _result;

        public bool Success => _success;
        public string Message => _message;
        public Nullable<LckError> Error => _error;
        public T Result => _result;

        private LckResult(bool success, string message, Nullable<LckError> error, T result)
        {
            _success = success;
            _message = message;
            _error = error;
            _result = result;
        }

        internal static LckResult<T> NewSuccess(T result)
        {
            return new LckResult<T>(true, null, null, result);
        }

        internal static LckResult<T> NewError(LckError error, string message)
        {
            return new LckResult<T>(false, message, error, default(T));
        }
    }

    public class LckResult : ILckResult
    {
        private bool _success;
        private string _message;
        private Nullable<LckError> _error;

        public bool Success => _success;
        public string Message => _message;
        public Nullable<LckError> Error => _error;

        private LckResult(bool success, string message, Nullable<LckError> error)
        {
            _success = success;
            _message = message;
            _error = error;
        }

        internal static LckResult NewSuccess()
        {
            return new LckResult(true, null, null);
        }

        internal static LckResult NewError(LckError error, string message)
        {
            return new LckResult(false, message, error);
        }
    }
}
