using UnityEngine;

namespace Liv.Lck
{
    internal interface ILckPhotoCapture
    {
        LckResult Capture();
        void SetRenderTexture(RenderTexture renderTexture);
    }
}