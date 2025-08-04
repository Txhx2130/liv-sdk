using System;
using System.Globalization;
using System.IO;
using UnityEngine;
#if UNITY_2018_4_OR_NEWER && !NATIVE_GALLERY_DISABLE_ASYNC_FUNCTIONS
using System.Threading.Tasks;
#endif
#if UNITY_ANDROID
using Liv.NativeGalleryBridge;
#endif
using Object = UnityEngine.Object;


namespace Liv.NativeGalleryBridge
{
	/// <summary>
	/// Simplified abstraction of yasirkula's NativeGallery
	/// See https://github.com/yasirkula/UnityNativeGallery
	/// </summary>
	public static class NativeGallery
	{
		public enum PermissionType
		{
			Read = 0,
			Write = 1
		};

		public enum Permission
		{
			Denied = 0,
			Granted = 1,
			ShouldAsk = 2
		};

		[Flags]
		public enum MediaType
		{
			Video = 2,
			Image = 4,
		};

		public delegate void PermissionCallback(Permission permission);

		public delegate void MediaSaveCallback(bool success, string path);

		public delegate void MediaPickCallback(string path);

		#region Platform Specific Elements

#if !UNITY_EDITOR && UNITY_ANDROID
	private static AndroidJavaClass m_ajc = null;
	private static AndroidJavaClass AJC
	{
		get
		{
			if( m_ajc == null )
				m_ajc = new AndroidJavaClass( "com.qck.nativegallerybridge.NativeGallery" );

			return m_ajc;
		}
	}

	private static AndroidJavaObject _context = null;
	private static AndroidJavaObject Context
	{
		get
		{
			if( _context == null )
			{
				using( AndroidJavaObject unityClass = new AndroidJavaClass( "com.unity3d.player.UnityPlayer" ) )
				{
					_context = unityClass.GetStatic<AndroidJavaObject>( "currentActivity" );
				}
			}

			return _context;
		}
	}

	private static string _temporaryImagePath = null;

	private static string _selectedMediaPath = null;
	private static string SelectedMediaPath
	{
		get
		{
			if( _selectedMediaPath == null )
			{
				_selectedMediaPath = Path.Combine( Application.temporaryCachePath, "pickedMedia" );
				Directory.CreateDirectory( Application.temporaryCachePath );
			}

			return _selectedMediaPath;
		}
	}
		
	private class SaveMediaCallback : AndroidJavaProxy
	{
		private MediaSaveCallback _mediaSaveCallback;

		public SaveMediaCallback(MediaSaveCallback mediaSaveCallback) : base(
			"com.qck.nativegallerybridge.NativeGallery$SaveMediaCallback")
		{
			_mediaSaveCallback = mediaSaveCallback;
		}

		void onMediaSaved(bool success, string path)
		{
			_mediaSaveCallback?.Invoke(success, path);
		}
	}
#endif

		#endregion

		#region Runtime Permissions

		private const bool PermissionFreeMode = true;

		public static Permission CheckPermission(PermissionType permissionType, MediaType mediaTypes)
		{
#if !UNITY_EDITOR && UNITY_ANDROID
		Permission result =
 (Permission) AJC.CallStatic<int>( "CheckPermission", Context, permissionType == PermissionType.Read, (int) mediaTypes );
		if( result == Permission.Denied && (Permission) PlayerPrefs.GetInt( "NativeGalleryPermission", (int) Permission.ShouldAsk ) == Permission.ShouldAsk )
			result = Permission.ShouldAsk;

		return result;
#else
			return Permission.Granted;
#endif
		}

		public static Permission RequestPermission(PermissionType permissionType, MediaType mediaTypes)
		{
			// Don't block the main thread if the permission is already granted
			if (CheckPermission(permissionType, mediaTypes) == Permission.Granted)
				return Permission.Granted;

#if !UNITY_EDITOR && UNITY_ANDROID
		object threadLock = new object();
		lock( threadLock )
		{
			PermissionCallbackAndroid nativeCallback = new PermissionCallbackAndroid( threadLock );

			AJC.CallStatic( "RequestPermission", Context, nativeCallback, permissionType == PermissionType.Read, (int) mediaTypes, (int) Permission.ShouldAsk );

			if( nativeCallback.Result == -1 )
				System.Threading.Monitor.Wait( threadLock );

			if( (Permission) nativeCallback.Result != Permission.ShouldAsk && PlayerPrefs.GetInt( "NativeGalleryPermission", -1 ) != nativeCallback.Result )
			{
				PlayerPrefs.SetInt( "NativeGalleryPermission", nativeCallback.Result );
				PlayerPrefs.Save();
			}

			return (Permission) nativeCallback.Result;
		}
#else
			return Permission.Granted;
#endif
		}

