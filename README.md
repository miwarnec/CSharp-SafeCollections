# C# Safe Collections for Beginners
Safety wheels around C# collections in order to help beginner devs to debug their data races.

For example, IllegalOperationException "Collection was modified while iterating" is hard to debug in complex projects where a lot is going on.

# Multithreaded Access Protection
For example:
```cs
// created on main thread
SafeDictionary<int, string> dict = new SafeDictionary<int, string>();
dict.Add(1, "one");

// accessed from another thread
Task.Run(() =>
{
    // this throws:
    string value = dict[1];
    // ----> System.InvalidOperationException : SafeDictionary Race Condition 
    // detected: it was created from ThreadId=15 but accessed from ThreadId=6. 
    // This will cause undefined state, please debug your code to always access 
    // this from the same thread.
}).Wait();
```
