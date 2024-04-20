using NUnit.Framework;

namespace SafeCollections.Tests;

public class SafeHashSetTests
{
    [Test]
    public void AddRemoveCount()
    {
        SafeHashSet<int> set = new SafeHashSet<int>();
        set.Add(1);
        set.Add(2);
        set.Add(3);
        Assert.AreEqual(3, set.Count);

        set.Remove(2);
        Assert.AreEqual(2, set.Count);
    }

    [Test]
    public void TestIterate()
    {
        SafeHashSet<int> set = new SafeHashSet<int>();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        int count = 0;
        foreach (int item in set)
        {
            count++;
        }
        Assert.AreEqual(3, count);
    }

    [Test]
    public void TestChangeWhileEnumerating()
    {
        SafeHashSet<int> set = new SafeHashSet<int>();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        foreach (int value in set)
        {
            Console.WriteLine(value);
            
            // reading while iterating should still be allowed
            set.Contains(1); 

            // modifying while iterating should throw IMMEDIATELY, and not just in the enumerator.
            //   > System.InvalidOperationException : Attempted to access collection while it's being enumerated elsewhere.
            //   > This would cause an InvalidOperationException...
            Assert.Throws<InvalidOperationException>(() =>
            {
                set.Add(42);
            });
            Assert.Throws<InvalidOperationException>(() =>
            {
                set.Clear();
            });
        }
        
        // after enumeration, adding should be allowed again without throwing.
        set.Add(4);
        
        // try another enumeration just to be sure
        foreach (int value in set)
        {
            Console.WriteLine(value);
            
            // reading while iterating should still be allowed
            set.Contains(2); 

            // modifying while iterating should throw IMMEDIATELY, and not just in the enumerator.
            //   > System.InvalidOperationException : Attempted to access collection while it's being enumerated elsewhere.
            //   > This would cause an InvalidOperationException...
            Assert.Throws<InvalidOperationException>(() =>
            {
                set.Add(43);
            });
        }
    }
}
