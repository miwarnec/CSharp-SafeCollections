// base class for all safe collections, with common code.

using System.Text;

namespace SafeCollections
{
    // details for an enumeration in progress
    struct EnumerationInfo
    {
        public int threadId;
        public string stackTrace;
    }

    public abstract class SafeCollection
    {
        // flag & thread id from the thread that is currently enumerating.
        // threadId is useful to debug race conditions with multiple threads.
        // stack trace is useful to find out where the enumeration started.
        //
        // there may be multiple enumerations at the same time, for example:
        //   foreach dict.keys:
        //       foreach dict.values:
        //            foreach dict:
        //               ...
        // so we need to keep a dict to track multiple enumerations at the same time.
        readonly Dictionary<Guid, EnumerationInfo> enumerations = new Dictionary<Guid, EnumerationInfo>();

        // call this when beginning / ending enumeration
        protected void BeginEnumerating(Guid uniqueId)
        {
            enumerations.Add(uniqueId, new EnumerationInfo
            {
                threadId = Thread.CurrentThread.ManagedThreadId,
                stackTrace = Environment.StackTrace
            });
        }

        protected void EndEnumerating(Guid uniqueId)
        {
            enumerations.Remove(uniqueId);
        }

        // call this internally AFTER .version changed.
        // this way the modification throws, and the enumeration throws because .version still had time to change.
        protected void OnVersionChanged()
        {
          if (enumerations.Count > 0)
          {
            // log thread id & stack trace for both this modification and all current enumerations.
            StringBuilder builder = new StringBuilder();
            foreach (EnumerationInfo enumeration in enumerations.Values)
            {
                builder.AppendLine(
                    $"Enumeration ThreadId={enumeration.threadId} StackTrace=\n{enumeration.stackTrace}");
                builder.AppendLine("--------------------------------------");
            }

            // note that the current stack trace is automatically appended by C#, we don't need to add it at the end.
            int threadId = Thread.CurrentThread.ManagedThreadId;
            InvalidOperationException exception = new InvalidOperationException(
              $"Attempted to access collection from ThreadId={threadId} while it's being enumerated in {enumerations.Count} other place(s). This would cause an InvalidOperationException when enumerating, which would cause a race condition which is hard to debug.\n\nEnumerations:\n{builder.ToString()}\nModification stack trace:\n");
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
