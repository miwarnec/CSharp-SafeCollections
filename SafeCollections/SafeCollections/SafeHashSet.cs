// based on Unity's C# HashSet<T>, which is based on Mono C#.

using System.Collections;
using System.Runtime.Serialization;
using System.Security;

namespace SafeCollections
{
  [Serializable]
  public class SafeHashSet<T> :
    SafeCollection,
    ICollection<T>,
    IEnumerable<T>,
    IEnumerable,
    // ISerializable,
    // IDeserializationCallback,
    ISet<T>,
    IReadOnlyCollection<T>
  {
    private const int Lower31BitMask = 2147483647;
    private const int StackAllocThreshold = 100;
    private const int ShrinkThreshold = 3;
    private const string CapacityName = "Capacity";
    private const string ElementsName = "Elements";
    private const string ComparerName = "Comparer";
    private const string VersionName = "Version";
    private int[] m_buckets;
    private SafeHashSet<T>.Slot[] m_slots;
    private int m_count;
    private int m_lastIndex;
    private int m_freeList;
    private IEqualityComparer<T> m_comparer;
    private int m_version;
    private SerializationInfo m_siInfo;

    public SafeHashSet()
      : this((IEqualityComparer<T>) EqualityComparer<T>.Default)
    {
    }

    public SafeHashSet(int capacity)
      : this(capacity, (IEqualityComparer<T>) EqualityComparer<T>.Default)
    {
    }

    
    public SafeHashSet(IEqualityComparer<T> comparer)
    {
      if (comparer == null)
        comparer = (IEqualityComparer<T>) EqualityComparer<T>.Default;
      this.m_comparer = comparer;
      this.m_lastIndex = 0;
      this.m_count = 0;
      this.m_freeList = -1;
      this.m_version = 0;
    }

    
    public SafeHashSet(IEnumerable<T> collection)
      : this(collection, (IEqualityComparer<T>) EqualityComparer<T>.Default)
    {
    }

    
    public SafeHashSet(IEnumerable<T> collection, IEqualityComparer<T> comparer)
      : this(comparer)
    {
      int capacity;
      switch (collection)
      {
        case null:
          throw new ArgumentNullException(nameof (collection));
        case SafeHashSet<T> objSet when SafeHashSet<T>.AreEqualityComparersEqual(this, objSet):
          this.CopyFrom(objSet);
          return;
        case ICollection<T> objs:
          capacity = objs.Count;
          break;
        default:
          capacity = 0;
          break;
      }
      this.Initialize(capacity);
      this.UnionWith(collection);
      if (this.m_count <= 0 || this.m_slots.Length / this.m_count <= 3)
        return;
      this.TrimExcess();
    }

    private void CopyFrom(SafeHashSet<T> source)
    {
      CheckEnumerating(); // CUSTOM CHANGE
      
      int count = source.m_count;
      if (count == 0)
        return;
      int length = source.m_buckets.Length;
      if (HashHelpers.ExpandPrime(count + 1) >= length)
      {
        this.m_buckets = (int[]) source.m_buckets.Clone();
        this.m_slots = (SafeHashSet<T>.Slot[]) source.m_slots.Clone();
        this.m_lastIndex = source.m_lastIndex;
        this.m_freeList = source.m_freeList;
      }
      else
      {
        int lastIndex = source.m_lastIndex;
        SafeHashSet<T>.Slot[] slots = source.m_slots;
        this.Initialize(count);
        int index1 = 0;
        for (int index2 = 0; index2 < lastIndex; ++index2)
        {
          int hashCode = slots[index2].hashCode;
          if (hashCode >= 0)
          {
            this.AddValue(index1, hashCode, slots[index2].value);
            ++index1;
          }
        }
        this.m_lastIndex = index1;
      }
      this.m_count = count;
    }

    protected SafeHashSet(SerializationInfo info, StreamingContext context) => this.m_siInfo = info;

    public SafeHashSet(int capacity, IEqualityComparer<T> comparer)
      : this(comparer)
    {
      if (capacity < 0)
        throw new ArgumentOutOfRangeException(nameof (capacity));
      if (capacity <= 0)
        return;
      this.Initialize(capacity);
    }

    
    void ICollection<T>.Add(T item) => this.AddIfNotPresent(item);

    
    public void Clear()
    {
      CheckEnumerating(); // CUSTOM CHANGE
      
      if (this.m_lastIndex > 0)
      {
        Array.Clear((Array) this.m_slots, 0, this.m_lastIndex);
        Array.Clear((Array) this.m_buckets, 0, this.m_buckets.Length);
        this.m_lastIndex = 0;
        this.m_count = 0;
        this.m_freeList = -1;
      }
      ++this.m_version;
    }

    
    public bool Contains(T item)
    {
      //CheckEnumerating(); // read while iterating is allowed

      if (this.m_buckets != null)
      {
        int hashCode = this.InternalGetHashCode(item);
        for (int index = this.m_buckets[hashCode % this.m_buckets.Length] - 1; index >= 0; index = this.m_slots[index].next)
        {
          if (this.m_slots[index].hashCode == hashCode && this.m_comparer.Equals(this.m_slots[index].value, item))
            return true;
        }
      }
      return false;
    }

    
    public void CopyTo(T[] array, int arrayIndex) => this.CopyTo(array, arrayIndex, this.m_count);

    
    public bool Remove(T item)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      if (this.m_buckets != null)
      {
        int hashCode = this.InternalGetHashCode(item);
        int index1 = hashCode % this.m_buckets.Length;
        int index2 = -1;
        for (int index3 = this.m_buckets[index1] - 1; index3 >= 0; index3 = this.m_slots[index3].next)
        {
          if (this.m_slots[index3].hashCode == hashCode && this.m_comparer.Equals(this.m_slots[index3].value, item))
          {
            if (index2 < 0)
              this.m_buckets[index1] = this.m_slots[index3].next + 1;
            else
              this.m_slots[index2].next = this.m_slots[index3].next;
            this.m_slots[index3].hashCode = -1;
            this.m_slots[index3].value = default (T);
            this.m_slots[index3].next = this.m_freeList;
            --this.m_count;
            ++this.m_version;
            if (this.m_count == 0)
            {
              this.m_lastIndex = 0;
              this.m_freeList = -1;
            }
            else
              this.m_freeList = index3;
            return true;
          }
          index2 = index3;
        }
      }
      return false;
    }

    
    public int Count
    {
      get
      {
        CheckEnumerating(); // CUSTOM CHANGE

        return this.m_count;
      }
    }


