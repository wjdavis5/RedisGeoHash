using System;

namespace GeoHash
{
    public class RedisGeoHash
    {
        private const double GeoLatMin = -85.05112878;
        private const double GeoLatMax = 85.05112878;
        private const double GeoLongMin = -180;
        private const double GeoLongMax = 180;
        public const int GeoStepMax = 26;

        public double Latitude { get; }
        public double Longitude { get; }

        public ulong GeoHash => GeoHashBits.Bits;

        public GeoHashBits GeoHashBits { get; }

        /* [latitude, longitude] — matches the original public API of this
         * library, even though Redis commands take longitude first. */
        public double[] LatLon => new[] { Latitude, Longitude };

        public RedisGeoHash(double latitude, double longitude)
        {
            if (double.IsNaN(latitude) || latitude < GeoLatMin || latitude > GeoLatMax)
                throw new ArgumentOutOfRangeException(nameof(latitude), latitude,
                    $"Latitude must be between {GeoLatMin} and {GeoLatMax}.");
            if (double.IsNaN(longitude) || longitude < GeoLongMin || longitude > GeoLongMax)
                throw new ArgumentOutOfRangeException(nameof(longitude), longitude,
                    $"Longitude must be between {GeoLongMin} and {GeoLongMax}.");

            Latitude = latitude;
            Longitude = longitude;
            var bits = new GeoHashBits();
            GeohashEncodeWgs84(longitude, latitude, GeoStepMax, ref bits);
            GeoHashBits = bits;
        }

        public RedisGeoHash(double[] latlon)
            : this(Validate(latlon)[0], latlon[1])
        {
        }

        private static double[] Validate(double[] latlon)
        {
            if (latlon == null)
                throw new ArgumentNullException(nameof(latlon));
            if (latlon.Length != 2)
                throw new ArgumentException("Expected [latitude, longitude].", nameof(latlon));
            return latlon;
        }

        public override string ToString()
        {
            return GeoHash.ToString();
        }

        private static bool Hashiszero(GeoHashBits r)
        {
            return r.Bits == 0 && r.Step == 0;
        }

        private static bool Rangeiszero(GeoHashRange r)
        {
            return r.Max == 0.0 && r.Min == 0.0;
        }

        private static ulong Interleave64(uint xlo, uint ylo)
        {
            ulong[] b =
            {
                0x5555555555555555UL, 0x3333333333333333UL, 0x0F0F0F0F0F0F0F0FUL, 0x00FF00FF00FF00FFUL,
                0x0000FFFF0000FFFFUL
            };
            int[] s = {1, 2, 4, 8, 16};

            ulong x = xlo;
            ulong y = ylo;

            x = (x | (x << s[4])) & b[4];
            y = (y | (y << s[4])) & b[4];

            x = (x | (x << s[3])) & b[3];
            y = (y | (y << s[3])) & b[3];

            x = (x | (x << s[2])) & b[2];
            y = (y | (y << s[2])) & b[2];

            x = (x | (x << s[1])) & b[1];
            y = (y | (y << s[1])) & b[1];

            x = (x | (x << s[0])) & b[0];
            y = (y | (y << s[0])) & b[0];

            return x | (y << 1);
        }

        /* reverse the interleave process
         * derived from http://stackoverflow.com/questions/4909263
         */
        private static ulong Deinterleave64(ulong interleaved)
        {
            ulong[] b =
            {
                0x5555555555555555UL, 0x3333333333333333UL, 0x0F0F0F0F0F0F0F0FUL, 0x00FF00FF00FF00FFUL,
                0x0000FFFF0000FFFFUL, 0x00000000FFFFFFFFUL
            };
            int[] s = {1, 2, 4, 8, 16};

            ulong x = interleaved;
            ulong y = interleaved >> 1;

            x = x & b[0];
            y = y & b[0];

            x = (x | (x >> s[0])) & b[1];
            y = (y | (y >> s[0])) & b[1];

            x = (x | (x >> s[1])) & b[2];
            y = (y | (y >> s[1])) & b[2];

            x = (x | (x >> s[2])) & b[3];
            y = (y | (y >> s[2])) & b[3];

            x = (x | (x >> s[3])) & b[4];
            y = (y | (y >> s[3])) & b[4];

            x = (x | (x >> s[4])) & b[5];
            y = (y | (y >> s[4])) & b[5];

            return x | (y << 32);
        }

