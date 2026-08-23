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

        [TestCase(85.05112878, 12.0)]
        [TestCase(-85.05112878, 12.0)]
        [TestCase(12.0, 180.0)]
        [TestCase(12.0, -180.0)]
        public void BoundaryCoordinatesAreAccepted(double lat, double lon)
        {
            var x = new RedisGeoHash(lat, lon);
            Assert.AreNotEqual(0UL, x.GeoHash);
        }

        [Test]
        public void OutOfRangeExceptionsNameTheOffendingParameter()
        {
            var latEx = Assert.Throws<ArgumentOutOfRangeException>(() => new RedisGeoHash(90.0, 0.0));
            Assert.AreEqual("latitude", latEx.ParamName);
            StringAssert.Contains("Latitude must be between", latEx.Message);

            var lonEx = Assert.Throws<ArgumentOutOfRangeException>(() => new RedisGeoHash(0.0, 181.0));
            Assert.AreEqual("longitude", lonEx.ParamName);
            StringAssert.Contains("Longitude must be between", lonEx.Message);

            var arrEx = Assert.Throws<ArgumentException>(() => new RedisGeoHash(new[] {1.0}));
            StringAssert.Contains("Expected [latitude, longitude]", arrEx.Message);
        }

        [TestCase((ushort) 0, 0)]
        [TestCase((ushort) 33, 0)]
        [TestCase((ushort) 32, 1)]
        [TestCase((ushort) 1, 1)]
        public void EncodeStepGuards(ushort step, int expected)
        {
            var bits = new GeoHashBits();
            Assert.AreEqual(expected, RedisGeoHash.GeohashEncodeWgs84(TestLon, TestLat, step, ref bits));
        }

        private static GeoHashRange Range(double min, double max) =>
            new GeoHashRange {Min = min, Max = max};

        [TestCase(5.0, 5.0, 1)]     /* inside the custom ranges */
        [TestCase(10.0, 20.0, 1)]   /* exactly on the max bounds */
        [TestCase(-10.0, -20.0, 1)] /* exactly on the min bounds */
        [TestCase(15.0, 5.0, 0)]    /* latitude above the custom range */
        [TestCase(-15.0, 5.0, 0)]   /* latitude below the custom range */
        [TestCase(5.0, 25.0, 0)]    /* longitude above the custom range */
        [TestCase(5.0, -25.0, 0)]   /* longitude below the custom range */
        public void EncodeRespectsCustomRanges(double lat, double lon, int expected)
        {
            var bits = new GeoHashBits();
            var result = RedisGeoHash.GeohashEncode(
                Range(-20, 20), Range(-10, 10), lon, lat, 16, ref bits);
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void EncodeRejectsZeroWidthRanges()
        {
            var bits = new GeoHashBits();
            Assert.AreEqual(0, RedisGeoHash.GeohashEncode(Range(-20, 20), Range(0, 0), 5.0, 5.0, 16, ref bits));
            Assert.AreEqual(0, RedisGeoHash.GeohashEncode(Range(0, 0), Range(-10, 10), 5.0, 5.0, 16, ref bits));
        }

        [Test]
        public void RangesTouchingZeroAreNotTreatedAsEmpty()
        {
            /* {min, 0} and {0, max} have one bound at 0 but are valid ranges */
            var bits = new GeoHashBits();
            Assert.AreEqual(1, RedisGeoHash.GeohashEncode(Range(-20, 0), Range(-10, 0), -5.0, -5.0, 16, ref bits));
            Assert.AreEqual(1, RedisGeoHash.GeohashEncode(Range(0, 20), Range(0, 10), 5.0, 5.0, 16, ref bits));
        }

        [Test]
        public void ZeroBitsWithNonZeroStepIsAValidHash()
        {
            /* Bits == 0 at step 26 is the south-west corner cell, not "no hash" */
            var area = new GeoHashArea();
            var hash = new GeoHashBits {Bits = 0, Step = 26};

            Assert.AreEqual(1, RedisGeoHash.GeohashDecodeWgs84(hash, ref area));
            Assert.AreEqual(-85.05112878, area.Latitude.Min, 1e-9);
            Assert.AreEqual(-180.0, area.Longitude.Min, 1e-9);
        }

        [Test]
        public void DecodedCenterIsExactMidpointOfArea()
        {
            var x = new RedisGeoHash(TestLat, TestLon);
            var area = x.Decode();
            var xy = new double[2];

            Assert.AreEqual(1, RedisGeoHash.GeohashDecodeToLongLatWgs84(x.GeoHashBits, xy));
            Assert.AreEqual((area.Longitude.Min + area.Longitude.Max) / 2, xy[0]);
            Assert.AreEqual((area.Latitude.Min + area.Latitude.Max) / 2, xy[1]);
        }

        [Test]
        public void AllNeighborsAreAdjacentAcrossTheGlobe()
        {
            /* stay off the map edges: neighbor wrapping there is out of scope */
            for (double lat = -80; lat <= 80; lat += 16.7)
            {
                for (double lon = -170; lon <= 170; lon += 23.3)
                {
                    var x = new RedisGeoHash(lat, lon);
                    var n = x.Neighbors();

                    var c = DecodeArea(x.GeoHashBits);
                    var label = $"({lat}, {lon})";

                    AssertCell(DecodeArea(n.East), c.Latitude.Min, c.Longitude.Max, $"East of {label}");
                    AssertCell(DecodeArea(n.West), c.Latitude.Min, 2 * c.Longitude.Min - c.Longitude.Max, $"West of {label}");
                    AssertCell(DecodeArea(n.North), c.Latitude.Max, c.Longitude.Min, $"North of {label}");
                    AssertCell(DecodeArea(n.South), 2 * c.Latitude.Min - c.Latitude.Max, c.Longitude.Min, $"South of {label}");
                    AssertCell(DecodeArea(n.NorthEast), c.Latitude.Max, c.Longitude.Max, $"NorthEast of {label}");
                    AssertCell(DecodeArea(n.NorthWest), c.Latitude.Max, 2 * c.Longitude.Min - c.Longitude.Max, $"NorthWest of {label}");
                    AssertCell(DecodeArea(n.SouthEast), 2 * c.Latitude.Min - c.Latitude.Max, c.Longitude.Max, $"SouthEast of {label}");
                    AssertCell(DecodeArea(n.SouthWest), 2 * c.Latitude.Min - c.Latitude.Max, 2 * c.Longitude.Min - c.Longitude.Max, $"SouthWest of {label}");
                }
            }
        }

        /* Cell indices with long runs of trailing ones/zeros force the
         * carry/borrow in GeohashMoveX/GeohashMoveY to propagate through many
         * interleaved bits — the cases plain grid points rarely hit. */
        [TestCase(0xFFFFu, 0xFFFFu)]
        [TestCase(0x10000u, 0x10000u)]
        [TestCase(0xFFFFFu, 0x100000u)]
        [TestCase(0x100000u, 0xFFFFFu)]
        [TestCase(0x3FFFFFu, 0x3FFFFFu)]
        [TestCase(0x400000u, 0x400000u)]
        public void NeighborsAreAdjacentAtCarryChainBoundaries(uint latIdx, uint lonIdx)
        {
            const double latMin = -85.05112878, latMax = 85.05112878;
            const double lonMin = -180.0, lonMax = 180.0;
            const double cells = 1ul << 26;

            /* center of the cell with the given indices */
            double lat = latMin + (latIdx + 0.5) / cells * (latMax - latMin);
            double lon = lonMin + (lonIdx + 0.5) / cells * (lonMax - lonMin);

            var x = new RedisGeoHash(lat, lon);
            var n = x.Neighbors();
            var c = DecodeArea(x.GeoHashBits);
            var label = $"idx ({latIdx}, {lonIdx})";

            AssertCell(DecodeArea(n.East), c.Latitude.Min, c.Longitude.Max, $"East of {label}");
            AssertCell(DecodeArea(n.West), c.Latitude.Min, 2 * c.Longitude.Min - c.Longitude.Max, $"West of {label}");
            AssertCell(DecodeArea(n.North), c.Latitude.Max, c.Longitude.Min, $"North of {label}");
            AssertCell(DecodeArea(n.South), 2 * c.Latitude.Min - c.Latitude.Max, c.Longitude.Min, $"South of {label}");
        }

        private static GeoHashArea DecodeArea(GeoHashBits bits)
        {
            var area = new GeoHashArea();
            Assert.AreEqual(1, RedisGeoHash.GeohashDecodeWgs84(bits, ref area));
            return area;
        }

        private static void AssertCell(GeoHashArea cell, double expectedLatMin, double expectedLonMin, string label)
        {
            Assert.AreEqual(expectedLatMin, cell.Latitude.Min, 1e-8, label + " latitude");
            Assert.AreEqual(expectedLonMin, cell.Longitude.Min, 1e-8, label + " longitude");
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
