# RedisGeoHash

A library for creating RedisGeo Hash values within .NET.

# Credits

This is a C# port of the Redis Geo Hash code by antirez:
https://github.com/antirez/redis

# Why?

When I was involved in the writing of the StackExchange.Redis Geo
functionality I became interested in how Redis uses geohashes to store
information in sorted sets. Behind the scenes a RedisGeo collection is nothing
more than a sorted set: Redis uses a numerical representation of geohashes as
the member scores. I wanted to do the same thing from within .NET to implement
local GEO collections for caching purposes at work.

# How?

```csharp
using GeoHash;

var hash = new RedisGeoHash(39.923422, -86.1078998); // latitude, longitude
Console.WriteLine(hash.GeoHash); // 1782901374540128
```

**Compared to Redis:**

```
127.0.0.1:6379> GEOADD willt -86.1078998 39.923422 1
(integer) 1
127.0.0.1:6379> ZRANGE willt 0 100 WITHSCORES
1) "1"
2) "1782901374540128"
```

Note that Redis commands take longitude first; this library's constructor
takes latitude first. Coordinates outside the supported range (latitude
±85.05112878, longitude ±180) throw `ArgumentOutOfRangeException`.

## Decoding

`Decode()` returns the bounding box of the geohash cell, and
`GeohashDecodeToLongLatWgs84` returns its center point:

```csharp
var area = hash.Decode();
// area.Latitude.Min/Max and area.Longitude.Min/Max bound the original point

var xy = new double[2];
RedisGeoHash.GeohashDecodeToLongLatWgs84(hash.GeoHashBits, xy);
// xy[0] = longitude, xy[1] = latitude of the cell center
```

## Neighbors

`Neighbors()` returns the eight adjacent cells:

```csharp
var neighbors = hash.Neighbors();
Console.WriteLine(neighbors.North.Bits);
```

# Building and testing

```
dotnet build
dotnet test
```
