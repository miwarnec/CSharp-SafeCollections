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

            foreach (var kvp in dict)
            {
                Console.WriteLine(kvp.Key);
                
                // reading while iterating should still be allowed
                dict.ContainsKey(kvp.Key);
                dict.ContainsValue(kvp.Value);
                string n = dict[1];
                dict.TryGetValue(1, out string val);
                
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
            }
        }
    }
}