    bool ICollection<T>.IsReadOnly
    {
       get => false;
    }

    
    public SafeHashSet<T>.Enumerator GetEnumerator()
    {
      CheckEnumerating(); // CUSTOM CHANGE
      return new SafeHashSet<T>.Enumerator(this);
    } 

    
    IEnumerator<T> IEnumerable<T>.GetEnumerator() 
    {
      CheckEnumerating(); // CUSTOM CHANGE

      return (IEnumerator<T>) new SafeHashSet<T>.Enumerator(this);
    }

    
    IEnumerator IEnumerable.GetEnumerator() 
    {
      CheckEnumerating(); // CUSTOM CHANGE

     return (IEnumerator) new SafeHashSet<T>.Enumerator(this);
    }

    /*
    [SecurityCritical]
    public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      if (info == null)
        throw new ArgumentNullException(nameof (info));
      info.AddValue("Version", this.m_version);
      info.AddValue("Comparer", HashHelpers.GetEqualityComparerForSerialization((object) this.m_comparer), typeof (IEqualityComparer<T>));
      info.AddValue("Capacity", this.m_buckets == null ? 0 : this.m_buckets.Length);
      if (this.m_buckets == null)
        return;
      T[] array = new T[this.m_count];
      this.CopyTo(array);
      info.AddValue("Elements", (object) array, typeof (T[]));
    }
    */

