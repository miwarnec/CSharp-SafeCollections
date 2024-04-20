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
    public void TestThreadSafety()
    {
        SafeHashSet<int> set = new SafeHashSet<int>();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        Task.Run(() =>
        {
            // add
            Assert.Throws<InvalidOperationException>(() =>
            {
                set.Add(4);
            });
            // remove
            Assert.Throws<InvalidOperationException>(() =>
            {
                set.Remove(3);
            });
            // clear
            Assert.Throws<InvalidOperationException>(() =>
            {
                set.Clear();
            });
            // enumerate
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (int item in set)
                {
                }
            });
        });
    }
}
