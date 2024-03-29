﻿using System;

namespace Microsoft.Framework.MemoryPool
{
    public class LeasedArraySegment<T> : ILeasedLifetime
    {
        public LeasedArraySegment(ArraySegment<T> data, IArraySegmentPool<T> owner)
        {
            Data = data;
            Owner = owner;
        }

        public ArraySegment<T> Data { get; private set; }

        public IArraySegmentPool<T> Owner { get; private set; }

        void ILeasedLifetime.Destroy()
        {
            Owner = null;
        }
    }
}
