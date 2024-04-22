// base class for all safe collections, with common code.
namespace SafeCollections
{
    public abstract class SafeCollection
    {
        // flag & thread id from the thread that is currently enumerating.
        // threadId is useful to debug race conditions with multiple threads.
        bool enumerating = false;
        int enumeratingThreadId = 0;

        // call this when beginning / ending enumeration
        protected void BeginEnumerating()
        {
          enumerating = true;
          enumeratingThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        protected void EndEnumerating()
        {
          enumerating = false;
        }

        // call this internally before any writes to the collection.
        protected void CheckEnumerating()
        {
          if (enumerating)
          {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            InvalidOperationException exception = new InvalidOperationException(
              $"Attempted to access collection from ThreadId={threadId} while it's being enumerated elsewhere from ThreadId={enumeratingThreadId}. This would cause an InvalidOperationException when enumerating, which would cause a race condition which is hard to debug.");
    #if UNITY_2019_1_OR_NEWER
                    // in Unity: log but continue so the game behaves as before but adds the obvious exception message
                    UnityEngine.Debug.LogException(exception);
    #else
            throw exception;
    #endif
          }
        }
    }
}