        private static void GeohashGetCoordRange(ref GeoHashRange longRange, ref GeoHashRange latRange)
        {
            /* These are constraints from EPSG:900913 / EPSG:3785 / OSGEO:41001 */
            /* We can't geocode at the north/south pole. */
            longRange.Max = GeoLongMax;
            longRange.Min = GeoLongMin;
            latRange.Max = GeoLatMax;
            latRange.Min = GeoLatMin;
        }

        public static int GeohashEncode(GeoHashRange longRange, GeoHashRange latRange, double longitude,
            double latitude, ushort step, ref GeoHashBits hash)
        {
            /* Check basic arguments sanity. */
            if (step > 32 || step == 0 ||
                Rangeiszero(latRange) || Rangeiszero(longRange)) return 0;

            /* Return an error when trying to index outside the supported
             * constraints. */
            if (longitude > GeoLongMax) return 0;
            if (longitude < GeoLongMin) return 0;
            if (latitude > GeoLatMax) return 0;
            if (latitude < GeoLatMin) return 0;

            hash.Bits = 0;
            hash.Step = step;

            if (latitude < latRange.Min || latitude > latRange.Max ||
                longitude < longRange.Min || longitude > longRange.Max)
            {
                return 0;
            }

            double latOffset =
                (latitude - latRange.Min) / (latRange.Max - latRange.Min);
            double longOffset =
                (longitude - longRange.Min) / (longRange.Max - longRange.Min);

            /* convert to fixed point based on the step size */
            latOffset *= (1UL << step);
            longOffset *= (1UL << step);
            hash.Bits = Interleave64((uint) latOffset, (uint) longOffset);
            return 1;
        }

        public static int GeohashEncodeType(double longitude, double latitude, ushort step, ref GeoHashBits hash)
        {
            GeoHashRange[] r = new GeoHashRange[2];
            GeohashGetCoordRange(ref r[0], ref r[1]);
            return GeohashEncode(r[0], r[1], longitude, latitude, step, ref hash);
        }

        public static int GeohashEncodeWgs84(double longitude, double latitude, ushort step,
            ref GeoHashBits hash)
        {
            return GeohashEncodeType(longitude, latitude, step, ref hash);
        }

        public static int GeohashDecode(GeoHashRange longRange, GeoHashRange latRange, GeoHashBits hash,
            ref GeoHashArea area)
        {
            if (Hashiszero(hash) || Rangeiszero(latRange) ||
                Rangeiszero(longRange))
            {
                return 0;
            }

            area.Hash = hash;
            ushort step = hash.Step;
            ulong hashSep = Deinterleave64(hash.Bits); /* hash = [LAT][LONG] */

            double latScale = latRange.Max - latRange.Min;
            double longScale = longRange.Max - longRange.Min;

            /* the C original assigns to uint32_t here, truncating each half */
            uint ilato = (uint) hashSep; /* get lat part of deinterleaved hash */
            uint ilono = (uint) (hashSep >> 32); /* shift over to get long part of hash */

            /* divide by 2**step.
             * Then, for 0-1 coordinate, multiply times scale and add
               to the min to get the absolute coordinate. */
            area.Latitude.Min =
                latRange.Min + ((double) ilato / (1ul << step)) * latScale;
            area.Latitude.Max =
                latRange.Min + ((double) (ilato + 1) / (1ul << step)) * latScale;
            area.Longitude.Min =
                longRange.Min + ((double) ilono / (1ul << step)) * longScale;
            area.Longitude.Max =
                longRange.Min + ((double) (ilono + 1) / (1ul << step)) * longScale;

            return 1;
        }

        public static int GeohashDecodeType(GeoHashBits hash, ref GeoHashArea area)
        {
            GeoHashRange[] r = new GeoHashRange[2];
            GeohashGetCoordRange(ref r[0], ref r[1]);
            return GeohashDecode(r[0], r[1], hash, ref area);
        }

