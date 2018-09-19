using System;
using System.Collections.Generic;
using System.Text;
// ReSharper disable IdentifierTypo

namespace GeoHash
{
    public class RedisGeoHash
    {

        private const double GeoLatMin = -85.05112878;
        private const double GeoLatMax = 85.05112878;
        private const double GeoLongMin = -180;
        private const double GeoLongMax = 180;
        public const int GeoStepMax = 26;

        public double[] LatLon { get; private set; }
        public double Latitude => LatLon[0];
        public double Longitude => LatLon[1];

        public ulong GeoHash => GeoHashBits.Bits;

        public GeoHashBits GeoHashBits { get; private set; }

        public RedisGeoHash(double[] latlon)
        {
            LatLon = latlon;
            var bits = new GeoHashBits();
            var res = GeohashEncodeWgs84(Longitude, Latitude, RedisGeoHash.GeoStepMax, ref bits);
            GeoHashBits = bits;
        }

        private  bool Hashiszero(GeoHashBits r)
        {
            return r.Bits == 0 && r.Step == 0;
        }

        private  bool Rangepiszero(GeoHashRange r)
        {
            return Rangeiszero(r);
        }

        private  bool Rangeiszero(GeoHashRange r)
        {

            return r.Max == 0.0 && r.Min == 0.0;
        }

         ulong Interleave64(uint xlo, uint ylo)
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
         ulong Deinterleave64(ulong interleaved)
        {
            ulong[] b =
            {
                0x5555555555555555UL, 0x3333333333333333UL, 0x0F0F0F0F0F0F0F0FUL, 0x00FF00FF00FF00FFUL,
                0x0000FFFF0000FFFFUL, 0x00000000FFFFFFFFUL
            };
            int[] s = {0, 1, 2, 4, 8, 16};

            ulong x = interleaved;
            ulong y = interleaved >> 1;

            x = (x | (x >> s[0])) & b[0];
            y = (y | (y >> s[0])) & b[0];

            x = (x | (x >> s[1])) & b[1];
            y = (y | (y >> s[1])) & b[1];

            x = (x | (x >> s[2])) & b[2];
            y = (y | (y >> s[2])) & b[2];

            x = (x | (x >> s[3])) & b[3];
            y = (y | (y >> s[3])) & b[3];

            x = (x | (x >> s[4])) & b[4];
            y = (y | (y >> s[4])) & b[4];

            x = (x | (x >> s[5])) & b[5];
            y = (y | (y >> s[5])) & b[5];

            return x | (y << 32);
        }

         void GeohashGetCoordRange(ref GeoHashRange longRange, ref GeoHashRange latRange)
        {
            /* These are constraints from EPSG:900913 / EPSG:3785 / OSGEO:41001 */
            /* We can't geocode at the north/south pole. */
            longRange.Max = GeoLongMax;
            longRange.Min = GeoLongMin;
            latRange.Max = GeoLatMax;
            latRange.Min = GeoLatMin;
        }

         int GeohashEncode(GeoHashRange longRange, GeoHashRange latRange, double longitude, double latitude,
            ushort step, ref GeoHashBits hash)
        {
            /* Check basic arguments sanity. */
            if (step > 32 || step == 0 ||
                Rangepiszero(latRange) || Rangepiszero(longRange)) return 0;

            /* Return an error when trying to index outside the supported
             * constraints. */
            if (longitude > GeoLongMax) return 0;
            if (longitude < GeoLongMin) return 0;
            if(latitude > GeoLatMax)return 0;
            if( latitude < GeoLatMin) return 0;

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

         int GeohashEncodeType(double longitude, double latitude, ushort step, ref GeoHashBits hash)
        {
            GeoHashRange[] r = new GeoHashRange[2];
            GeohashGetCoordRange(ref r[0], ref r[1]);
            return GeohashEncode(r[0], r[1], longitude, latitude, step, ref hash);

        }

        public  int GeohashEncodeWgs84(double longitude, double latitude, ushort step,
            ref GeoHashBits hash)
        {
            return GeohashEncodeType(longitude, latitude, step, ref hash);
        }

         int GeohashDecode(GeoHashRange longRange, GeoHashRange latRange, GeoHashBits hash, GeoHashArea area)
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

            ulong ilato = hashSep; /* get lat part of deinterleaved hash */
            ulong ilono = hashSep >> 32; /* shift over to get long part of hash */

            /* divide by 2**step.
             * Then, for 0-1 coordinate, multiply times scale and add
               to the min to get the absolute coordinate. */
            area.Latitude.Min =
                latRange.Min + (ilato * 1.0 / (1ul << step)) * latScale;
            area.Latitude.Max =
                latRange.Min + ((ilato + 1) * 1.0 / (1ul << step)) * latScale;
            area.Longitude.Min =
                longRange.Min + (ilono * 1.0 / (1ul << step)) * longScale;
            area.Longitude.Max =
                longRange.Min + ((ilono + 1) * 1.0 / (1ul << step)) * longScale;

            return 1;
        }

