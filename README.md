# RedisGeoHash
A library for creating RedisGeo Hash values within .NET


# Why?
When I was involved in the writting of the Stackexchange.Redis Geo functionality I become interested in how they are using GeoHashes to store
information in sorted sets. Behind the scense a RedisGeo collection is nothing more than a SortSet. They use a numerical represenation of 
GeoHashes to achieve this. I wanted to do the same thing from within .NET to implemented local GEO collections for caching purposes at work.\


# How?
Below is a code snip from the test case included in the project. I compared the output from this application to the output found in Redis.

```
public void Test()
        {
            double lat = 39.923422, lon = -86.1078998;
            var x = new RedisGeoHash(new []{lat,lon});
            Console.WriteLine(x.ToString());
            Console.ReadLine();
        }
            
			
    }

```

**Compared to Redis:**
```
will@will-laptop:/mnt/c/Users/wdavis$ redis-cli
127.0.0.1:6379> geoadd willt -86.1078998 39.923422 1
(integer) 1
127.0.0.1:6379> zrange willt 0 100 WITHSCORES
1) "1"
2) "1782901374540128"
```
