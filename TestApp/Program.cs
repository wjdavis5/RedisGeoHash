using System;
using GeoHash;
using NUnit.Framework;

namespace TestApp
{
    [TestFixture]
    public class Program
    {

        //[SetUp]
        //public void Init() { }

        [TestCase(39.923422, -86.1078998, (ulong)1782901374540128)]
        [TestCase(-86.1078998,39.923422, (ulong)0)]
        public void CheckHashSuccess(double lat, double lon, ulong result)
        {
            var x = new RedisGeoHash(new[] {lat, lon});
            Assert.AreEqual(x.GeoHash,result);
        }
    }
}
