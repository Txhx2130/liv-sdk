using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Liv.NGFX
{
    public enum LogLevel
    {
        Log,
        Warning,
        Error,
        Abort,
    }

    public class NI
    {
        [DllImport("ngfx")] public static extern IntPtr GetPluginEventFunction();
        [DllImport("ngfx")] public static extern uint AllocResource(IntPtr resource_ctx);
        [DllImport("ngfx")] public static extern void SetGlobalLogLevel(LogLevel level, bool enableGLMessages);
    }
    
    public enum EventType : int
    {
        GraphicsBufferCreate,
        GraphicsBufferCopy,
        TextureCreate,
        RenderBufferCreate,
        ResourceDestroy,
    }

    public class Handle<T> : IDisposable
    {
        T m_data;
        GCHandle m_handle;
        bool m_valid = false;
        public Handle(T data)
        {
            m_data = data;
            m_handle = GCHandle.Alloc(m_data, GCHandleType.Pinned);
            m_valid = true;
        }
        ~Handle()
        {
            Dispose();
        }
        public IntPtr ptr() { return m_handle.AddrOfPinnedObject(); }
        // You need to copy the data from the underlying ptr back to the managed object to get the data modified from native code
        public T data() { return m_data; }
        public void Dispose()
        {
            if (m_valid)
                m_handle.Free();
            m_valid = false;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ResourceDestroyInfo
    {
        public static EventType eventType = EventType.ResourceDestroy;
        IntPtr m_context;
        uint m_id;
        public ResourceDestroyInfo(IntPtr ctx, uint id)
        {
            m_context = ctx;
            m_id = id;
        }
    }

    public class NativeTexture : IDisposable
    {
        public enum Format : int
        {
            RGBA,
            Depth,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TextureCreateInfo
        {
            public static EventType eventType = EventType.TextureCreate;
            IntPtr m_context;
            IntPtr m_handle;
            int m_width;
            int m_height;
            Format m_format;
            uint m_out_id;
            public TextureCreateInfo(IntPtr ctx, uint id, IntPtr handle, int width, int height, Format format)
            {
                m_context= ctx;
                m_handle = handle;
                m_width = width;
                m_height = height;
                m_format = format;
                m_out_id = id;
            }
            public uint id() { return m_out_id; }
        }

        Texture2D m_texture;
        uint m_id;
        bool m_valid = false;
        IntPtr m_context = IntPtr.Zero;

        public NativeTexture(IntPtr ctx, int width, int height, Format format)
        {
            m_context = ctx;
            m_texture = new Texture2D(width, height, FormatToUnity(format), false);
            m_id = NI.AllocResource(m_context);
            var createInfo = new Handle<TextureCreateInfo>(new TextureCreateInfo(ctx, m_id, m_texture.GetNativeTexturePtr(), width, height, format));
            var cb = new CommandBuffer();
            cb.IssuePluginEventAndData(NI.GetPluginEventFunction(), ((int)TextureCreateInfo.eventType), createInfo.ptr());
            Graphics.ExecuteCommandBuffer(cb);
            m_valid = true;
        }
        ~NativeTexture()
        {
            // TODO: Set up warning in case of undisposed object instead
            // Dispose();
        }
        public TextureFormat FormatToUnity(Format fmt)
        {
            switch (fmt)
            {
                case Format.RGBA:
                    return TextureFormat.RGBA32;
                case Format.Depth:
                    return TextureFormat.R16;
                default:
                    throw new NotSupportedException();
            }
        }
        public static implicit operator Texture2D(NativeTexture o) => o.m_texture;
        public uint id { get { return m_id; } }
        public Texture2D texture { get { return m_texture; } }
        public void Dispose()
        {
            if (m_valid)
            {
                var destroyInfo = new Handle<ResourceDestroyInfo>(new ResourceDestroyInfo(m_context, m_id));
                CommandBuffer cb = new CommandBuffer();
                cb.IssuePluginEventAndData(NI.GetPluginEventFunction(), ((int)ResourceDestroyInfo.eventType), destroyInfo.ptr());
                Graphics.ExecuteCommandBuffer(cb);
                m_valid = false;
                m_texture = null;
            }
        }
    }

    public class NativeRenderBuffer : IDisposable
    {
        public enum Format : int
        {
            RGBA,
            Depth,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct RenderBufferCreateInfo
        {
            public static EventType eventType = EventType.RenderBufferCreate;
            IntPtr m_context;
            IntPtr m_handle;
            int m_width;
            int m_height;
            int m_mips;
            GraphicsFormat m_format;
            uint m_out_id;
            public RenderBufferCreateInfo(IntPtr ctx, uint id, IntPtr handle, int width, int height, int mips, GraphicsFormat format)
            {
                m_context= ctx;
                m_handle = handle;
                m_width = width;
                m_height = height;
                m_mips = mips;
                m_format = format;
                m_out_id = id;
            }
            public uint id() { return m_out_id; }
        }

        RenderBuffer m_buffer;
        uint m_id;
        int m_mips;
        bool m_valid = false;
        IntPtr m_context = IntPtr.Zero;

        public NativeRenderBuffer(IntPtr ctx, RenderBuffer rb, int width, int height, int mips, GraphicsFormat format)
        {
            m_context = ctx;
            m_buffer = rb;
            m_mips = mips;
            m_id = NI.AllocResource(m_context);
            var createInfo = new Handle<RenderBufferCreateInfo>(
                new RenderBufferCreateInfo(ctx, m_id, rb.GetNativeRenderBufferPtr(), width, height, mips, format));
            var cb = new CommandBuffer();
            cb.IssuePluginEventAndData(NI.GetPluginEventFunction(), (int)RenderBufferCreateInfo.eventType, createInfo.ptr());
            Graphics.ExecuteCommandBuffer(cb);
            m_valid = true;
        }
        public NativeRenderBuffer(IntPtr ctx, RenderBuffer rb, IntPtr texturePtr, int width, int height, int mips, GraphicsFormat format)
        {
            m_context = ctx;
            m_buffer = rb;
            m_mips = mips;
            m_id = NI.AllocResource(m_context);
            var createInfo = new Handle<RenderBufferCreateInfo>(
                new RenderBufferCreateInfo(ctx, m_id, texturePtr, width, height, mips, format));
            var cb = new CommandBuffer();
            cb.IssuePluginEventAndData(NI.GetPluginEventFunction(), (int)RenderBufferCreateInfo.eventType, createInfo.ptr());
            Graphics.ExecuteCommandBuffer(cb);
            m_valid = true;
        }
        ~NativeRenderBuffer()
        {
            // TODO: Set up warning in case of undisposed object instead
            // Dispose();
        }
        public static implicit operator RenderBuffer(NativeRenderBuffer o) => o.m_buffer;
        public uint id { get { return m_id; } }
        public RenderBuffer buffer { get { return m_buffer; } }
        public void Dispose()
        {
            if (m_valid)
            {
                var destroyInfo = new Handle<ResourceDestroyInfo>(new ResourceDestroyInfo(m_context, m_id));
                CommandBuffer cb = new CommandBuffer();
                cb.IssuePluginEventAndData(NI.GetPluginEventFunction(), ((int)ResourceDestroyInfo.eventType), destroyInfo.ptr());
                Graphics.ExecuteCommandBuffer(cb);
                m_valid = false;
            }
        }
    }

    public class NativeGraphicsBuffer<T> : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BufferCreateInfo
        {
            public static EventType eventType = EventType.GraphicsBufferCreate;
            IntPtr m_context;
            IntPtr m_handle;
            int m_count;
            int m_stride;
            GraphicsBuffer.Target m_target;
            uint m_out_id;
            public BufferCreateInfo(IntPtr ctx, uint id, IntPtr handle, int count, int stride, GraphicsBuffer.Target target)
            {
                m_context = ctx;
                m_handle = handle;
                m_count = count;
                m_stride = stride;
                m_target = target;
                m_out_id = id;
            }
            public uint id() { return m_out_id; }
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BufferCopyInfo
        {
            public static EventType eventType = EventType.GraphicsBufferCopy;
            IntPtr m_context;
            uint m_src;
            uint m_dst;
            uint m_size;
            public BufferCopyInfo(IntPtr ctx, uint src, uint dst, uint size)
            {
                m_context = ctx;
                m_src = src;
                m_dst = dst;
                m_size = size;
            }
        }

        GraphicsBuffer m_buffer = null;
        uint m_id;
        int m_count = 0;
        bool m_valid = false;
        IntPtr m_context = IntPtr.Zero;

        public NativeGraphicsBuffer(IntPtr ctx, int count, GraphicsBuffer.Target target)
        {
            m_context = ctx;
            m_count = count;
            int stride = Marshal.SizeOf(typeof(T));
            m_buffer = new GraphicsBuffer(target, count, stride);
            // Just call it once so the next time it will return a valid ptr
            m_buffer.GetNativeBufferPtr();
            m_id = NI.AllocResource(m_context);
            #if UNITY_2021_2_OR_NEWER
            m_buffer.name = "NativeGraphicsBuffer " + m_id;
            #endif
            var createInfo = new Handle<BufferCreateInfo>(new BufferCreateInfo(ctx, m_id, m_buffer.GetNativeBufferPtr(), count, stride, target));
            var cb = new CommandBuffer();
            cb.IssuePluginEventAndData(NI.GetPluginEventFunction(), ((int)BufferCreateInfo.eventType), createInfo.ptr());
            Graphics.ExecuteCommandBuffer(cb);
            m_valid = true;
        }
        public NativeGraphicsBuffer(IntPtr ctx, int count, IntPtr nativeBuffer, GraphicsBuffer.Target target)
        {
            m_context = ctx;
            m_count = count;
            int stride = Marshal.SizeOf(typeof(T));
            m_id = NI.AllocResource(m_context);
            var createInfo = new Handle<BufferCreateInfo>(new BufferCreateInfo(ctx, m_id, nativeBuffer, count, stride, target));
            var cb = new CommandBuffer();
            cb.IssuePluginEventAndData(NI.GetPluginEventFunction(), ((int)BufferCreateInfo.eventType), createInfo.ptr());
            Graphics.ExecuteCommandBuffer(cb);
            m_valid = true;
        }
        public NativeGraphicsBuffer(IntPtr ctx, GraphicsBuffer buffer, GraphicsBuffer.Target target)
        {
            m_context = ctx;
            m_count = buffer.count;
            int stride = buffer.stride;
            m_buffer = buffer;
            // Just call it once so the next time it will return a valid ptr
            m_buffer.GetNativeBufferPtr();
            m_id = NI.AllocResource(m_context);
            var createInfo = new Handle<BufferCreateInfo>(new BufferCreateInfo(ctx, m_id, m_buffer.GetNativeBufferPtr(), count, stride, target));
            var cb = new CommandBuffer();
            cb.IssuePluginEventAndData(NI.GetPluginEventFunction(), ((int)BufferCreateInfo.eventType), createInfo.ptr());
            Graphics.ExecuteCommandBuffer(cb);
            m_valid = true;
        }
        ~NativeGraphicsBuffer()
        {
            // TODO: Set up warning in case of undisposed object instead
            // Dispose();
        }
        public void BufferCopy(NativeGraphicsBuffer<T> dst, uint size)
        {
            var copyInfo = new Handle<BufferCopyInfo>(new BufferCopyInfo(m_context, m_id, dst.m_id, size));
            CommandBuffer cb = new CommandBuffer();
            cb.IssuePluginEventAndData(NI.GetPluginEventFunction(), ((int)BufferCopyInfo.eventType), copyInfo.ptr());
            Graphics.ExecuteCommandBuffer(cb);
        }
        public static implicit operator GraphicsBuffer(NativeGraphicsBuffer<T> o) => o.m_buffer;
        public uint id { get { return m_id; } }
        public int count { get { return m_count; } }
        public GraphicsBuffer buffer { get { return m_buffer; } }
        public void Dispose()
        {
            if (m_valid)
            {
                var destroyInfo = new Handle<ResourceDestroyInfo>(new ResourceDestroyInfo(m_context, m_id));
                CommandBuffer cb = new CommandBuffer();
                cb.IssuePluginEventAndData(NI.GetPluginEventFunction(), ((int)ResourceDestroyInfo.eventType), destroyInfo.ptr());
                Graphics.ExecuteCommandBuffer(cb);
                m_valid = false;
                m_buffer.Dispose();
                m_buffer = null;
            }
        }
    }
}