         int GeohashDecodeType(GeoHashBits hash, GeoHashArea area)
        {
            GeoHashRange[] r = new GeoHashRange[2];
                GeohashGetCoordRange(ref r[0], ref r[1]);
                return GeohashDecode(r[0], r[1], hash, area);
        }

         int GeohashDecodeWgs84(GeoHashBits hash, GeoHashArea area)
        {
            return GeohashDecodeType(hash, area);
        }

         int GeohashDecodeAreaToLongLat(GeoHashArea area, double[] xy)
        {
            xy[0] = (area.Longitude.Min + area.Longitude.Max) / 2;
            xy[1] = (area.Latitude.Min + area.Latitude.Max) / 2;
            return 1;
        }

         int GeohashDecodeToLongLatType(GeoHashBits hash, double[] xy)
        {
            GeoHashArea area = new GeoHashArea();
            if (GeohashDecodeType(hash, area) == 0)
                return 0;
            return GeohashDecodeAreaToLongLat(area, xy);
        }

         int GeohashDecodeToLongLatWgs84(GeoHashBits hash, double[] xy)
        {
            return GeohashDecodeToLongLatType(hash, xy);
        }

         void geohash_move_x(GeoHashBits hash, short d)
        {
            if (d == 0)
                return;

            ulong x = hash.Bits & 0xaaaaaaaaaaaaaaaaUL;
            ulong y = hash.Bits & 0x5555555555555555UL;

            ulong zz = 0x5555555555555555UL >> (64 - hash.Step * 2);

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

         void geohash_move_y(GeoHashBits hash, short d)
        {
            if (d == 0)
                return;

            ulong x = hash.Bits & 0xaaaaaaaaaaaaaaaaUL;
            ulong y = hash.Bits & 0x5555555555555555UL;

            ulong zz = 0xaaaaaaaaaaaaaaaaUL >> (64 - hash.Step * 2);
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

          void GeohashNeighbors(GeoHashBits hash, GeoHashNeighbors neighbors)
        {
            neighbors.East = hash;
            neighbors.West = hash;
            neighbors.North = hash;
            neighbors.South = hash;
            neighbors.SouthEast = hash;
            neighbors.SouthWest = hash;
            neighbors.NorthEast = hash;
            neighbors.NorthWest = hash;

            geohash_move_x(neighbors.East, 1);
            geohash_move_y(neighbors.East, 0);

            geohash_move_x(neighbors.West, -1);
            geohash_move_y(neighbors.West, 0);

            geohash_move_x(neighbors.South, 0);
            geohash_move_y(neighbors.South, -1);

            geohash_move_x(neighbors.North, 0);
            geohash_move_y(neighbors.North, 1);

            geohash_move_x(neighbors.NorthWest, -1);
            geohash_move_y(neighbors.NorthWest, 1);

            geohash_move_x(neighbors.NorthEast, 1);
            geohash_move_y(neighbors.NorthEast, 1);

            geohash_move_x(neighbors.SouthEast, 1);
            geohash_move_y(neighbors.SouthEast, -1);

            geohash_move_x(neighbors.SouthWest, -1);
            geohash_move_y(neighbors.SouthWest, -1);
        }
        
    }
}
