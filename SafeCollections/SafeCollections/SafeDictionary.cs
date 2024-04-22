// based on Unity's C# HashSet<T>, which is based on Mono C#.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;

namespace SafeCollections
{
  [DebuggerDisplay("Count = {Count}")]
    [Serializable]
  public class SafeDictionary<TKey, TValue> :
    SafeCollection,
    IDictionary<TKey, TValue>,
    ICollection<KeyValuePair<TKey, TValue>>,
    IEnumerable<KeyValuePair<TKey, TValue>>,
    IEnumerable,
    IDictionary,
    ICollection,
    IReadOnlyDictionary<TKey, TValue>,
    IReadOnlyCollection<KeyValuePair<TKey, TValue>>
    // ISerializable,
    // IDeserializationCallback
  {
    private int[] buckets;
    private SafeDictionary<TKey, TValue>.Entry[] entries;
    private int count;
    private int version;
    private int freeList;
    private int freeCount;
    private IEqualityComparer<TKey> comparer;
    private SafeDictionary<TKey, TValue>.KeyCollection keys;
    private SafeDictionary<TKey, TValue>.ValueCollection values;
    private object _syncRoot;
    private const string VersionName = "Version";
    private const string HashSizeName = "HashSize";
    private const string KeyValuePairsName = "KeyValuePairs";
    private const string ComparerName = "Comparer";

    public SafeDictionary()
      : this(0, (IEqualityComparer<TKey>) null)
    {
    }

    public SafeDictionary(int capacity)
      : this(capacity, (IEqualityComparer<TKey>) null)
    {
    }

    public SafeDictionary(IEqualityComparer<TKey> comparer)
      : this(0, comparer)
    {
    }

    public SafeDictionary(int capacity, IEqualityComparer<TKey> comparer)
    {
      if (capacity < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.capacity");
      if (capacity > 0)
        this.Initialize(capacity);
      this.comparer = comparer ?? (IEqualityComparer<TKey>) EqualityComparer<TKey>.Default;
    }

    public SafeDictionary(IDictionary<TKey, TValue> dictionary)
      : this(dictionary, (IEqualityComparer<TKey>) null)
    {
    }

    public SafeDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
      : this(dictionary != null ? dictionary.Count : 0, comparer)
    {
      if (dictionary == null)
        throw new ArgumentNullException("ExceptionArgument.dictionary");
      foreach (KeyValuePair<TKey, TValue> keyValuePair in (IEnumerable<KeyValuePair<TKey, TValue>>) dictionary)
        this.Add(keyValuePair.Key, keyValuePair.Value);
    }

    // protected SafeDictionary(SerializationInfo info, StreamingContext context) => HashHelpers.SerializationInfoTable.Add((object) this, info);
    //
    //     public IEqualityComparer<TKey> Comparer
    // {
    //   get => this.comparer;
    // }

    public int Count
    {
      get => this.count - this.freeCount;
    }

    public SafeDictionary<TKey, TValue>.KeyCollection Keys
    {
      get
      {
        if (this.keys == null)
          this.keys = new SafeDictionary<TKey, TValue>.KeyCollection(this);
        return this.keys;
      }
    }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys
    {
      get
      {
        if (this.keys == null)
          this.keys = new SafeDictionary<TKey, TValue>.KeyCollection(this);
        return (ICollection<TKey>) this.keys;
      }
    }

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys
    {
      get
      {
        if (this.keys == null)
          this.keys = new SafeDictionary<TKey, TValue>.KeyCollection(this);
        return (IEnumerable<TKey>) this.keys;
      }
    }

    public SafeDictionary<TKey, TValue>.ValueCollection Values
    {
      get
      {
        if (this.values == null)
          this.values = new SafeDictionary<TKey, TValue>.ValueCollection(this);
        return this.values;
      }
    }

    ICollection<TValue> IDictionary<TKey, TValue>.Values
    {
      get
      {
        if (this.values == null)
          this.values = new SafeDictionary<TKey, TValue>.ValueCollection(this);
        return (ICollection<TValue>) this.values;
      }
    }

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values
    {
      get
      {
        if (this.values == null)
          this.values = new SafeDictionary<TKey, TValue>.ValueCollection(this);
        return (IEnumerable<TValue>) this.values;
      }
    }

    public TValue this[TKey key]
    {
      get
      {
        int entry = this.FindEntry(key);
        if (entry >= 0)
          return this.entries[entry].value;
        throw new KeyNotFoundException();
        return default (TValue);
      }
      set
      {
        this.Insert(key, value, false);
      }
    }

    public void Add(TKey key, TValue value) => this.Insert(key, value, true);

    void ICollection<KeyValuePair<TKey, TValue>>.Add(
      KeyValuePair<TKey, TValue> keyValuePair)
    {
      this.Add(keyValuePair.Key, keyValuePair.Value);
    }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(
      KeyValuePair<TKey, TValue> keyValuePair)
    {
      int entry = this.FindEntry(keyValuePair.Key);
      return entry >= 0 && EqualityComparer<TValue>.Default.Equals(this.entries[entry].value, keyValuePair.Value);
    }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(
      KeyValuePair<TKey, TValue> keyValuePair)
    {
      int entry = this.FindEntry(keyValuePair.Key);
      if (entry < 0 || !EqualityComparer<TValue>.Default.Equals(this.entries[entry].value, keyValuePair.Value))
        return false;
      this.Remove(keyValuePair.Key);
      return true;
    }

    public void Clear()
    {
      if (this.count <= 0)
        return;
      for (int index = 0; index < this.buckets.Length; ++index)
        this.buckets[index] = -1;
      Array.Clear((Array) this.entries, 0, this.count);
      this.freeList = -1;
      this.count = 0;
      this.freeCount = 0;
      ++this.version;
      OnVersionChanged(); // CUSTOM CHANGE
    }

    public bool ContainsKey(TKey key) => this.FindEntry(key) >= 0;

    public bool ContainsValue(TValue value)
    {
      if ((object) value == null)
      {
        for (int index = 0; index < this.count; ++index)
        {
          if (this.entries[index].hashCode >= 0 && (object) this.entries[index].value == null)
            return true;
        }
      }
      else
      {
        EqualityComparer<TValue> equalityComparer = EqualityComparer<TValue>.Default;
        for (int index = 0; index < this.count; ++index)
        {
          if (this.entries[index].hashCode >= 0 && equalityComparer.Equals(this.entries[index].value, value))
            return true;
        }
      }
      return false;
    }

    private void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
    {
      if (array == null)
        throw new ArgumentNullException("ExceptionArgument.array");
      if (index < 0 || index > array.Length)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (array.Length - index < this.Count)
        throw new ArgumentException("ExceptionResource.Arg_ArrayPlusOffTooSmall");
      int count = this.count;
      SafeDictionary<TKey, TValue>.Entry[] entries = this.entries;
      for (int index1 = 0; index1 < count; ++index1)
      {
        if (entries[index1].hashCode >= 0)
          array[index++] = new KeyValuePair<TKey, TValue>(entries[index1].key, entries[index1].value);
      }
    }

    public SafeDictionary<TKey, TValue>.Enumerator GetEnumerator()
    {
      return new SafeDictionary<TKey, TValue>.Enumerator(this, 2);
    }

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
    {
      return (IEnumerator<KeyValuePair<TKey, TValue>>) new SafeDictionary<TKey, TValue>.Enumerator(this, 2);
    }

    public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      if (info == null)
        throw new ArgumentNullException("ExceptionArgument.info");
      info.AddValue("Version", this.version);
      // info.AddValue("Comparer", HashHelpers.GetEqualityComparerForSerialization((object) this.comparer), typeof (IEqualityComparer<TKey>));
      info.AddValue("HashSize", this.buckets == null ? 0 : this.buckets.Length);
      if (this.buckets == null)
        return;
      KeyValuePair<TKey, TValue>[] array = new KeyValuePair<TKey, TValue>[this.Count];
      this.CopyTo(array, 0);
      info.AddValue("KeyValuePairs", (object) array, typeof (KeyValuePair<TKey, TValue>[]));
    }

    private int FindEntry(TKey key)
    {
      if ((object) key == null)
        throw new ArgumentNullException("ExceptionArgument.key");
      if (this.buckets != null)
      {
        int num = this.comparer.GetHashCode(key) & int.MaxValue;
        for (int entry = this.buckets[num % this.buckets.Length]; entry >= 0; entry = this.entries[entry].next)
        {
          if (this.entries[entry].hashCode == num && this.comparer.Equals(this.entries[entry].key, key))
            return entry;
        }
      }
      return -1;
    }

    private void Initialize(int capacity)
    {
      int prime = HashHelpers.GetPrime(capacity);
      this.buckets = new int[prime];
      for (int index = 0; index < this.buckets.Length; ++index)
        this.buckets[index] = -1;
      this.entries = new SafeDictionary<TKey, TValue>.Entry[prime];
      this.freeList = -1;
    }

    private void Insert(TKey key, TValue value, bool add)
    {
      if ((object) key == null)
        throw new ArgumentNullException("ExceptionArgument.key");
      if (this.buckets == null)
        this.Initialize(0);
      int num1 = this.comparer.GetHashCode(key) & int.MaxValue;
      int index1 = num1 % this.buckets.Length;
      int num2 = 0;
      for (int index2 = this.buckets[index1]; index2 >= 0; index2 = this.entries[index2].next)
      {
        if (this.entries[index2].hashCode == num1 && this.comparer.Equals(this.entries[index2].key, key))
        {
          if (add)
            throw new ArgumentException("ExceptionResource.Argument_AddingDuplicate");
          this.entries[index2].value = value;
          ++this.version;
          OnVersionChanged(); // CUSTOM CHANGE
          return;
        }
        ++num2;
      }
      int index3;
      if (this.freeCount > 0)
      {
        index3 = this.freeList;
        this.freeList = this.entries[index3].next;
        --this.freeCount;
      }
      else
      {
        if (this.count == this.entries.Length)
        {
          this.Resize();
          index1 = num1 % this.buckets.Length;
        }
        index3 = this.count;
        ++this.count;
      }
      this.entries[index3].hashCode = num1;
      this.entries[index3].next = this.buckets[index1];
      this.entries[index3].key = key;
      this.entries[index3].value = value;
      this.buckets[index1] = index3;
      ++this.version;
      OnVersionChanged(); // CUSTOM CHANGE
      if (num2 <= 100/* || !HashHelpers.IsWellKnownEqualityComparer((object) this.comparer)*/)
        return;
      // this.comparer = (IEqualityComparer<TKey>) HashHelpers.GetRandomizedEqualityComparer((object) this.comparer);
      this.Resize(this.entries.Length, true);
    }

    /*
    public virtual void OnDeserialization(object sender)
    {
      SerializationInfo serializationInfo;
      HashHelpers.SerializationInfoTable.TryGetValue((object) this, out serializationInfo);
      if (serializationInfo == null)
        return;
      int int32_1 = serializationInfo.GetInt32("Version");
      int int32_2 = serializationInfo.GetInt32("HashSize");
      this.comparer = (IEqualityComparer<TKey>) serializationInfo.GetValue("Comparer", typeof (IEqualityComparer<TKey>));
      if (int32_2 != 0)
      {
        this.buckets = new int[int32_2];
        for (int index = 0; index < this.buckets.Length; ++index)
          this.buckets[index] = -1;
        this.entries = new SafeDictionary<TKey, TValue>.Entry[int32_2];
        this.freeList = -1;
        KeyValuePair<TKey, TValue>[] keyValuePairArray = (KeyValuePair<TKey, TValue>[]) serializationInfo.GetValue("KeyValuePairs", typeof (KeyValuePair<TKey, TValue>[]));
        if (keyValuePairArray == null)
          throw new SerializationException("ExceptionResource.Serialization_MissingKeys");
        for (int index = 0; index < keyValuePairArray.Length; ++index)
        {
          if ((object) keyValuePairArray[index].Key == null)
            throw new SerializationException("ExceptionResource.Serialization_NullKey");
          this.Insert(keyValuePairArray[index].Key, keyValuePairArray[index].Value, true);
        }
      }
      else
        this.buckets = (int[]) null;
      this.version = int32_1;
      HashHelpers.SerializationInfoTable.Remove((object) this);
    }
    */

    private void Resize() => this.Resize(HashHelpers.ExpandPrime(this.count), false);

    private void Resize(int newSize, bool forceNewHashCodes)
    {
      int[] numArray = new int[newSize];
      for (int index = 0; index < numArray.Length; ++index)
        numArray[index] = -1;
      SafeDictionary<TKey, TValue>.Entry[] destinationArray = new SafeDictionary<TKey, TValue>.Entry[newSize];
      Array.Copy((Array) this.entries, 0, (Array) destinationArray, 0, this.count);
      if (forceNewHashCodes)
      {
        for (int index = 0; index < this.count; ++index)
        {
          if (destinationArray[index].hashCode != -1)
            destinationArray[index].hashCode = this.comparer.GetHashCode(destinationArray[index].key) & int.MaxValue;
        }
      }
      for (int index1 = 0; index1 < this.count; ++index1)
      {
        if (destinationArray[index1].hashCode >= 0)
        {
          int index2 = destinationArray[index1].hashCode % newSize;
          destinationArray[index1].next = numArray[index2];
          numArray[index2] = index1;
        }
      }
      this.buckets = numArray;
      this.entries = destinationArray;
    }

    public bool Remove(TKey key)
    {
      if ((object) key == null)
        throw new ArgumentNullException("ExceptionArgument.key");
      if (this.buckets != null)
      {
        int num = this.comparer.GetHashCode(key) & int.MaxValue;
        int index1 = num % this.buckets.Length;
        int index2 = -1;
        for (int index3 = this.buckets[index1]; index3 >= 0; index3 = this.entries[index3].next)
        {
          if (this.entries[index3].hashCode == num && this.comparer.Equals(this.entries[index3].key, key))
          {
            if (index2 < 0)
              this.buckets[index1] = this.entries[index3].next;
            else
              this.entries[index2].next = this.entries[index3].next;
            this.entries[index3].hashCode = -1;
            this.entries[index3].next = this.freeList;
            this.entries[index3].key = default (TKey);
            this.entries[index3].value = default (TValue);
            this.freeList = index3;
            ++this.freeCount;
            ++this.version;
            OnVersionChanged(); // CUSTOM CHANGE
            return true;
          }
          index2 = index3;
        }
      }
      return false;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
      int entry = this.FindEntry(key);
      if (entry >= 0)
      {
        value = this.entries[entry].value;
        return true;
      }
      value = default (TValue);
      return false;
    }

    internal TValue GetValueOrDefault(TKey key)
    {
      int entry = this.FindEntry(key);
      return entry >= 0 ? this.entries[entry].value : default (TValue);
    }

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
    {
      get => false;
    }

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(
      KeyValuePair<TKey, TValue>[] array,
      int index)
    {
      this.CopyTo(array, index);
    }

    void ICollection.CopyTo(Array array, int index)
    {
      if (array == null)
        throw new ArgumentNullException("ExceptionArgument.array");
      if (array.Rank != 1)
        throw new ArgumentException("ExceptionResource.Arg_RankMultiDimNotSupported");
      if (array.GetLowerBound(0) != 0)
        throw new ArgumentException("ExceptionResource.Arg_NonZeroLowerBound");
      if (index < 0 || index > array.Length)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (array.Length - index < this.Count)
        throw new ArgumentException("ExceptionResource.Arg_ArrayPlusOffTooSmall");
      switch (array)
      {
        case KeyValuePair<TKey, TValue>[] array1:
          this.CopyTo(array1, index);
          break;
        case DictionaryEntry[] _:
          DictionaryEntry[] dictionaryEntryArray = array as DictionaryEntry[];
          SafeDictionary<TKey, TValue>.Entry[] entries1 = this.entries;
          for (int index1 = 0; index1 < this.count; ++index1)
          {
            if (entries1[index1].hashCode >= 0)
              dictionaryEntryArray[index++] = new DictionaryEntry((object) entries1[index1].key, (object) entries1[index1].value);
          }
          break;
        case object[] objArray:
label_18:
          try
          {
            int count = this.count;
            SafeDictionary<TKey, TValue>.Entry[] entries2 = this.entries;
            for (int index2 = 0; index2 < count; ++index2)
            {
              if (entries2[index2].hashCode >= 0)
              {
                int index3 = index++;
                // ISSUE: variable of a boxed type
                var local = (ValueType) new KeyValuePair<TKey, TValue>(entries2[index2].key, entries2[index2].value);
                objArray[index3] = (object) local;
              }
            }
            break;
          }
          catch (ArrayTypeMismatchException ex)
          {
            throw new ArgumentException("ExceptionResource.Argument_InvalidArrayType");
            break;
          }
        default:
          throw new ArgumentException("ExceptionResource.Argument_InvalidArrayType");
          goto label_18;
      }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return (IEnumerator) new SafeDictionary<TKey, TValue>.Enumerator(this, 2);
      
    }

    bool ICollection.IsSynchronized
    {
      get => false;
    }

    object ICollection.SyncRoot
    {
      get
      {
        if (this._syncRoot == null)
          Interlocked.CompareExchange<object>(ref this._syncRoot, new object(), (object) null);
        return this._syncRoot;
      }
    }

        bool IDictionary.IsFixedSize
    {
      get => false;
    }

        bool IDictionary.IsReadOnly
    {
      get => false;
    }

        ICollection IDictionary.Keys
    {
      get
      {
        return (ICollection) this.Keys;
      } 
    }

    ICollection IDictionary.Values
    {
      get
      {
        return (ICollection) this.Values;
      } 
    }

    object IDictionary.this[object key]
    {
      get
      {
        if (SafeDictionary<TKey, TValue>.IsCompatibleKey(key))
        {
          int entry = this.FindEntry((TKey) key);
          if (entry >= 0)
            return (object) this.entries[entry].value;
        }
        return (object) null;
      }
      set
      {
        if (key == null)
          throw new ArgumentNullException("ExceptionArgument.key");
        // ThrowHelper.IfNullAndNullsAreIllegalThenThrow<TValue>(value, ExceptionArgument.value);
        try
        {
          TKey key1 = (TKey) key;
          try
          {
            this[key1] = (TValue) value;
          }
          catch (InvalidCastException ex)
          {
            throw new ArgumentException($"WrongValue: value, typeof (TValue)");
          }
        }
        catch (InvalidCastException ex)
        {
          throw new ArgumentException($"WrongKeyType: key, typeof (TKey)");
        }
      }
    }

    private static bool IsCompatibleKey(object key)
    {
      if (key == null)
        throw new ArgumentNullException("ExceptionArgument.key");
      return key is TKey;
    }

        void IDictionary.Add(object key, object value)
    {
      if (key == null)
        throw new ArgumentNullException("ExceptionArgument.key");
      // ThrowHelper.IfNullAndNullsAreIllegalThenThrow<TValue>(value, ExceptionArgument.value);
      try
      {
        TKey key1 = (TKey) key;
        try
        {
          this.Add(key1, (TValue) value);
        }
        catch (InvalidCastException ex)
        {
          throw new ArgumentException($"WrongValue: value, typeof (TValue)");
        }
      }
      catch (InvalidCastException ex)
      {
        throw new ArgumentException($"WrongKeyType: key, typeof (TKey)");
      }
    }

        bool IDictionary.Contains(object key) => SafeDictionary<TKey, TValue>.IsCompatibleKey(key) && this.ContainsKey((TKey) key);

        IDictionaryEnumerator IDictionary.GetEnumerator() => (IDictionaryEnumerator) new SafeDictionary<TKey, TValue>.Enumerator(this, 1);

        void IDictionary.Remove(object key)
    {
      if (!SafeDictionary<TKey, TValue>.IsCompatibleKey(key))
        return;
      this.Remove((TKey) key);
    }

    private struct Entry
    {
      public int hashCode;
      public int next;
      public TKey key;
      public TValue value;
    }

        [Serializable]
    public struct Enumerator : 
      IEnumerator<KeyValuePair<TKey, TValue>>,
      IDisposable,
      IEnumerator,
      IDictionaryEnumerator
    {
      private SafeDictionary<TKey, TValue> dictionary;
      private int version;
      private int index;
      private KeyValuePair<TKey, TValue> current;
      private int getEnumeratorRetType;
      internal const int DictEntry = 1;
      internal const int KeyValuePair = 2;

      private Guid uniqueId; // CUSTOM CHANGE

      internal Enumerator(SafeDictionary<TKey, TValue> dictionary, int getEnumeratorRetType)
      {
        this.dictionary = dictionary;
        this.version = dictionary.version;
        this.index = 0;
        this.getEnumeratorRetType = getEnumeratorRetType;
        this.current = new KeyValuePair<TKey, TValue>();

        this.uniqueId = Guid.NewGuid();
        dictionary.BeginEnumerating(this.uniqueId); // CUSTOM CHANGE
      }

      public bool MoveNext()
      {
        if (this.version != this.dictionary.version)
          throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumFailedVersion");
        for (; (uint) this.index < (uint) this.dictionary.count; ++this.index)
        {
          if (this.dictionary.entries[this.index].hashCode >= 0)
          {
            this.current = new KeyValuePair<TKey, TValue>(this.dictionary.entries[this.index].key, this.dictionary.entries[this.index].value);
            ++this.index;
            return true;
          }
        }
        this.index = this.dictionary.count + 1;
        this.current = new KeyValuePair<TKey, TValue>();
        return false;
      }

      public KeyValuePair<TKey, TValue> Current
      {
        get => this.current;
      }


    public void Dispose()
    {
      dictionary.EndEnumerating(this.uniqueId); // CUSTOM CHANGE
    }

      object IEnumerator.Current
      {
        get
        {
          if (this.index == 0 || this.index == this.dictionary.count + 1)
            throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumOpCantHappen");
          return this.getEnumeratorRetType == 1 ? (object) new DictionaryEntry((object) this.current.Key, (object) this.current.Value) : (object) new KeyValuePair<TKey, TValue>(this.current.Key, this.current.Value);
        }
      }

      void IEnumerator.Reset()
      {
        if (this.version != this.dictionary.version)
          throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumFailedVersion");
        this.index = 0;
        this.current = new KeyValuePair<TKey, TValue>();
      }

            DictionaryEntry IDictionaryEnumerator.Entry
      {
        get
        {
          if (this.index == 0 || this.index == this.dictionary.count + 1)
            throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumOpCantHappen");
          return new DictionaryEntry((object) this.current.Key, (object) this.current.Value);
        }
      }

            object IDictionaryEnumerator.Key
      {
        get
        {
          if (this.index == 0 || this.index == this.dictionary.count + 1)
            throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumOpCantHappen");
          return (object) this.current.Key;
        }
      }

            object IDictionaryEnumerator.Value
      {
        get
        {
          if (this.index == 0 || this.index == this.dictionary.count + 1)
            throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumOpCantHappen");
          return (object) this.current.Value;
        }
      }
    }

    [DebuggerDisplay("Count = {Count}")]
        [Serializable]
    public sealed class KeyCollection : 
      ICollection<TKey>,
      IEnumerable<TKey>,
      IEnumerable,
      ICollection,
      IReadOnlyCollection<TKey>
    {
      private SafeDictionary<TKey, TValue> dictionary;

      public KeyCollection(SafeDictionary<TKey, TValue> dictionary)
      {
        if (dictionary == null)
          throw new ArgumentNullException("ExceptionArgument.dictionary");
        this.dictionary = dictionary;
      }

      public SafeDictionary<TKey, TValue>.KeyCollection.Enumerator GetEnumerator()
      {
        
        return new SafeDictionary<TKey, TValue>.KeyCollection.Enumerator(this.dictionary);
      }

      public void CopyTo(TKey[] array, int index)
      {
        if (array == null)
          throw new ArgumentNullException("ExceptionArgument.array");
        if (index < 0 || index > array.Length)
          throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
        if (array.Length - index < this.dictionary.Count)
          throw new ArgumentException("ExceptionResource.Arg_ArrayPlusOffTooSmall");
        int count = this.dictionary.count;
        SafeDictionary<TKey, TValue>.Entry[] entries = this.dictionary.entries;
        for (int index1 = 0; index1 < count; ++index1)
        {
          if (entries[index1].hashCode >= 0)
            array[index++] = entries[index1].key;
        }
      }

      public int Count
      {
        get => this.dictionary.Count;
      }

      bool ICollection<TKey>.IsReadOnly
      {
        get => true;
      }

      void ICollection<TKey>.Add(TKey item) => throw new NotSupportedException("ExceptionResource.NotSupported_KeyCollectionSet");

      void ICollection<TKey>.Clear() => throw new NotSupportedException("ExceptionResource.NotSupported_KeyCollectionSet");

      bool ICollection<TKey>.Contains(TKey item) => this.dictionary.ContainsKey(item);

      bool ICollection<TKey>.Remove(TKey item)
      {
        throw new NotSupportedException("ExceptionResource.NotSupported_KeyCollectionSet");
        return false;
      }

      IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() 
      {
          return (IEnumerator<TKey>) new SafeDictionary<TKey, TValue>.KeyCollection.Enumerator(this.dictionary);
      }

      IEnumerator IEnumerable.GetEnumerator() 
      {
          return (IEnumerator) new SafeDictionary<TKey, TValue>.KeyCollection.Enumerator(this.dictionary);
      }

      void ICollection.CopyTo(Array array, int index)
      {
        if (array == null)
          throw new ArgumentNullException("ExceptionArgument.array");
        if (array.Rank != 1)
          throw new ArgumentException("ExceptionResource.Arg_RankMultiDimNotSupported");
        if (array.GetLowerBound(0) != 0)
          throw new ArgumentException("ExceptionResource.Arg_NonZeroLowerBound");
        if (index < 0 || index > array.Length)
          throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
        if (array.Length - index < this.dictionary.Count)
          throw new ArgumentException("ExceptionResource.Arg_ArrayPlusOffTooSmall");
        switch (array)
        {
          case TKey[] array1:
            this.CopyTo(array1, index);
            break;
          case object[] objArray:
label_13:
            int count = this.dictionary.count;
            SafeDictionary<TKey, TValue>.Entry[] entries = this.dictionary.entries;
            try
            {
              for (int index1 = 0; index1 < count; ++index1)
              {
                if (entries[index1].hashCode >= 0)
                {
                  int index2 = index++;
                  // ISSUE: variable of a boxed type
                  var key = (object) entries[index1].key;
                  objArray[index2] = (object) key;
                }
              }
              break;
            }
            catch (ArrayTypeMismatchException ex)
            {
              throw new ArgumentException("ExceptionResource.Argument_InvalidArrayType");
              break;
            }
          default:
            throw new ArgumentException("ExceptionResource.Argument_InvalidArrayType");
            goto label_13;
        }
      }

      bool ICollection.IsSynchronized
      {
        get => false;
      }

            object ICollection.SyncRoot
      {
        get => ((ICollection) this.dictionary).SyncRoot;
      }

            [Serializable]
      public struct Enumerator : IEnumerator<TKey>, IDisposable, IEnumerator
      {
        private SafeDictionary<TKey, TValue> dictionary;
        private int index;
        private int version;
        private TKey currentKey;
        private Guid uniqueId; // CUSTOM CHANGE

        internal Enumerator(SafeDictionary<TKey, TValue> dictionary)
        {
          this.dictionary = dictionary;
          this.version = dictionary.version;
          this.index = 0;
          this.currentKey = default (TKey);

          this.uniqueId = Guid.NewGuid();
          dictionary.BeginEnumerating(this.uniqueId); // CUSTOM CHANGE
        }

      public void Dispose()
        {
          dictionary.EndEnumerating(this.uniqueId); // CUSTOM CHANGE
        }

      public bool MoveNext()
        {
          if (this.version != this.dictionary.version)
            throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumFailedVersion");
          for (; (uint) this.index < (uint) this.dictionary.count; ++this.index)
          {
            if (this.dictionary.entries[this.index].hashCode >= 0)
            {
              this.currentKey = this.dictionary.entries[this.index].key;
              ++this.index;
              return true;
            }
          }
          this.index = this.dictionary.count + 1;
          this.currentKey = default (TKey);
          return false;
        }

      public TKey Current
        {
          get => this.currentKey;
        }

                object IEnumerator.Current
        {
          get
          {
            if (this.index == 0 || this.index == this.dictionary.count + 1)
              throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumOpCantHappen");
            return (object) this.currentKey;
          }
        }

          void IEnumerator.Reset()
        {
          if (this.version != this.dictionary.version)
            throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumFailedVersion");
          this.index = 0;
          this.currentKey = default (TKey);
        }
      }
    }

    [DebuggerDisplay("Count = {Count}")]
        [Serializable]
    public sealed class ValueCollection : 
      ICollection<TValue>,
      IEnumerable<TValue>,
      IEnumerable,
      ICollection,
      IReadOnlyCollection<TValue>
    {
      private SafeDictionary<TKey, TValue> dictionary;

      public ValueCollection(SafeDictionary<TKey, TValue> dictionary)
      {
        if (dictionary == null)
          throw new ArgumentNullException("ExceptionArgument.dictionary");
        this.dictionary = dictionary;
      }

      public SafeDictionary<TKey, TValue>.ValueCollection.Enumerator GetEnumerator()
      {
        return new SafeDictionary<TKey, TValue>.ValueCollection.Enumerator(this.dictionary);
      }

      public void CopyTo(TValue[] array, int index)
      {
        if (array == null)
          throw new ArgumentNullException("ExceptionArgument.array");
        if (index < 0 || index > array.Length)
          throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
        if (array.Length - index < this.dictionary.Count)
          throw new ArgumentException("ExceptionResource.Arg_ArrayPlusOffTooSmall");
        int count = this.dictionary.count;
        SafeDictionary<TKey, TValue>.Entry[] entries = this.dictionary.entries;
        for (int index1 = 0; index1 < count; ++index1)
        {
          if (entries[index1].hashCode >= 0)
            array[index++] = entries[index1].value;
        }
      }

      public int Count
      {
        get => this.dictionary.Count;
      }

      bool ICollection<TValue>.IsReadOnly
      {
        get => true;
      }

      void ICollection<TValue>.Add(TValue item) => throw new NotSupportedException("ExceptionResource.NotSupported_ValueCollectionSet");

      bool ICollection<TValue>.Remove(TValue item)
      {
        throw new NotSupportedException("ExceptionResource.NotSupported_ValueCollectionSet");
        return false;
      }

      void ICollection<TValue>.Clear() => throw new NotSupportedException("ExceptionResource.NotSupported_ValueCollectionSet");

      bool ICollection<TValue>.Contains(TValue item) => this.dictionary.ContainsValue(item);

      IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() 
     {
      return (IEnumerator<TValue>) new SafeDictionary<TKey, TValue>.ValueCollection.Enumerator(this.dictionary);
     }

      IEnumerator IEnumerable.GetEnumerator() 
     {
      return (IEnumerator) new SafeDictionary<TKey, TValue>.ValueCollection.Enumerator(this.dictionary);
     }

      void ICollection.CopyTo(Array array, int index)
      {
        if (array == null)
          throw new ArgumentNullException("ExceptionArgument.array");
        if (array.Rank != 1)
          throw new ArgumentException("ExceptionResource.Arg_RankMultiDimNotSupported");
        if (array.GetLowerBound(0) != 0)
          throw new ArgumentException("ExceptionResource.Arg_NonZeroLowerBound");
        if (index < 0 || index > array.Length)
          throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
        if (array.Length - index < this.dictionary.Count)
          throw new ArgumentException("ExceptionResource.Arg_ArrayPlusOffTooSmall");
        switch (array)
        {
          case TValue[] array1:
            this.CopyTo(array1, index);
            break;
          case object[] objArray:
label_13:
            int count = this.dictionary.count;
            SafeDictionary<TKey, TValue>.Entry[] entries = this.dictionary.entries;
            try
            {
              for (int index1 = 0; index1 < count; ++index1)
              {
                if (entries[index1].hashCode >= 0)
                {
                  int index2 = index++;
                  // ISSUE: variable of a boxed type
                  var local = (object) entries[index1].value;
                  objArray[index2] = (object) local;
                }
              }
              break;
            }
            catch (ArrayTypeMismatchException ex)
            {
              throw new ArgumentException("ExceptionResource.Argument_InvalidArrayType");
              break;
            }
          default:
            throw new ArgumentException("ExceptionResource.Argument_InvalidArrayType");
            goto label_13;
        }
      }

      bool ICollection.IsSynchronized
      {
        get => false;
      }

            object ICollection.SyncRoot
      {
        get => ((ICollection) this.dictionary).SyncRoot;
      }

            [Serializable]
      public struct Enumerator : IEnumerator<TValue>, IDisposable, IEnumerator
      {
        private SafeDictionary<TKey, TValue> dictionary;
        private int index;
        private int version;
        private TValue currentValue;
        private Guid uniqueId;

        internal Enumerator(SafeDictionary<TKey, TValue> dictionary)
        {
          this.dictionary = dictionary;
          this.version = dictionary.version;
          this.index = 0;
          this.currentValue = default (TValue);

          this.uniqueId = Guid.NewGuid();
          dictionary.BeginEnumerating(this.uniqueId); // CUSTOM CHANGE
        }

      public void Dispose()
        {
          dictionary.EndEnumerating(this.uniqueId); // CUSTOM CHANGE
        }

      public bool MoveNext()
        {
          if (this.version != this.dictionary.version)
            throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumFailedVersion");
          for (; (uint) this.index < (uint) this.dictionary.count; ++this.index)
          {
            if (this.dictionary.entries[this.index].hashCode >= 0)
            {
              this.currentValue = this.dictionary.entries[this.index].value;
              ++this.index;
              return true;
            }
          }
          this.index = this.dictionary.count + 1;
          this.currentValue = default (TValue);
          return false;
        }

      public TValue Current
        {
          get => this.currentValue;
        }

                object IEnumerator.Current
        {
          get
          {
            if (this.index == 0 || this.index == this.dictionary.count + 1)
              throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumOpCantHappen");
            return (object) this.currentValue;
          }
        }

          void IEnumerator.Reset()
        {
          if (this.version != this.dictionary.version)
            throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumFailedVersion");
          this.index = 0;
          this.currentValue = default (TValue);
        }
      }
    }
  }
}

