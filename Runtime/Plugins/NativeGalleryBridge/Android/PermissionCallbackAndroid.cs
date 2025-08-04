#if UNITY_ANDROID && !UNITY_EDITOR
using System.Threading;
using UnityEngine;

namespace Liv.NativeGalleryBridge
{
	public class PermissionCallbackAndroid : AndroidJavaProxy
	{
		private object threadLock;
		public int Result { get; private set; }

		public PermissionCallbackAndroid( object threadLock ) : base( "com.qck.nativegallerybridge.NativeGalleryPermissionReceiver" )
		{
			Result = -1;
			this.threadLock = threadLock;
		}

		[UnityEngine.Scripting.Preserve]
		public void OnPermissionResult( int result )
		{
			Result = result;

			lock( threadLock )
			{
				Monitor.Pulse( threadLock );
			}
		}
	}

	public class PermissionCallbackAsyncAndroid : AndroidJavaProxy
	{
		private readonly NativeGallery.PermissionCallback callback;
		private readonly NativeGalleryCallbackHelper _nativeGalleryCallbackHelper;

		public PermissionCallbackAsyncAndroid( NativeGallery.PermissionCallback callback ) : base( "com.qck.nativegallerybridge.NativeGalleryPermissionReceiver" )
		{
			this.callback = callback;
			_nativeGalleryCallbackHelper = new GameObject( "NativeGalleryCallbackHelper" ).AddComponent<NativeGalleryCallbackHelper>();
		}

		[UnityEngine.Scripting.Preserve]
		public void OnPermissionResult( int result )
		{
			_nativeGalleryCallbackHelper.CallOnMainThread( () => callback( (NativeGallery.Permission) result ) );
		}
	}
}
#endif
