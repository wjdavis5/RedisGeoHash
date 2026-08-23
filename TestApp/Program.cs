using System;
using GeoHash;
using NUnit.Framework;

namespace TestApp
{
    [TestFixture]
    public class RedisGeoHashTests
    {
        private const double TestLat = 39.923422;
        private const double TestLon = -86.1078998;
        private const ulong TestHash = 1782901374540128UL;

        [TestCase(39.923422, -86.1078998, 1782901374540128UL)]
        [TestCase(0.0, 0.0, 3377699720527872UL)]
        public void EncodeMatchesRedis(double lat, double lon, ulong expected)
        {
            var x = new RedisGeoHash(lat, lon);
            Assert.AreEqual(expected, x.GeoHash);
        }

        [Test]
        public void ArrayConstructorMatchesScalarConstructor()
        {
            var fromArray = new RedisGeoHash(new[] {TestLat, TestLon});
            var fromScalars = new RedisGeoHash(TestLat, TestLon);
            Assert.AreEqual(fromScalars.GeoHash, fromArray.GeoHash);
        }

        [Test]
        public void SwappedLatLonThrows()
        {
            /* longitude passed as latitude is outside the ±85.05112878 range */
            Assert.Throws<ArgumentOutOfRangeException>(() => new RedisGeoHash(TestLon, TestLat));
        }

        [TestCase(90.0, 0.0)]
        [TestCase(-90.0, 0.0)]
        [TestCase(0.0, 180.1)]
        [TestCase(0.0, -180.1)]
        [TestCase(double.NaN, 0.0)]
        [TestCase(0.0, double.NaN)]
        public void OutOfRangeCoordinatesThrow(double lat, double lon)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RedisGeoHash(lat, lon));
        }

        [Test]
        public void NullArrayThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new RedisGeoHash(null));
        }

        [Test]
        public void WrongLengthArrayThrows()
        {
            Assert.Throws<ArgumentException>(() => new RedisGeoHash(new[] {1.0}));
            Assert.Throws<ArgumentException>(() => new RedisGeoHash(new[] {1.0, 2.0, 3.0}));
        }

        [Test]
        public void ToStringReturnsHashValue()
        {
            var x = new RedisGeoHash(TestLat, TestLon);
            Assert.AreEqual(TestHash.ToString(), x.ToString());
        }

        [Test]
        public void DecodedAreaContainsOriginalPoint()
        {
            var x = new RedisGeoHash(TestLat, TestLon);
            var area = x.Decode();

            Assert.AreEqual(x.GeoHashBits.Bits, area.Hash.Bits);
            Assert.That(area.Latitude.Min, Is.LessThanOrEqualTo(TestLat));
            Assert.That(area.Latitude.Max, Is.GreaterThanOrEqualTo(TestLat));
            Assert.That(area.Longitude.Min, Is.LessThanOrEqualTo(TestLon));
            Assert.That(area.Longitude.Max, Is.GreaterThanOrEqualTo(TestLon));
        }

        [Test]
        public void DecodeToLongLatIsCloseToOriginal()
        {
            var x = new RedisGeoHash(TestLat, TestLon);
            var xy = new double[2];

            Assert.AreEqual(1, RedisGeoHash.GeohashDecodeToLongLatWgs84(x.GeoHashBits, xy));
            /* at step 26 a cell is ~0.6m, so the center is well within 0.0001° */
            Assert.AreEqual(TestLon, xy[0], 0.0001);
            Assert.AreEqual(TestLat, xy[1], 0.0001);
        }

        [Test]
        public void DecodeOfZeroHashFails()
        {
            var area = new GeoHashArea();
            Assert.AreEqual(0, RedisGeoHash.GeohashDecodeWgs84(new GeoHashBits(), ref area));
        }

        [Test]
        public void NeighborsDifferFromCenterAndEachOther()
        {
            var x = new RedisGeoHash(TestLat, TestLon);
            var n = x.Neighbors();

            var all = new[]
            {
                n.North.Bits, n.South.Bits, n.East.Bits, n.West.Bits,
                n.NorthEast.Bits, n.NorthWest.Bits, n.SouthEast.Bits, n.SouthWest.Bits
            };

            foreach (var bits in all)
                Assert.AreNotEqual(x.GeoHash, bits);
            CollectionAssert.AllItemsAreUnique(all);
        }

        [Test]
        public void EastNeighborIsAdjacentCell()
        {
            var x = new RedisGeoHash(TestLat, TestLon);
            var n = x.Neighbors();

            var center = new GeoHashArea();
            var east = new GeoHashArea();
            Assert.AreEqual(1, RedisGeoHash.GeohashDecodeWgs84(x.GeoHashBits, ref center));
            Assert.AreEqual(1, RedisGeoHash.GeohashDecodeWgs84(n.East, ref east));

            Assert.AreEqual(center.Longitude.Max, east.Longitude.Min, 1e-9);
            Assert.AreEqual(center.Latitude.Min, east.Latitude.Min, 1e-9);
        }

        [Test]
        public void NorthNeighborIsAdjacentCell()
        {
            var x = new RedisGeoHash(TestLat, TestLon);
            var n = x.Neighbors();

            var center = new GeoHashArea();
            var north = new GeoHashArea();
            Assert.AreEqual(1, RedisGeoHash.GeohashDecodeWgs84(x.GeoHashBits, ref center));
            Assert.AreEqual(1, RedisGeoHash.GeohashDecodeWgs84(n.North, ref north));

            Assert.AreEqual(center.Latitude.Max, north.Latitude.Min, 1e-9);
            Assert.AreEqual(center.Longitude.Min, north.Longitude.Min, 1e-9);
        }
    }
}