		public static void RequestPermissionAsync(PermissionCallback callback, PermissionType permissionType,
			MediaType mediaTypes)
		{
#if !UNITY_EDITOR && UNITY_ANDROID
		PermissionCallbackAsyncAndroid nativeCallback = new PermissionCallbackAsyncAndroid( callback );
		AJC.CallStatic( "RequestPermission", Context, nativeCallback, permissionType == PermissionType.Read, (int) mediaTypes, (int) Permission.ShouldAsk );
#else
			callback(Permission.Granted);
#endif
		}

#if UNITY_2018_4_OR_NEWER && !NATIVE_GALLERY_DISABLE_ASYNC_FUNCTIONS
		public static Task<Permission> RequestPermissionAsync(PermissionType permissionType, MediaType mediaTypes)
		{
			TaskCompletionSource<Permission> tcs = new TaskCompletionSource<Permission>();
			RequestPermissionAsync((permission) => tcs.SetResult(permission), permissionType, mediaTypes);
			return tcs.Task;
		}
#endif

		private static Permission ProcessPermission(Permission permission)
		{
			return (PermissionFreeMode && (int)permission == 3) ? Permission.Granted : permission;
		}

		#endregion

		#region Save Functions

		public static async Task<Permission> SaveVideoToGallery(byte[] mediaBytes, string album, string filename,
			MediaSaveCallback callback = null)
		{
			return await SaveToGallery(mediaBytes, album, filename, MediaType.Video, callback);
		}

		public static async Task<Permission> SaveVideoToGallery(string existingMediaPath, string album, string filename,
			MediaSaveCallback callback = null)
		{
			return await SaveToGallery(existingMediaPath, album, filename, MediaType.Video, callback);
		}
		
		public static async Task<Permission> SaveImageToGallery(string existingMediaPath, string album, string filename,
			MediaSaveCallback callback = null)
		{
			return await SaveToGallery(existingMediaPath, album, filename, MediaType.Image, callback);
		}
		
		public static string GetExternalStoragePublicDirectory()
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			return AJC.CallStatic<string>("GetExternalStoragePublicDirectory");
#endif
			return "";
		}

		#endregion


		#region Internal Functions

		private static async Task<Permission> SaveToGallery(byte[] mediaBytes, string album, string filename,
			MediaType mediaType, MediaSaveCallback callback)
		{
			Permission result = await RequestPermissionAsync(PermissionType.Write, mediaType);
			if (result == Permission.Granted)
			{
				if (mediaBytes == null || mediaBytes.Length == 0)
					throw new ArgumentException("Parameter 'mediaBytes' is null or empty!");

				if (album == null || album.Length == 0)
					throw new ArgumentException("Parameter 'album' is null or empty!");

				if (filename == null || filename.Length == 0)
					throw new ArgumentException("Parameter 'filename' is null or empty!");

				if (string.IsNullOrEmpty(Path.GetExtension(filename)))
					Debug.LogWarning(
						"LCK 'filename' doesn't have an extension, this might result in unexpected behaviour!");

				string path = GetTemporarySavePath(filename);
#if UNITY_EDITOR
				Debug.Log("LCK SaveToGallery called successfully in the Editor");
#else
    		using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
    		{
      		await fileStream.WriteAsync(mediaBytes, 0, mediaBytes.Length);
    		}
#endif

				SaveToGalleryInternal(path, album, mediaType, callback);
			}

			return result;
		}

		private static async Task<Permission> SaveToGallery(string existingMediaPath, string album, string filename,
			MediaType mediaType, MediaSaveCallback callback)
		{
			Permission result = await RequestPermissionAsync(PermissionType.Write, mediaType);
			if (result == Permission.Granted)
			{
				if (!File.Exists(existingMediaPath))
					throw new FileNotFoundException("File not found at " + existingMediaPath);

				if (album == null || album.Length == 0)
					throw new ArgumentException("Parameter 'album' is null or empty!");

				if (filename == null || filename.Length == 0)
					throw new ArgumentException("Parameter 'filename' is null or empty!");

				if (string.IsNullOrEmpty(Path.GetExtension(filename)))
				{
					string originalExtension = Path.GetExtension(existingMediaPath);
					if (string.IsNullOrEmpty(originalExtension))
						Debug.LogWarning(
							"LCK 'filename' doesn't have an extension, this might result in unexpected behaviour!");
					else
						filename += originalExtension;
				}

				string path = GetTemporarySavePath(filename);
#if UNITY_EDITOR
				Debug.Log("LCK SaveToGallery called successfully in the Editor");
#else
        await Task.Run(() => File.Copy(existingMediaPath, path, true));
#endif

				SaveToGalleryInternal(path, album, mediaType, callback);
			}

			return result;
		}

		private static void SaveToGalleryInternal(string path, string album, MediaType mediaType,
			MediaSaveCallback callback)
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			AJC.CallStatic("saveMediaAsync", Context, path, album, new SaveMediaCallback(callback));
#else
			if (callback != null)
			{
				callback(true, null);
			}
#endif
		}

		private static string GetTemporarySavePath(string filename)
		{
			string saveDir = Path.Combine(Application.persistentDataPath, "NGallery");
			Directory.CreateDirectory(saveDir);

			return Path.Combine(saveDir, filename);
		}

		#endregion
	}
}
