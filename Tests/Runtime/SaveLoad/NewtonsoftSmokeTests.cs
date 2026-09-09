using System;
using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;

namespace NovelForge.Runtime.Tests
{
    public class NewtonsoftSmokeTests
    {
        [Test]
        public void DictionaryOfObject_RoundTripsThroughJsonConvert_WithConvertToleranceIntact()
        {
            var original = new Dictionary<string, object>
            {
                ["relationship"] = 3,
                ["ratio"] = 1.5f,
                ["metAlice"] = true,
                ["playerName"] = "Kai",
            };

            string json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            Assert.AreEqual(3, Convert.ToInt32(restored["relationship"]));
            Assert.AreEqual(1.5f, Convert.ToSingle(restored["ratio"]));
            Assert.AreEqual(true, Convert.ToBoolean(restored["metAlice"]));
            Assert.AreEqual("Kai", restored["playerName"].ToString());
        }
    }
}