    /*
    public virtual void OnDeserialization(object sender)
    {
      if (this.m_siInfo == null)
        return;
      int int32 = this.m_siInfo.GetInt32("Capacity");
      this.m_comparer = (IEqualityComparer<T>) this.m_siInfo.GetValue("Comparer", typeof (IEqualityComparer<T>));
      this.m_freeList = -1;
      if (int32 != 0)
      {
        this.m_buckets = new int[int32];
        this.m_slots = new SafeHashSet<T>.Slot[int32];
        T[] objArray = (T[]) this.m_siInfo.GetValue("Elements", typeof (T[]));
        if (objArray == null)
          throw new SerializationException("Serialization_MissingKeys");
        for (int index = 0; index < objArray.Length; ++index)
          this.AddIfNotPresent(objArray[index]);
      }
      else
        this.m_buckets = (int[]) null;
      this.m_version = this.m_siInfo.GetInt32("Version");
      this.m_siInfo = (SerializationInfo) null;
    }
    */

    
    public bool Add(T item) => this.AddIfNotPresent(item);

    public bool TryGetValue(T equalValue, out T actualValue)
    {
      CheckEnumerating(); // CUSTOM CHANGE
      
      if (this.m_buckets != null)
      {
        int index = this.InternalIndexOf(equalValue);
        if (index >= 0)
        {
          actualValue = this.m_slots[index].value;
          return true;
        }
      }
      actualValue = default (T);
      return false;
    }

    
    public void UnionWith(IEnumerable<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE
      
      if (other == null)
        throw new ArgumentNullException(nameof (other));
      foreach (T obj in other)
        this.AddIfNotPresent(obj);
    }

    
    public void IntersectWith(IEnumerable<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE
      
      if (other == null)
        throw new ArgumentNullException(nameof (other));
      if (this.m_count == 0)
        return;
      if (other is ICollection<T> objs)
      {
        if (objs.Count == 0)
        {
          this.Clear();
          return;
        }
        if (other is SafeHashSet<T> objSet && SafeHashSet<T>.AreEqualityComparersEqual(this, objSet))
        {
          this.IntersectWithHashSetWithSameEC(objSet);
          return;
        }
      }
      this.IntersectWithEnumerable(other);
    }

    
    public void ExceptWith(IEnumerable<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE
      
      if (other == null)
        throw new ArgumentNullException(nameof (other));
      if (this.m_count == 0)
        return;
      if (other == this)
      {
        this.Clear();
      }
      else
      {
        foreach (T obj in other)
          this.Remove(obj);
      }
    }

    
    public void SymmetricExceptWith(IEnumerable<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE
      
      if (other == null)
        throw new ArgumentNullException(nameof (other));
      if (this.m_count == 0)
        this.UnionWith(other);
      else if (other == this)
        this.Clear();
      else if (other is SafeHashSet<T> objSet && SafeHashSet<T>.AreEqualityComparersEqual(this, objSet))
        this.SymmetricExceptWithUniqueHashSet(objSet);
      else
        this.SymmetricExceptWithEnumerable(other);
    }

    
    public bool IsSubsetOf(IEnumerable<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE
      
      if (other == null)
        throw new ArgumentNullException(nameof (other));
      if (this.m_count == 0)
        return true;
      if (other is SafeHashSet<T> objSet && SafeHashSet<T>.AreEqualityComparersEqual(this, objSet))
        return this.m_count <= objSet.Count && this.IsSubsetOfHashSetWithSameEC(objSet);
      SafeHashSet<T>.ElementCount elementCount = this.CheckUniqueAndUnfoundElements(other, false);
      return elementCount.uniqueCount == this.m_count && elementCount.unfoundCount >= 0;
    }

    
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE
      
