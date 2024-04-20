using NUnit.Framework;

namespace SafeCollections.Tests
{
    public class SafeDictionaryTests
    {
        [Test]
        public void AddRemoveCount()
        {
            SafeDictionary<int, string> dict = new SafeDictionary<int, string>();
            dict.Add(1, "one");
            dict.Add(2, "two");
            dict.Add(3, "three");
            Assert.AreEqual(3, dict.Count);

            dict.Remove(2);
            Assert.AreEqual(2, dict.Count);
        }

        [Test]
        public void TestIterate()
        {
            SafeDictionary<int, string> dict = new SafeDictionary<int, string>();
            dict.Add(1, "one");
            dict.Add(2, "two");
            dict.Add(3, "three");

            int count = 0;
            foreach (KeyValuePair<int, string> item in dict)
            {
                count++;
            }
            Assert.AreEqual(3, count);
        }

        [Test]
        public void TestThreadSafety()
        {
            SafeDictionary<int, string> dict = new SafeDictionary<int, string>();
            dict.Add(1, "one");
            dict.Add(2, "two");
            dict.Add(3, "three");

            Task.Run(() =>
            {
                // add
                Assert.Throws<InvalidOperationException>(() =>
                {
                    dict.Add(4, "four");
                });
                // remove
                Assert.Throws<InvalidOperationException>(() =>
                {
                    dict.Remove(3);
                });
                // clear
                Assert.Throws<InvalidOperationException>(() =>
                {
                    dict.Clear();
                });
                // [] get operator
                Assert.Throws<InvalidOperationException>(() =>
                {
                    string value = dict[2];
                });
                // [] set operator
                Assert.Throws<InvalidOperationException>(() =>
                {
                    dict[2] = "two";
                });
                // Keys
                Assert.Throws<InvalidOperationException>(() =>
                {
                    ICollection<int> keys = dict.Keys;
                });
                // Values
                Assert.Throws<InvalidOperationException>(() =>
                {
                    ICollection<string> values = dict.Values;
                });
                // enumerator
                Assert.Throws<InvalidOperationException>(() =>
                {
                    foreach (KeyValuePair<int, string> item in dict)
                    {
                    }
                });
            }).Wait();
        }
    }
}
