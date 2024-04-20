using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SafeCollections
{
    public class SafeHashSet<T> : ISet<T>
    {
        // the internal collection that we are protecting
        readonly HashSet<T> hashset;

        // initial thread id to guard against access from other threads
        int lastThreadId;

        public SafeHashSet()
        {
            hashset = new HashSet<T>();
            lastThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        protected void CheckThread()
        {
            // detect acces from another thread
            if (Thread.CurrentThread.ManagedThreadId != lastThreadId)
                throw new InvalidOperationException($"{nameof(SafeHashSet<T>)} Race Condition detected: it was last accessed from ThreadId={lastThreadId} but now accessed from ThreadId={Thread.CurrentThread.ManagedThreadId}. This will cause undefined state, please debug your code to always access this from the same thread.");

            // update last accessed thread id to always log last->current
            // instead of initial->current.
            // for example, setup may run from thread A, and main loop from thread B.
            // in that case we still want to detect thread C accessing after thread B.
            lastThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        // ISet<T> /////////////////////////////////////////////////////////////
        public int Count
        {
            get
            {
                CheckThread();
                return hashset.Count;
            }
        }

        public bool IsReadOnly => false;

        void ICollection<T>.Add(T item)
        {
            CheckThread();
            hashset.Add(item);
        }

        public void ExceptWith(IEnumerable<T> other)
        {
            CheckThread();
            hashset.ExceptWith(other);
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            CheckThread();
            hashset.IntersectWith(other);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            CheckThread();
            return hashset.IsProperSubsetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            CheckThread();
            return hashset.IsProperSupersetOf(other);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            CheckThread();
            return hashset.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            CheckThread();
            return hashset.IsSupersetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            CheckThread();
            return hashset.Overlaps(other);
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            CheckThread();
            return hashset.SetEquals(other);
        }

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            CheckThread();
            hashset.SymmetricExceptWith(other);
        }

        public void UnionWith(IEnumerable<T> other)
        {
            CheckThread();
            hashset.UnionWith(other);
        }

        bool ISet<T>.Add(T item)
        {
            CheckThread();
            return hashset.Add(item);
        }

        public void Clear()
        {
            CheckThread();
            hashset.Clear();
        }

        public bool Contains(T item)
        {
            CheckThread();
            return hashset.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            CheckThread();
            hashset.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            CheckThread();
            return hashset.Remove(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            CheckThread();
            return hashset.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            CheckThread();
            return ((IEnumerable)hashset).GetEnumerator();
        }
        // HashSet<T> //////////////////////////////////////////////////////////
        public bool Add(T item)
        {
            CheckThread();
            return hashset.Add(item);
        }

        public bool TryGetValue(T equalValue, out T actualValue)
        {
            CheckThread();
            return hashset.TryGetValue(equalValue, out actualValue);
        }

    }
}
