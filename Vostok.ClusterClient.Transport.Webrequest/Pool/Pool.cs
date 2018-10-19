using System;
using JetBrains.Annotations;
using Vostok.Commons.Collections;

namespace Vostok.Clusterclient.Transport.Webrequest.Pool
{
    internal class Pool<T> : UnboundedObjectPool<T>, IPool<T>
        where T : class
    {
        public Pool([NotNull] Func<T> itemFactory)
            : base(itemFactory)
        {
        }
    }
}