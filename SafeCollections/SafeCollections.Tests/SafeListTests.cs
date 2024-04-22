using NUnit.Framework;

namespace SafeCollections.Tests
{
    public class SafeListTests
    {
        [Test]
        public void AddRemoveCount()
        {
            SafeList<int> list = new SafeList<int>();
            list.Add(1);
            list.Add(2);
            list.Add(3);
            Assert.AreEqual(3, list.Count);

            list.Remove(2);
            Assert.AreEqual(2, list.Count);
        }

        [Test]
        public void TestIterate()
        {
            SafeList<int> list = new SafeList<int>();
            list.Add(1);
            list.Add(2);
            list.Add(3);

            int count = 0;
            foreach (int item in list)
            {
                count++;
            }
            Assert.AreEqual(3, count);
        }

        [Test]
        public void TestChangeWhileEnumerating()
        {
            SafeList<int> list = new SafeList<int>();
            list.Add(1);
            list.Add(2);
            list.Add(3);

            foreach (int value in list)
            {
                Console.WriteLine(value);
            
                // reading while iterating should still be allowed
                list.Contains(1);
                int n = list[0];
                int index = list.IndexOf(0);

                // modifying while iterating should throw IMMEDIATELY, and not just in the enumerator.
                //   > System.InvalidOperationException : Attempted to access collection while it's being enumerated elsewhere.
                //   > This would cause an InvalidOperationException...
                Assert.Throws<InvalidOperationException>(() =>
                {
                    list.Add(42);
                });
                Assert.Throws<InvalidOperationException>(() =>
                {
                    list[0] = 42;
                });
                Assert.Throws<InvalidOperationException>(() =>
                {
                    list.Clear();
                });
            }
        
            // after enumeration, adding should be allowed again without throwing.
            list.Add(4);
        
            // try another enumeration just to be sure
            foreach (int value in list)
            {
                Console.WriteLine(value);
            
                // reading while iterating should still be allowed
                list.Contains(2); 
                int n = list[0];

                // modifying while iterating should throw IMMEDIATELY, and not just in the enumerator.
                //   > System.InvalidOperationException : Attempted to access collection while it's being enumerated elsewhere.
                //   > This would cause an InvalidOperationException...
                Assert.Throws<InvalidOperationException>(() =>
                {
                    list.Add(43);
                });
                Assert.Throws<InvalidOperationException>(() =>
                {
                    list[0] = 43;
                });
            }
        }

        [Test]
        public void TestFromAnotherThread()
        {
            SafeList<int> list = new SafeList<int>();
            list.Add(1);
            list.Add(2);
            list.Add(3);

            foreach (int value in list)
            {
                // try to access from another thread
                Thread thread = new Thread(() =>
                {
                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        list.Add(42);
                    });
                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        list[0] = 42;
                    });
                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        list.Clear();
                    });
                });
                thread.Start();
                thread.Join();
            }
        }
    }
}