using System.Collections;

namespace SafeCollections
{
    public class SafeDictionary<TKey, TValue> :
        IDictionary<TKey, TValue>
    {
        // the internal collection that we are protecting
        readonly Dictionary<TKey, TValue> dict;

        // initial thread id to guard against access from other threads
        readonly int initialThreadId;

        public SafeDictionary()
        {
            dict = new Dictionary<TKey, TValue>();
            initialThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        protected void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != initialThreadId)
                throw new InvalidOperationException($"{nameof(SafeDictionary<TKey, TValue>)} Race Condition detected: it was created from ThreadId={initialThreadId} but accessed from ThreadId={Thread.CurrentThread.ManagedThreadId}. This will cause undefined state, please debug your code to always access this from the same thread.");
        }

        // IDictionary ////////////////////////////////////////////////////////
        public int Count
        {
            get
            {
                CheckThread();
                return dict.Count;
            }
        }
        public bool IsReadOnly => false;

        public ICollection<TKey> Keys
        {
            get
            {
                CheckThread();
                return dict.Keys;
            }
        }
        public ICollection<TValue> Values
        {
            get
            {
                CheckThread();
                return dict.Values;
            }
        }

        public void Add(TKey key, TValue value)
        {
            CheckThread();
            dict.Add(key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            CheckThread();
            dict.Add(item.Key, item.Value);
        }

        public bool Remove(TKey key)
        {
            CheckThread();
            return dict.Remove(key);
        }

        public void Clear()
        {
            CheckThread();
            dict.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            CheckThread();
            return dict.Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            CheckThread();
            return dict.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            CheckThread();
            return dict.TryGetValue(key, out value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            CheckThread();
            return dict.Remove(item.Key);
        }

        public TValue this[TKey key]
        {
            get
            {
                CheckThread();
                return dict[key];
            }
            set
            {
                CheckThread();
                dict[key] = value;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            CheckThread();
            return dict.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            CheckThread();
            return dict.GetEnumerator();
        }
        ////////////////////////////////////////////////////////////////////////
    }
}
