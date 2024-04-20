// based on Unity's C# HashSet<T>, which is based on Mono C#.

using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace SafeCollections
{
  [DebuggerDisplay("Count = {Count}")]
    [Serializable]
  public class SafeList<T> : 
    IList<T>,
    ICollection<T>,
    IEnumerable<T>,
    IEnumerable,
    IList,
    ICollection,
    IReadOnlyList<T>,
    IReadOnlyCollection<T>
  {
    private const int _defaultCapacity = 4;
    private T[] _items;
    private int _size;
    private int _version;
    [NonSerialized]
    private object _syncRoot;
    private static readonly T[] _emptyArray = new T[0];
    
    // CUSTOM CHANGE: enumerating is set true while enumerating, and false when done enumerating
    private bool enumerating = false;
    
    void CheckEnumerating()
    {
      if (enumerating)
      {
        throw new InvalidOperationException(
          "Attempted to access collection while it's being enumerated elsewhere. This would cause an InvalidOperationException when enumerating, which would cause a race condition which is hard to debug.");
      }
    }
    // END CUSTOM CHANGE

    public SafeList() => this._items = SafeList<T>._emptyArray;

    public SafeList(int capacity)
    {
      if (capacity < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.capacity, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (capacity == 0)
        this._items = SafeList<T>._emptyArray;
      else
        this._items = new T[capacity];
    }

    public SafeList(IEnumerable<T> collection)
    {
      if (collection == null)
        throw new ArgumentNullException("ExceptionArgument.collection");
      if (collection is ICollection<T> objs)
      {
        int count = objs.Count;
        if (count == 0)
        {
          this._items = SafeList<T>._emptyArray;
        }
        else
        {
          this._items = new T[count];
          objs.CopyTo(this._items, 0);
          this._size = count;
        }
      }
      else
      {
        this._size = 0;
        this._items = SafeList<T>._emptyArray;
        foreach (T obj in collection)
          this.Add(obj);
      }
    }

    public int Capacity
    {
      get
      {
        //CheckEnumerating(); // read is okay while iterating
        return this._items.Length;
      }
      set
      {
        CheckEnumerating();

        if (value < this._size)
          throw new ArgumentOutOfRangeException("ExceptionArgument.value, ExceptionResource.ArgumentOutOfRange_SmallCapacity");
        if (value == this._items.Length)
          return;
        if (value > 0)
        {
          T[] destinationArray = new T[value];
          if (this._size > 0)
            Array.Copy((Array) this._items, 0, (Array) destinationArray, 0, this._size);
          this._items = destinationArray;
        }
        else
          this._items = SafeList<T>._emptyArray;
      }
    }

    public int Count
    {
      get
      {
        //CheckEnumerating(); read is okay while iterating
        return this._size;
      }
    }

    bool IList.IsFixedSize
    {
      get => false;
    }

    bool ICollection<T>.IsReadOnly
    {
      get => false;
    }

    bool IList.IsReadOnly
    {
      get => false;
    }

    bool ICollection.IsSynchronized
    {
      get => false;
    }

    object ICollection.SyncRoot
    {
      get
      {
        CheckEnumerating();

        if (this._syncRoot == null)
          Interlocked.CompareExchange<object>(ref this._syncRoot, new object(), (object) null);
        return this._syncRoot;
      }
    }

    public T this[int index]
    {
      get
      {
        //CheckEnumerating(); read is okay while iterating

        if ((uint) index >= (uint) this._size)
          throw new ArgumentOutOfRangeException();
        return this._items[index];
      }
      set
      {
        CheckEnumerating();

        if ((uint) index >= (uint) this._size)
          throw new ArgumentOutOfRangeException();
        this._items[index] = value;
        ++this._version;
      }
    }

    private static bool IsCompatibleObject(object value)
    {
      if (value is T)
        return true;
      return value == null && (object) default (T) == null;
    }

      object IList.this[int index]
      {
        get
        {
          //CheckEnumerating(); read is okay while iterating

          return (object) this[index];
        }
        set
      {
        CheckEnumerating();

        //ThrowHelper.IfNullAndNullsAreIllegalThenThrow<T>(value, ExceptionArgument.value);
        try
        {
          this[index] = (T) value;
        }
        catch (InvalidCastException ex)
        {
          throw new ArgumentException("$Wrong value type: value, typeof (T)");
        }
      }
      }

    public void Add(T item)
    {
      CheckEnumerating();

      if (this._size == this._items.Length)
        this.EnsureCapacity(this._size + 1);
      this._items[this._size++] = item;
      ++this._version;
    }

    int IList.Add(object item)
    {
      CheckEnumerating();

      //ThrowHelper.IfNullAndNullsAreIllegalThenThrow<T>(item, ExceptionArgument.item);
      try
      {
        this.Add((T) item);
      }
      catch (InvalidCastException ex)
      {
        throw new ArgumentException($"Wrong value type: item, typeof (T)");
      }
      return this.Count - 1;
    }

    public void AddRange(IEnumerable<T> collection) => this.InsertRange(this._size, collection);

    public ReadOnlyCollection<T> AsReadOnly() => new ReadOnlyCollection<T>((IList<T>) this);

    public int BinarySearch(int index, int count, T item, IComparer<T> comparer)
    {
      CheckEnumerating();

      if (index < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (count < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (this._size - index < count)
        throw new ArgumentException("ExceptionResource.Argument_InvalidOffLen");
      return Array.BinarySearch<T>(this._items, index, count, item, comparer);
    }

    public int BinarySearch(T item) => this.BinarySearch(0, this.Count, item, (IComparer<T>) null);

    public int BinarySearch(T item, IComparer<T> comparer) => this.BinarySearch(0, this.Count, item, comparer);

    public void Clear()
    {
      CheckEnumerating();

      if (this._size > 0)
      {
        Array.Clear((Array) this._items, 0, this._size);
        this._size = 0;
      }
      ++this._version;
    }

    public bool Contains(T item)
    {
      // CheckEnumerating(); read is ok while iterating

      if ((object) item == null)
      {
        for (int index = 0; index < this._size; ++index)
        {
          if ((object) this._items[index] == null)
            return true;
        }
        return false;
      }
      EqualityComparer<T> equalityComparer = EqualityComparer<T>.Default;
      for (int index = 0; index < this._size; ++index)
      {
        if (equalityComparer.Equals(this._items[index], item))
          return true;
      }
      return false;
    }

    bool IList.Contains(object item) => SafeList<T>.IsCompatibleObject(item) && this.Contains((T) item);

    public SafeList<TOutput> ConvertAll<TOutput>(Converter<T, TOutput> converter)
    {
      CheckEnumerating();

      if (converter == null)
        throw new ArgumentNullException("ExceptionArgument.converter");
      SafeList<TOutput> outputList = new SafeList<TOutput>(this._size);
      for (int index = 0; index < this._size; ++index)
        outputList._items[index] = converter(this._items[index]);
      outputList._size = this._size;
      return outputList;
    }

    public void CopyTo(T[] array) => this.CopyTo(array, 0);

    void ICollection.CopyTo(Array array, int arrayIndex)
    {
      CheckEnumerating();

      if (array != null && array.Rank != 1)
        throw new ArgumentException("ExceptionResource.Arg_RankMultiDimNotSupported");
      try
      {
        Array.Copy((Array) this._items, 0, array, arrayIndex, this._size);
      }
      catch (ArrayTypeMismatchException ex)
      {
        throw new ArgumentException("ExceptionResource.Argument_InvalidArrayType");
      }
    }

    public void CopyTo(int index, T[] array, int arrayIndex, int count)
    {
      CheckEnumerating();

      if (this._size - index < count)
        throw new ArgumentException("ExceptionResource.Argument_InvalidOffLen");
      Array.Copy((Array) this._items, index, (Array) array, arrayIndex, count);
    }

    public void CopyTo(T[] array, int arrayIndex) => Array.Copy((Array) this._items, 0, (Array) array, arrayIndex, this._size);

    private void EnsureCapacity(int min)
    {
      CheckEnumerating();

      if (this._items.Length >= min)
        return;
      int num = this._items.Length == 0 ? 4 : this._items.Length * 2;
      if ((uint) num > 2146435071U)
        num = 2146435071;
      if (num < min)
        num = min;
      this.Capacity = num;
    }

    public bool Exists(Predicate<T> match) => this.FindIndex(match) != -1;

    public T Find(Predicate<T> match)
    {
      //CheckEnumerating(); read is okay while iterating

      if (match == null)
        throw new ArgumentNullException("ExceptionArgument.match");
      for (int index = 0; index < this._size; ++index)
      {
        if (match(this._items[index]))
          return this._items[index];
      }
      return default (T);
    }

    public SafeList<T> FindAll(Predicate<T> match)
    {
      // CheckEnumerating(); read is ok while iterating

      if (match == null)
        throw new ArgumentNullException("ExceptionArgument.match");
      SafeList<T> all = new SafeList<T>();
      for (int index = 0; index < this._size; ++index)
      {
        if (match(this._items[index]))
          all.Add(this._items[index]);
      }
      return all;
    }

    public int FindIndex(Predicate<T> match) => this.FindIndex(0, this._size, match);

    public int FindIndex(int startIndex, Predicate<T> match) => this.FindIndex(startIndex, this._size - startIndex, match);

    public int FindIndex(int startIndex, int count, Predicate<T> match)
    {
      // CheckEnumerating(); read is okay while iterating

      if ((uint) startIndex > (uint) this._size)
        throw new ArgumentOutOfRangeException("ExceptionArgument.startIndex, ExceptionResource.ArgumentOutOfRange_Index");
      if (count < 0 || startIndex > this._size - count)
        throw new ArgumentOutOfRangeException("ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_Count");
      if (match == null)
        throw new ArgumentNullException("ExceptionArgument.match");
      int num = startIndex + count;
      for (int index = startIndex; index < num; ++index)
      {
        if (match(this._items[index]))
          return index;
      }
      return -1;
    }

    public T FindLast(Predicate<T> match)
    {
      // CheckEnumerating(); read is okay while iterating
      
      if (match == null)
        throw new ArgumentNullException("ExceptionArgument.match");
      for (int index = this._size - 1; index >= 0; --index)
      {
        if (match(this._items[index]))
          return this._items[index];
      }
      return default (T);
    }

    public int FindLastIndex(Predicate<T> match) => this.FindLastIndex(this._size - 1, this._size, match);

    public int FindLastIndex(int startIndex, Predicate<T> match) => this.FindLastIndex(startIndex, startIndex + 1, match);

    public int FindLastIndex(int startIndex, int count, Predicate<T> match)
    {
      // CheckEnumerating(); read is okay while iterating
      
      if (match == null)
        throw new ArgumentNullException("ExceptionArgument.match");
      if (this._size == 0)
      {
        if (startIndex != -1)
          throw new ArgumentOutOfRangeException("ExceptionArgument.startIndex, ExceptionResource.ArgumentOutOfRange_Index");
      }
      else if ((uint) startIndex >= (uint) this._size)
        throw new ArgumentOutOfRangeException("ExceptionArgument.startIndex, ExceptionResource.ArgumentOutOfRange_Index");
      if (count < 0 || startIndex - count + 1 < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_Count");
      int num = startIndex - count;
      for (int lastIndex = startIndex; lastIndex > num; --lastIndex)
      {
        if (match(this._items[lastIndex]))
          return lastIndex;
      }
      return -1;
    }

    public void ForEach(Action<T> action)
    {
      CheckEnumerating();
      
      if (action == null)
        throw new ArgumentNullException("ExceptionArgument.match");
      int version = this._version;
      for (int index = 0; index < this._size && (version == this._version /*|| !BinaryCompatibility.TargetsAtLeast_Desktop_V4_5*/); ++index)
        action(this._items[index]);
      if (version == this._version/* || !BinaryCompatibility.TargetsAtLeast_Desktop_V4_5*/)
        return;
      throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumFailedVersion");
    }

        public SafeList<T>.Enumerator GetEnumerator() => new SafeList<T>.Enumerator(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => (IEnumerator<T>) new SafeList<T>.Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) new SafeList<T>.Enumerator(this);

    public SafeList<T> GetRange(int index, int count)
    {
      // CheckEnumerating(); // read is ok while iterating
      
      if (index < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (count < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (this._size - index < count)
        throw new ArgumentException("ExceptionResource.Argument_InvalidOffLen");
      SafeList<T> range = new SafeList<T>(count);
      Array.Copy((Array) this._items, index, (Array) range._items, 0, count);
      range._size = count;
      return range;
    }

    public int IndexOf(T item)
    {
      // CheckEnumerating(); // read is ok while iterating

      return Array.IndexOf<T>(this._items, item, 0, this._size);
    } 

    int IList.IndexOf(object item) => SafeList<T>.IsCompatibleObject(item) ? this.IndexOf((T) item) : -1;

    public int IndexOf(T item, int index)
    {
      // CheckEnumerating(); // read is ok while iterating

      if (index > this._size)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_Index");
      return Array.IndexOf<T>(this._items, item, index, this._size - index);
    }

    public int IndexOf(T item, int index, int count)
    {
      // CheckEnumerating(); // read is ok while iterating

      if (index > this._size)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_Index");
      if (count < 0 || index > this._size - count)
        throw new ArgumentOutOfRangeException("ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_Count");
      return Array.IndexOf<T>(this._items, item, index, count);
    }

    public void Insert(int index, T item)
    {
      CheckEnumerating();
      
      if ((uint) index > (uint) this._size)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_ListInsert");
      if (this._size == this._items.Length)
        this.EnsureCapacity(this._size + 1);
      if (index < this._size)
        Array.Copy((Array) this._items, index, (Array) this._items, index + 1, this._size - index);
      this._items[index] = item;
      ++this._size;
      ++this._version;
    }

    void IList.Insert(int index, object item)
    {
      CheckEnumerating(); 

      //ThrowHelper.IfNullAndNullsAreIllegalThenThrow<T>(item, ExceptionArgument.item);
      try
      {
        this.Insert(index, (T) item);
      }
      catch (InvalidCastException ex)
      {
        throw new ArgumentException($"Wrong value type: item, typeof (T)");
      }
    }

    public void InsertRange(int index, IEnumerable<T> collection)
    {
      CheckEnumerating();
      
      if (collection == null)
        throw new ArgumentNullException("ExceptionArgument.collection");
      if ((uint) index > (uint) this._size)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_Index");
      if (collection is ICollection<T> objs)
      {
        int count = objs.Count;
        if (count > 0)
        {
          this.EnsureCapacity(this._size + count);
          if (index < this._size)
            Array.Copy((Array) this._items, index, (Array) this._items, index + count, this._size - index);
          if (this == objs)
          {
            Array.Copy((Array) this._items, 0, (Array) this._items, index, index);
            Array.Copy((Array) this._items, index + count, (Array) this._items, index * 2, this._size - index);
          }
          else
          {
            T[] array = new T[count];
            objs.CopyTo(array, 0);
            array.CopyTo((Array) this._items, index);
          }
          this._size += count;
        }
      }
      else
      {
        foreach (T obj in collection)
          this.Insert(index++, obj);
      }
      ++this._version;
    }

    public int LastIndexOf(T item) => this._size == 0 ? -1 : this.LastIndexOf(item, this._size - 1, this._size);

    public int LastIndexOf(T item, int index)
    {
      // CheckEnumerating(); // read is ok while iterating

      if (index >= this._size)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_Index");
      return this.LastIndexOf(item, index, index + 1);
    }

        public int LastIndexOf(T item, int index, int count)
    {
      if (this.Count != 0 && index < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (this.Count != 0 && count < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (this._size == 0)
        return -1;
      if (index >= this._size)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_BiggerThanCollection");
      if (count > index + 1)
        throw new ArgumentOutOfRangeException("ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_BiggerThanCollection");
      return Array.LastIndexOf<T>(this._items, item, index, count);
    }

    public bool Remove(T item)
    {
      CheckEnumerating();
      
      int index = this.IndexOf(item);
      if (index < 0)
        return false;
      this.RemoveAt(index);
      return true;
    }

    void IList.Remove(object item)
    {
      CheckEnumerating();
      
      if (!SafeList<T>.IsCompatibleObject(item))
        return;
      this.Remove((T) item);
    }

    public int RemoveAll(Predicate<T> match)
    {
      CheckEnumerating();
      
      if (match == null)
        throw new ArgumentNullException("ExceptionArgument.match");
      int index1 = 0;
      while (index1 < this._size && !match(this._items[index1]))
        ++index1;
      if (index1 >= this._size)
        return 0;
      int index2 = index1 + 1;
      while (index2 < this._size)
      {
        while (index2 < this._size && match(this._items[index2]))
          ++index2;
        if (index2 < this._size)
          this._items[index1++] = this._items[index2++];
      }
      Array.Clear((Array) this._items, index1, this._size - index1);
      int num = this._size - index1;
      this._size = index1;
      ++this._version;
      return num;
    }

    public void RemoveAt(int index)
    {
      CheckEnumerating();
      
      if ((uint) index >= (uint) this._size)
        throw new ArgumentOutOfRangeException();
      --this._size;
      if (index < this._size)
        Array.Copy((Array) this._items, index + 1, (Array) this._items, index, this._size - index);
      this._items[this._size] = default (T);
      ++this._version;
    }

    public void RemoveRange(int index, int count)
    {
      CheckEnumerating();
      
      if (index < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (count < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (this._size - index < count)
        throw new ArgumentException("ExceptionResource.Argument_InvalidOffLen");
      if (count <= 0)
        return;
      int size = this._size;
      this._size -= count;
      if (index < this._size)
        Array.Copy((Array) this._items, index + count, (Array) this._items, index, this._size - index);
      Array.Clear((Array) this._items, this._size, count);
      ++this._version;
    }

    public void Reverse() => this.Reverse(0, this.Count);

    public void Reverse(int index, int count)
    {
        CheckEnumerating();
      
      if (index < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (count < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (this._size - index < count)
        throw new ArgumentException("ExceptionResource.Argument_InvalidOffLen");
      Array.Reverse((Array) this._items, index, count);
      ++this._version;
    }

        public void Sort() => this.Sort(0, this.Count, (IComparer<T>) null);

        public void Sort(IComparer<T> comparer) => this.Sort(0, this.Count, comparer);

        public void Sort(int index, int count, IComparer<T> comparer)
    {
      if (index < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (count < 0)
        throw new ArgumentOutOfRangeException("ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum");
      if (this._size - index < count)
        throw new ArgumentException("ExceptionResource.Argument_InvalidOffLen");
      Array.Sort<T>(this._items, index, count, comparer);
      ++this._version;
    }

    public void Sort(Comparison<T> comparison)
    {
      CheckEnumerating();
      
      if (comparison == null)
        throw new ArgumentNullException("ExceptionArgument.match");
      if (this._size <= 0)
        return;
      Array.Sort<T>(this._items, 0, this._size, (IComparer<T>) new FunctorComparer<T>(comparison));
    }

    public T[] ToArray()
    {
      // CheckEnumerating(); // read is ok while iterating

      T[] destinationArray = new T[this._size];
      Array.Copy((Array) this._items, 0, (Array) destinationArray, 0, this._size);
      return destinationArray;
    }

    public void TrimExcess()
    {
      CheckEnumerating();
      
      if (this._size >= (int) ((double) this._items.Length * 0.9))
        return;
      this.Capacity = this._size;
    }

      public bool TrueForAll(Predicate<T> match)
    {
      // CheckEnumerating(); // read is ok while iterating

      if (match == null)
        throw new ArgumentNullException("ExceptionArgument.match");
      for (int index = 0; index < this._size; ++index)
      {
        if (!match(this._items[index]))
          return false;
      }
      return true;
    }

    internal static IList<T> Synchronized(SafeList<T> list) => (IList<T>) new SafeList<T>.SynchronizedList(list);

    [Serializable]
    internal class SynchronizedList : IList<T>, ICollection<T>, IEnumerable<T>, IEnumerable
    {
      private SafeList<T> _list;
      private object _root;

      internal SynchronizedList(SafeList<T> list)
      {
        this._list = list;
        this._root = ((ICollection) list).SyncRoot;
      }

      public int Count
      {
        get
        {
          lock (this._root)
            return this._list.Count;
        }
      }

      public bool IsReadOnly => ((ICollection<T>) this._list).IsReadOnly;

      public void Add(T item)
      {
        lock (this._root)
          this._list.Add(item);
      }

      public void Clear()
      {
        lock (this._root)
          this._list.Clear();
      }

      public bool Contains(T item)
      {
        lock (this._root)
          return this._list.Contains(item);
      }

      public void CopyTo(T[] array, int arrayIndex)
      {
        lock (this._root)
          this._list.CopyTo(array, arrayIndex);
      }

      public bool Remove(T item)
      {
        lock (this._root)
          return this._list.Remove(item);
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
        lock (this._root)
          return (IEnumerator) this._list.GetEnumerator();
      }

      IEnumerator<T> IEnumerable<T>.GetEnumerator()
      {
        lock (this._root)
          return ((IEnumerable<T>) this._list).GetEnumerator();
      }

      public T this[int index]
      {
        get
        {
          lock (this._root)
            return this._list[index];
        }
        set
        {
          lock (this._root)
            this._list[index] = value;
        }
      }

      public int IndexOf(T item)
      {
        lock (this._root)
          return this._list.IndexOf(item);
      }

      public void Insert(int index, T item)
      {
        lock (this._root)
          this._list.Insert(index, item);
      }

      public void RemoveAt(int index)
      {
        lock (this._root)
          this._list.RemoveAt(index);
      }
    }

    [Serializable]
    public struct Enumerator : IEnumerator<T>, IDisposable, IEnumerator
    {
      private SafeList<T> list;
      private int index;
      private int version;
      private T current;

      internal Enumerator(SafeList<T> list)
      {
        this.list = list;
        this.index = 0;
        this.version = list._version;
        this.current = default (T);
        
        // CUSTOM CHANGE
        list.enumerating = true;
        // END CUSTOM CHANGE
      }

      
      public void Dispose()
      {
        // CUSTOM CHANGE
        list.enumerating = false;
        // END CUSTOM CHANGE
      }

      public bool MoveNext()
      {
        SafeList<T> list = this.list;
        if (this.version != list._version || (uint) this.index >= (uint) list._size)
          return this.MoveNextRare();
        this.current = list._items[this.index];
        ++this.index;
        return true;
      }

      private bool MoveNextRare()
      {
        if (this.version != this.list._version)
          throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumFailedVersion");
        this.index = this.list._size + 1;
        this.current = default (T);
        return false;
      }

        public T Current
      {
        get => this.current;
      }

      object IEnumerator.Current
      {
        get
        {
          if (this.index == 0 || this.index == this.list._size + 1)
            throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumOpCantHappen");
          return (object) this.Current;
        }
      }

      void IEnumerator.Reset()
      {
        if (this.version != this.list._version)
          throw new InvalidOperationException("ExceptionResource.InvalidOperation_EnumFailedVersion");
        this.index = 0;
        this.current = default (T);
      }
    }
  }
}

internal sealed class FunctorComparer<T> : IComparer<T>
{
  private Comparison<T> comparison;

  public FunctorComparer(Comparison<T> comparison) => this.comparison = comparison;

  public int Compare(T x, T y) => this.comparison(x, y);
}