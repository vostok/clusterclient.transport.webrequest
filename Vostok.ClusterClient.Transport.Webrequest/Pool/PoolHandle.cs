using System;

namespace Vostok.Clusterclient.Transport.Webrequest.Pool
{
    internal struct PoolHandle<T> : IDisposable
        where T : class
    {
        private readonly IPool<T> pool;

        public PoolHandle(IPool<T> pool, T resource)
        {
            this.pool = pool;
            Resource = resource;
        }

        public T Resource { get; }

        public void Dispose() => pool.Return(Resource);

        public static implicit operator T(PoolHandle<T> handle) => handle.Resource;
    }
}