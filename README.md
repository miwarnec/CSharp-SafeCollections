# C# Safe Collections for Beginners
Safety wheels around C# collections in order to help beginner devs to debug their data races.

For example, IllegalOperationException "Collection was modified while iterating" is hard to debug in complex projects where a lot is going on.

# Collections
Collections can be dropped in as single file as needed.
It's recommended to go back to the original C# collections eventually for performance.

- SafeList<T>
- SafeHashSet<T>
- SafeDictionary<K,V>

# Multithreaded Access Detection
Safe Collections report accidental access from other threads:
```cs
// created on main thread
SafeDictionary<int, string> dict = new SafeDictionary<int, string>();

// access from main thread is fine
dict.Add(1, "one");

// accessed from another thread throws:
// ----> System.InvalidOperationException : SafeDictionary Race Condition 
// detected: it was created from ThreadId=15 but accessed from ThreadId=6. 
// This will cause undefined state, please debug your code to always access 
// this from the same thread.
Task.Run(() => {
    string value = dict[1];
}).Wait();
```