        public static int GeohashDecodeWgs84(GeoHashBits hash, ref GeoHashArea area)
        {
            return GeohashDecodeType(hash, ref area);
        }

        public static int GeohashDecodeAreaToLongLat(GeoHashArea area, double[] xy)
        {
            xy[0] = (area.Longitude.Min + area.Longitude.Max) / 2;
            xy[1] = (area.Latitude.Min + area.Latitude.Max) / 2;
            return 1;
        }

        public static int GeohashDecodeToLongLatType(GeoHashBits hash, double[] xy)
        {
            GeoHashArea area = new GeoHashArea();
            if (GeohashDecodeType(hash, ref area) == 0)
                return 0;
            return GeohashDecodeAreaToLongLat(area, xy);
        }

        public static int GeohashDecodeToLongLatWgs84(GeoHashBits hash, double[] xy)
        {
            return GeohashDecodeToLongLatType(hash, xy);
        }

        /* d is always +1 or -1; the C original's d == 0 no-op calls are elided */
        private static void GeohashMoveX(ref GeoHashBits hash, short d)
        {
            ulong x = hash.Bits & 0xaaaaaaaaaaaaaaaaUL;
            ulong y = hash.Bits & 0x5555555555555555UL;

            ulong zz = 0x5555555555555555UL >> (64 - hash.Step * 2);

            // Stryker disable once equality : d is always +1 or -1, so > and >= are equivalent
            if (d > 0)
            {
                x = x + (zz + 1);
            }
            else
            {
                x = x | zz;
                x = x - (zz + 1);
            }

            x &= (0xaaaaaaaaaaaaaaaaUL >> (64 - hash.Step * 2));
            hash.Bits = (x | y);
        }

        private static void GeohashMoveY(ref GeoHashBits hash, short d)
        {
            ulong x = hash.Bits & 0xaaaaaaaaaaaaaaaaUL;
            ulong y = hash.Bits & 0x5555555555555555UL;

            ulong zz = 0xaaaaaaaaaaaaaaaaUL >> (64 - hash.Step * 2);
            // Stryker disable once equality : d is always +1 or -1, so > and >= are equivalent
            if (d > 0)
            {
                y = y + (zz + 1);
            }
            else
            {
                y = y | zz;
                y = y - (zz + 1);
            }

            y &= (0x5555555555555555UL >> (64 - hash.Step * 2));
            hash.Bits = (x | y);
        }

        public static void GeohashNeighbors(GeoHashBits hash, ref GeoHashNeighbors neighbors)
        {
            neighbors.East = hash;
            neighbors.West = hash;
            neighbors.North = hash;
            neighbors.South = hash;
            neighbors.SouthEast = hash;
            neighbors.SouthWest = hash;
            neighbors.NorthEast = hash;
            neighbors.NorthWest = hash;

            GeohashMoveX(ref neighbors.East, 1);

            GeohashMoveX(ref neighbors.West, -1);

            GeohashMoveY(ref neighbors.South, -1);

            GeohashMoveY(ref neighbors.North, 1);

            GeohashMoveX(ref neighbors.NorthWest, -1);
            GeohashMoveY(ref neighbors.NorthWest, 1);

            GeohashMoveX(ref neighbors.NorthEast, 1);
            GeohashMoveY(ref neighbors.NorthEast, 1);

            GeohashMoveX(ref neighbors.SouthEast, 1);
            GeohashMoveY(ref neighbors.SouthEast, -1);

            GeohashMoveX(ref neighbors.SouthWest, -1);
            GeohashMoveY(ref neighbors.SouthWest, -1);
        }

        public GeoHashNeighbors Neighbors()
        {
            var neighbors = new GeoHashNeighbors();
            GeohashNeighbors(GeoHashBits, ref neighbors);
            return neighbors;
        }

        public GeoHashArea Decode()
        {
            var area = new GeoHashArea();
            GeohashDecodeWgs84(GeoHashBits, ref area);
            return area;
        }
    }
}
