using System;

namespace Liv.Lck
{
    internal interface ILckStorageWatcher : IDisposable
    {
        bool HasEnoughFreeStorage();
    }
}