      if (other == null)
        throw new ArgumentNullException(nameof (other));
      if (other is ICollection<T> objs)
      {
        if (this.m_count == 0)
          return objs.Count > 0;
        if (other is SafeHashSet<T> objSet && SafeHashSet<T>.AreEqualityComparersEqual(this, objSet))
          return this.m_count < objSet.Count && this.IsSubsetOfHashSetWithSameEC(objSet);
      }
      SafeHashSet<T>.ElementCount elementCount = this.CheckUniqueAndUnfoundElements(other, false);
      return elementCount.uniqueCount == this.m_count && elementCount.unfoundCount > 0;
    }

    
    public bool IsSupersetOf(IEnumerable<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE
      
      if (other == null)
        throw new ArgumentNullException(nameof (other));
      if (other is ICollection<T> objs)
      {
        if (objs.Count == 0)
          return true;
        if (other is SafeHashSet<T> set2 && SafeHashSet<T>.AreEqualityComparersEqual(this, set2) && set2.Count > this.m_count)
          return false;
      }
      return this.ContainsAllElements(other);
    }

    
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE
      
      if (other == null)
        throw new ArgumentNullException(nameof (other));
      if (this.m_count == 0)
        return false;
      if (other is ICollection<T> objs)
      {
        if (objs.Count == 0)
          return true;
        if (other is SafeHashSet<T> objSet && SafeHashSet<T>.AreEqualityComparersEqual(this, objSet))
          return objSet.Count < this.m_count && this.ContainsAllElements((IEnumerable<T>) objSet);
      }
      SafeHashSet<T>.ElementCount elementCount = this.CheckUniqueAndUnfoundElements(other, true);
      return elementCount.uniqueCount < this.m_count && elementCount.unfoundCount == 0;
    }

    
    public bool Overlaps(IEnumerable<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      if (other == null)
        throw new ArgumentNullException(nameof (other));
      if (this.m_count == 0)
        return false;
      foreach (T obj in other)
      {
        if (this.Contains(obj))
          return true;
      }
      return false;
    }

    
    public bool SetEquals(IEnumerable<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      switch (other)
      {
        case null:
          throw new ArgumentNullException(nameof (other));
        case SafeHashSet<T> objSet when SafeHashSet<T>.AreEqualityComparersEqual(this, objSet):
          return this.m_count == objSet.Count && this.ContainsAllElements((IEnumerable<T>) objSet);
        case ICollection<T> objs when this.m_count == 0 && objs.Count > 0:
          return false;
        default:
          SafeHashSet<T>.ElementCount elementCount = this.CheckUniqueAndUnfoundElements(other, true);
          return elementCount.uniqueCount == this.m_count && elementCount.unfoundCount == 0;
      }
    }

    
    public void CopyTo(T[] array) => this.CopyTo(array, 0, this.m_count);

    
    public void CopyTo(T[] array, int arrayIndex, int count)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      if (array == null)
        throw new ArgumentNullException(nameof (array));
      if (arrayIndex < 0)
        throw new ArgumentOutOfRangeException(nameof (arrayIndex), "ArgumentOutOfRange_NeedNonNegNum");
      if (count < 0)
        throw new ArgumentOutOfRangeException(nameof (count), "ArgumentOutOfRange_NeedNonNegNum");
      if (arrayIndex > array.Length || count > array.Length - arrayIndex)
        throw new ArgumentException("Arg_ArrayPlusOffTooSmall");
      int num = 0;
      for (int index = 0; index < this.m_lastIndex && num < count; ++index)
      {
        if (this.m_slots[index].hashCode >= 0)
        {
          array[arrayIndex + num] = this.m_slots[index].value;
          ++num;
        }
      }
    }

    
    public int RemoveWhere(Predicate<T> match)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      if (match == null)
        throw new ArgumentNullException(nameof (match));
      int num = 0;
      for (int index = 0; index < this.m_lastIndex; ++index)
      {
        if (this.m_slots[index].hashCode >= 0)
        {
          T obj = this.m_slots[index].value;
          if (match(obj) && this.Remove(obj))
            ++num;
        }
      }
      return num;
    }

    
    public IEqualityComparer<T> Comparer
    {
       get => this.m_comparer;
    }

    
    public void TrimExcess()
    {
      CheckEnumerating(); // CUSTOM CHANGE

      if (this.m_count == 0)
      {
        this.m_buckets = (int[]) null;
        this.m_slots = (SafeHashSet<T>.Slot[]) null;
        ++this.m_version;
      }
      else
      {
        int prime = HashHelpers.GetPrime(this.m_count);
        SafeHashSet<T>.Slot[] slotArray = new SafeHashSet<T>.Slot[prime];
        int[] numArray = new int[prime];
        int index1 = 0;
        for (int index2 = 0; index2 < this.m_lastIndex; ++index2)
        {
          if (this.m_slots[index2].hashCode >= 0)
          {
            slotArray[index1] = this.m_slots[index2];
            int index3 = slotArray[index1].hashCode % prime;
            slotArray[index1].next = numArray[index3] - 1;
            numArray[index3] = index1 + 1;
            ++index1;
          }
        }
        this.m_lastIndex = index1;
        this.m_slots = slotArray;
        this.m_buckets = numArray;
        this.m_freeList = -1;
      }
    }

    // public static IEqualityComparer<SafeHashSet<T>> CreateSetComparer() => (IEqualityComparer<SafeHashSet<T>>) new HashSetEqualityComparer<T>();

    private void Initialize(int capacity)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      int prime = HashHelpers.GetPrime(capacity);
      this.m_buckets = new int[prime];
      this.m_slots = new SafeHashSet<T>.Slot[prime];
    }

    private void IncreaseCapacity()
    {
      CheckEnumerating(); // CUSTOM CHANGE

      int newSize = HashHelpers.ExpandPrime(this.m_count);
      if (newSize <= this.m_count)
        throw new ArgumentException("Arg_HSCapacityOverflow");
      this.SetCapacity(newSize, false);
    }

    private void SetCapacity(int newSize, bool forceNewHashCodes)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      SafeHashSet<T>.Slot[] destinationArray = new SafeHashSet<T>.Slot[newSize];
      if (this.m_slots != null)
        Array.Copy((Array) this.m_slots, 0, (Array) destinationArray, 0, this.m_lastIndex);
      if (forceNewHashCodes)
      {
        for (int index = 0; index < this.m_lastIndex; ++index)
        {
          if (destinationArray[index].hashCode != -1)
            destinationArray[index].hashCode = this.InternalGetHashCode(destinationArray[index].value);
        }
      }
      int[] numArray = new int[newSize];
      for (int index1 = 0; index1 < this.m_lastIndex; ++index1)
      {
        int index2 = destinationArray[index1].hashCode % newSize;
        destinationArray[index1].next = numArray[index2] - 1;
        numArray[index2] = index1 + 1;
      }
      this.m_slots = destinationArray;
      this.m_buckets = numArray;
    }

    private bool AddIfNotPresent(T value)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      if (this.m_buckets == null)
        this.Initialize(0);
      int hashCode = this.InternalGetHashCode(value);
      int index1 = hashCode % this.m_buckets.Length;
      int num = 0;
      for (int index2 = this.m_buckets[hashCode % this.m_buckets.Length] - 1; index2 >= 0; index2 = this.m_slots[index2].next)
      {
        if (this.m_slots[index2].hashCode == hashCode && this.m_comparer.Equals(this.m_slots[index2].value, value))
          return false;
        ++num;
      }
      int index3;
      if (this.m_freeList >= 0)
      {
        index3 = this.m_freeList;
        this.m_freeList = this.m_slots[index3].next;
      }
      else
      {
        if (this.m_lastIndex == this.m_slots.Length)
        {
          this.IncreaseCapacity();
          index1 = hashCode % this.m_buckets.Length;
        }
        index3 = this.m_lastIndex;
        ++this.m_lastIndex;
      }
      this.m_slots[index3].hashCode = hashCode;
      this.m_slots[index3].value = value;
      this.m_slots[index3].next = this.m_buckets[index1] - 1;
      this.m_buckets[index1] = index3 + 1;
      ++this.m_count;
      ++this.m_version;
      // if (num > 100 && HashHelpers.IsWellKnownEqualityComparer((object) this.m_comparer))
      // {
      //   this.m_comparer = (IEqualityComparer<T>) HashHelpers.GetRandomizedEqualityComparer((object) this.m_comparer);
      //   this.SetCapacity(this.m_buckets.Length, true);
      // }
      return true;
    }

    private void AddValue(int index, int hashCode, T value)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      int index1 = hashCode % this.m_buckets.Length;
      this.m_slots[index].hashCode = hashCode;
      this.m_slots[index].value = value;
      this.m_slots[index].next = this.m_buckets[index1] - 1;
      this.m_buckets[index1] = index + 1;
    }

    private bool ContainsAllElements(IEnumerable<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      foreach (T obj in other)
      {
        if (!this.Contains(obj))
          return false;
      }
      return true;
    }

    private bool IsSubsetOfHashSetWithSameEC(SafeHashSet<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      foreach (T obj in this)
      {
        if (!other.Contains(obj))
          return false;
      }
      return true;
    }

    private void IntersectWithHashSetWithSameEC(SafeHashSet<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      for (int index = 0; index < this.m_lastIndex; ++index)
      {
        if (this.m_slots[index].hashCode >= 0)
        {
          T obj = this.m_slots[index].value;
          if (!other.Contains(obj))
            this.Remove(obj);
        }
      }
    }

    [SecuritySafeCritical]
    private unsafe void IntersectWithEnumerable(IEnumerable<T> other)
    {
      throw new NotImplementedException();
      /*
      int lastIndex = this.m_lastIndex;
      int intArrayLength = BitHelper.ToIntArrayLength(lastIndex);
      BitHelper bitHelper;
      if (intArrayLength <= 100)
      {
        int* bitArrayPtr = stackalloc int[intArrayLength];
        bitHelper = new BitHelper(bitArrayPtr, intArrayLength);
      }
      else
        bitHelper = new BitHelper(new int[intArrayLength], intArrayLength);
      foreach (T obj in other)
      {
        int bitPosition = this.InternalIndexOf(obj);
        if (bitPosition >= 0)
          bitHelper.MarkBit(bitPosition);
      }
      for (int bitPosition = 0; bitPosition < lastIndex; ++bitPosition)
      {
        if (this.m_slots[bitPosition].hashCode >= 0 && !bitHelper.IsMarked(bitPosition))
          this.Remove(this.m_slots[bitPosition].value);
      }
      */
    }

    private int InternalIndexOf(T item)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      int hashCode = this.InternalGetHashCode(item);
      for (int index = this.m_buckets[hashCode % this.m_buckets.Length] - 1; index >= 0; index = this.m_slots[index].next)
      {
        if (this.m_slots[index].hashCode == hashCode && this.m_comparer.Equals(this.m_slots[index].value, item))
          return index;
      }
      return -1;
    }

    private void SymmetricExceptWithUniqueHashSet(SafeHashSet<T> other)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      foreach (T obj in other)
      {
        if (!this.Remove(obj))
          this.AddIfNotPresent(obj);
      }
    }

    [SecuritySafeCritical]
    private unsafe void SymmetricExceptWithEnumerable(IEnumerable<T> other)
    {
      throw new NotImplementedException();
      /*
      int lastIndex = this.m_lastIndex;
      int intArrayLength = BitHelper.ToIntArrayLength(lastIndex);
      BitHelper bitHelper1;
      BitHelper bitHelper2;
      if (intArrayLength <= 50)
      {
        int* bitArrayPtr1 = stackalloc int[intArrayLength];
        bitHelper1 = new BitHelper(bitArrayPtr1, intArrayLength);
        int* bitArrayPtr2 = stackalloc int[intArrayLength];
        bitHelper2 = new BitHelper(bitArrayPtr2, intArrayLength);
      }
      else
      {
        bitHelper1 = new BitHelper(new int[intArrayLength], intArrayLength);
        bitHelper2 = new BitHelper(new int[intArrayLength], intArrayLength);
      }
      foreach (T obj in other)
      {
        int location = 0;
        if (this.AddOrGetLocation(obj, out location))
          bitHelper2.MarkBit(location);
        else if (location < lastIndex && !bitHelper2.IsMarked(location))
          bitHelper1.MarkBit(location);
      }
      for (int bitPosition = 0; bitPosition < lastIndex; ++bitPosition)
      {
        if (bitHelper1.IsMarked(bitPosition))
          this.Remove(this.m_slots[bitPosition].value);
      }
      */
    }

    private bool AddOrGetLocation(T value, out int location)
    {
      CheckEnumerating(); // CUSTOM CHANGE

      int hashCode = this.InternalGetHashCode(value);
      int index1 = hashCode % this.m_buckets.Length;
      for (int index2 = this.m_buckets[hashCode % this.m_buckets.Length] - 1; index2 >= 0; index2 = this.m_slots[index2].next)
      {
        if (this.m_slots[index2].hashCode == hashCode && this.m_comparer.Equals(this.m_slots[index2].value, value))
        {
          location = index2;
          return false;
        }
      }
      int index3;
      if (this.m_freeList >= 0)
      {
        index3 = this.m_freeList;
        this.m_freeList = this.m_slots[index3].next;
      }
      else
      {
        if (this.m_lastIndex == this.m_slots.Length)
        {
          this.IncreaseCapacity();
          index1 = hashCode % this.m_buckets.Length;
        }
        index3 = this.m_lastIndex;
        ++this.m_lastIndex;
      }
      this.m_slots[index3].hashCode = hashCode;
      this.m_slots[index3].value = value;
      this.m_slots[index3].next = this.m_buckets[index1] - 1;
      this.m_buckets[index1] = index3 + 1;
      ++this.m_count;
      ++this.m_version;
      location = index3;
      return true;
    }

    [SecuritySafeCritical]
    private unsafe SafeHashSet<T>.ElementCount CheckUniqueAndUnfoundElements(
      IEnumerable<T> other,
      bool returnIfUnfound)
    {
      throw new NotImplementedException();
      /*
      if (this.m_count == 0)
      {
        int num = 0;
        using (IEnumerator<T> enumerator = other.GetEnumerator())
        {
          if (enumerator.MoveNext())
          {
            T current = enumerator.Current;
            ++num;
          }
        }
        SafeHashSet<T>.ElementCount elementCount;
        elementCount.uniqueCount = 0;
        elementCount.unfoundCount = num;
        return elementCount;
      }
      int intArrayLength = BitHelper.ToIntArrayLength(this.m_lastIndex);
      BitHelper bitHelper;
      if (intArrayLength <= 100)
      {
        int* bitArrayPtr = stackalloc int[intArrayLength];
        bitHelper = new BitHelper(bitArrayPtr, intArrayLength);
      }
      else
        bitHelper = new BitHelper(new int[intArrayLength], intArrayLength);
      int num1 = 0;
      int num2 = 0;
      foreach (T obj in other)
      {
        int bitPosition = this.InternalIndexOf(obj);
        if (bitPosition >= 0)
        {
          if (!bitHelper.IsMarked(bitPosition))
          {
            bitHelper.MarkBit(bitPosition);
            ++num2;
          }
        }
        else
        {
          ++num1;
          if (returnIfUnfound)
            break;
        }
      }
      SafeHashSet<T>.ElementCount elementCount1;
      elementCount1.uniqueCount = num2;
      elementCount1.unfoundCount = num1;
      return elementCount1;
      */
    }

    internal T[] ToArray()
    {
      CheckEnumerating(); // CUSTOM CHANGE

      T[] array = new T[this.Count];
      this.CopyTo(array);
      return array;
    }

    internal static bool HashSetEquals(
      SafeHashSet<T> set1,
      SafeHashSet<T> set2,
      IEqualityComparer<T> comparer)
    {
      if (set1 == null)
        return set2 == null;
      if (set2 == null)
        return false;
      if (SafeHashSet<T>.AreEqualityComparersEqual(set1, set2))
      {
        if (set1.Count != set2.Count)
          return false;
        foreach (T obj in set2)
        {
          if (!set1.Contains(obj))
            return false;
        }
        return true;
      }
      foreach (T x in set2)
      {
        bool flag = false;
        foreach (T y in set1)
        {
          if (comparer.Equals(x, y))
          {
            flag = true;
            break;
          }
        }
        if (!flag)
          return false;
      }
      return true;
    }

    private static bool AreEqualityComparersEqual(SafeHashSet<T> set1, SafeHashSet<T> set2) => set1.Comparer.Equals((object) set2.Comparer);

    private int InternalGetHashCode(T item) => (object) item == null ? 0 : this.m_comparer.GetHashCode(item) & int.MaxValue;

    internal struct ElementCount
    {
      internal int uniqueCount;
      internal int unfoundCount;
    }

    internal struct Slot
    {
      internal int hashCode;
      internal int next;
      internal T value;
    }

    
    [Serializable]
    public struct Enumerator : IEnumerator<T>, IDisposable, IEnumerator
    {
      private SafeHashSet<T> set;
      private int index;
      private int version;
      private T current;

      internal Enumerator(SafeHashSet<T> set)
      {
        this.set = set;
        this.index = 0;
        this.version = set.m_version;
        this.current = default (T);

        set.BeginEnumerating(); // CUSTOM CHANGE
      }


      public void Dispose()
      {
        set.EndEnumerating(); // CUSTOM CHANGE
      }

      public bool MoveNext()
      {
        if (this.version != this.set.m_version)
          throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
        for (; this.index < this.set.m_lastIndex; ++this.index)
        {
          if (this.set.m_slots[this.index].hashCode >= 0)
          {
            this.current = this.set.m_slots[this.index].value;
            ++this.index;
            return true;
          }
        }
        this.index = this.set.m_lastIndex + 1;
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
          if (this.index == 0 || this.index == this.set.m_lastIndex + 1)
            throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
          return (object) this.Current;
        }
      }

      
      void IEnumerator.Reset()
      {
        if (this.version != this.set.m_version)
          throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
        this.index = 0;
        this.current = default (T);
      }
    }
  }
}

