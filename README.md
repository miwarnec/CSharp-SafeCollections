# C# Safe Collections for Beginners
Safety wheels around C# collections in order to help beginner devs to debug their data races.

### The Problem
By default, if a collection is modified while iterating, the enumerator throws an IllegalOperationException("Collection was modified while iterating").

This is fine, but it's very difficult to debug. Both for beginners, and for professionals when dealing with large projects.

### The Solution
Instead, **C# Safe Collections** throw **in the place of modification** while iterating.

## Collections
Collections are based on Unity 2022's C# / Mono, and modified with "CUSTOM CHANGE" comments.

The following collection types are supported:

- SafeList<T>
- SafeHashSet<T>
- SafeDictionary<K,V>

**It's recommended to go back to the original C# collection types once you debugged your ata race.**

## Examples
C# HashSet which detects illegal modifications in place:
```cs
// create the (Safe)HashSet
SafeHashSet<int> set = new SafeHashSet<int>();
set.Add(1);
set.Add(2);

// iteration uses the custom SafeHashSet.Enumator automatically
foreach (int value in set)
{
    // reading is fine
    set.Contains(1); 

    // modifying while iterating.
    // by default, it would only throw in the next foreach enumeration step.
    // instead, it throws immediately:
    //   > System.InvalidOperationException : Attempted to access collection while it's being enumerated elsewhere.
    //   > This would cause an InvalidOperationException.
    Assert.Throws<InvalidOperationException>(() =>
    {
        set.Add(43);
    });
}
```
