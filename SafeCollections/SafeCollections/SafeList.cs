using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SafeCollections
{
    public class SafeList<T> : IList<T>
    {
        // the internal collection that we are protecting
        readonly List<T> list;

        // initial thread id to guard against access from other threads
        int lastThreadId;

        public SafeList()
        {
            list = new List<T>();
            lastThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        protected void CheckThread()
        {
            // detect acces from another thread
            if (Thread.CurrentThread.ManagedThreadId != lastThreadId)
                throw new InvalidOperationException($"{nameof(SafeList<T>)} Race Condition detected: it was last accessed from ThreadId={lastThreadId} but now accessed from ThreadId={Thread.CurrentThread.ManagedThreadId}. This will cause undefined state, please debug your code to always access this from the same thread.");

            // update last accessed thread id to always log last->current
            // instead of initial->current.
            // for example, setup may run from thread A, and main loop from thread B.
            // in that case we still want to detect thread C accessing after thread B.
            lastThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        // IList<T> ////////////////////////////////////////////////////////////
        public void Add(T item)
        {
            CheckThread();
            list.Add(item);
        }

        public void Clear()
        {
            CheckThread();
            list.Clear();
        }

        public bool Contains(T item)
        {
            CheckThread();
            return list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex) => throw new NotImplementedException();

        public bool Remove(T item)
        {
            CheckThread();
            return list.Remove(item);
        }

        public int Count
        {
            get
            {
                CheckThread();
                return list.Count;
            }
        }

        public bool IsReadOnly => false;

        public int IndexOf(T item)
        {
            CheckThread();
            return list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            CheckThread();
            list.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            CheckThread();
            list.RemoveAt(index);
        }

        public T this[int index]
        {
            get
            {
                CheckThread();
                return list[index];
            }
            set
            {
                CheckThread();
                list[index] = value;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            CheckThread();
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            CheckThread();
            return list.GetEnumerator();
        }
        ////////////////////////////////////////////////////////////////////////
    }
}
