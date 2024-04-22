// base class for all safe collections, with common code.
namespace SafeCollections
{
    public abstract class SafeCollection
    {
        protected bool enumerating = false;

        // call this internally before any writes to the collection.
        protected void CheckEnumerating()
        {
          if (enumerating)
          {
            InvalidOperationException exception = new InvalidOperationException(
              "Attempted to access collection while it's being enumerated elsewhere. This would cause an InvalidOperationException when enumerating, which would cause a race condition which is hard to debug.");
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