// from Unity HashHelpers
public static class HashHelpers
{
  public static readonly int[] primes = new int[72]
  {
    3,
    7,
    11,
    17,
    23,
    29,
    37,
    47,
    59,
    71,
    89,
    107,
    131,
    163,
    197,
    239,
    293,
    353,
    431,
    521,
    631,
    761,
    919,
    1103,
    1327,
    1597,
    1931,
    2333,
    2801,
    3371,
    4049,
    4861,
    5839,
    7013,
    8419,
    10103,
    12143,
    14591,
    17519,
    21023,
    25229,
    30293,
    36353,
    43627,
    52361,
    62851,
    75431,
    90523,
    108631,
    130363,
    156437,
    187751,
    225307,
    270371,
    324449,
    389357,
    467237,
    560689,
    672827,
    807403,
    968897,
    1162687,
    1395263,
    1674319,
    2009191,
    2411033,
    2893249,
    3471899,
    4166287,
    4999559,
    5999471,
    7199369
  };
  
  
  public static bool IsPrime(int candidate)
  {
    if ((candidate & 1) == 0)
      return candidate == 2;
    int num = (int) Math.Sqrt((double) candidate);
    for (int index = 3; index <= num; index += 2)
    {
      if (candidate % index == 0)
        return false;
    }
    return true;
  }
  
  public static int GetMinPrime() => HashHelpers.primes[0];

  public static int ExpandPrime(int oldSize)
  {
    int min = 2 * oldSize;
    return (uint) min > 2146435069U && 2146435069 > oldSize ? 2146435069 : HashHelpers.GetPrime(min);
  }
  
  public static int GetPrime(int min)
  {
    if (min < 0)
      throw new ArgumentException("Arg_HTCapacityOverflow");
    for (int index = 0; index < HashHelpers.primes.Length; ++index)
    {
      int prime = HashHelpers.primes[index];
      if (prime >= min)
        return prime;
    }
    for (int candidate = min | 1; candidate < int.MaxValue; candidate += 2)
    {
      if (HashHelpers.IsPrime(candidate) && (candidate - 1) % 101 != 0)
        return candidate;
    }
    return min;
  }
}