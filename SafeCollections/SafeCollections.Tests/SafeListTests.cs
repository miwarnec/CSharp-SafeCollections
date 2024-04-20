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
        public void TestThreadSafety()
        {
            SafeList<int> list = new SafeList<int>();
            list.Add(1);
            list.Add(2);
            list.Add(3);

            Task.Run(() =>
            {
                // add
                Assert.Throws<InvalidOperationException>(() =>
                {
                    list.Add(4);
                });
                // remove
                Assert.Throws<InvalidOperationException>(() =>
                {
                    list.Remove(3);
                });
                // clear
                Assert.Throws<InvalidOperationException>(() =>
                {
                    list.Clear();
                });
                // enumerate
                Assert.Throws<InvalidOperationException>(() =>
                {
                    foreach (int item in list)
                    {
                    }
                });
                // [] get operator
                Assert.Throws<InvalidOperationException>(() =>
                {
                    int x = list[0];
                });
                // [] set operator
                Assert.Throws<InvalidOperationException>(() =>
                {
                    list[0] = 1;
                });
            });
        }
    }
}