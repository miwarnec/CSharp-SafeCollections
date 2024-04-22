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

            // below code modifies while iterating, so this should throw an InvalidOperationException.
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var kvp in dict)
                {
                    Console.WriteLine(kvp.Key);

                    // reading while iterating should still be allowed
                    dict.ContainsKey(kvp.Key);
                    dict.ContainsValue(kvp.Value);
                    string n = dict[1];
                    dict.TryGetValue(1, out string val);

                    // add
                    Assert.Throws<InvalidOperationException>(() => { dict.Add(4, "four"); });
                    // remove
                    Assert.Throws<InvalidOperationException>(() => { dict.Remove(3); });
                    // clear
                    Assert.Throws<InvalidOperationException>(() => { dict.Clear(); });
                    // [] set operator
                    Assert.Throws<InvalidOperationException>(() => { dict[2] = "two"; });
                }
            });
        }

        [Test]
        public void TestIterateKeys()
        {
            SafeDictionary<int, string> dict = new SafeDictionary<int, string>();
            dict.Add(1, "one");
            dict.Add(2, "two");
            dict.Add(3, "three");

            // below code modifies while iterating, so this should throw an InvalidOperationException.
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var key in dict.Keys)
                {
                    Console.WriteLine(key);

                    // reading while iterating should still be allowed
                    dict.ContainsKey(key);
                    string n = dict[1];
                    dict.TryGetValue(1, out string val);

                    // add
                    Assert.Throws<InvalidOperationException>(() => { dict.Add(4, "four"); });
                    // remove
                    Assert.Throws<InvalidOperationException>(() => { dict.Remove(3); });
                    // clear
                    Assert.Throws<InvalidOperationException>(() => { dict.Clear(); });
                    // [] set operator
                    Assert.Throws<InvalidOperationException>(() => { dict[2] = "two"; });
                }
            });
        }

        [Test]
        public void TestIterateValues()
        {
            SafeDictionary<int, string> dict = new SafeDictionary<int, string>();
            dict.Add(1, "one");
            dict.Add(2, "two");
            dict.Add(3, "three");

            // below code modifies while iterating, so this should throw an InvalidOperationException.
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var value in dict.Values)
                {
                    Console.WriteLine(value);

                    // reading while iterating should still be allowed
                    dict.ContainsKey(1);
                    string n = dict[1];
                    dict.TryGetValue(1, out string val);

                    // add
                    Assert.Throws<InvalidOperationException>(() => { dict.Add(4, "four"); });
                    // remove
                    Assert.Throws<InvalidOperationException>(() => { dict.Remove(3); });
                    // clear
                    Assert.Throws<InvalidOperationException>(() => { dict.Clear(); });
                    // [] set operator
                    Assert.Throws<InvalidOperationException>(() => { dict[2] = "two"; });
                }
            });
        }
    }
}
