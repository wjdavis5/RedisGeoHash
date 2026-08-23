using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using GeoHash;

namespace Benchmarks
{
    public class Program
    {
        public static void Main(string[] args) =>
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    [MemoryDiagnoser]
    public class GeoHashBenchmarks
    {
        private const double Lat = 39.923422;
        private const double Lon = -86.1078998;

        private RedisGeoHash _hash = null!;
        private readonly double[] _xy = new double[2];

        [GlobalSetup]
        public void Setup() => _hash = new RedisGeoHash(Lat, Lon);

        [Benchmark]
        public RedisGeoHash Construct() => new RedisGeoHash(Lat, Lon);

        [Benchmark]
        public ulong EncodeRaw()
        {
            var bits = new GeoHashBits();
            RedisGeoHash.GeohashEncodeWgs84(Lon, Lat, RedisGeoHash.GeoStepMax, ref bits);
            return bits.Bits;
        }

        [Benchmark]
        public GeoHashArea Decode() => _hash.Decode();

        [Benchmark]
        public double DecodeToLongLat()
        {
            RedisGeoHash.GeohashDecodeToLongLatWgs84(_hash.GeoHashBits, _xy);
            return _xy[0];
        }

        [Benchmark]
        public GeoHashNeighbors Neighbors() => _hash.Neighbors();
    }
}
