namespace GeoHash
{
    /// <summary>
    /// Represents the neighboring GeoHashes in all eight directions.
    /// </summary>
    public struct GeoHashNeighbors
    {
        /// <summary>
        /// The GeoHash to the north.
        /// </summary>
        public GeoHashBits North;
        
        /// <summary>
        /// The GeoHash to the east.
        /// </summary>
        public GeoHashBits East;
        
        /// <summary>
        /// The GeoHash to the west.
        /// </summary>
        public GeoHashBits West;
        
        /// <summary>
        /// The GeoHash to the south.
        /// </summary>
        public GeoHashBits South;
        
        /// <summary>
        /// The GeoHash to the northeast.
        /// </summary>
        public GeoHashBits NorthEast;
        
        /// <summary>
        /// The GeoHash to the southeast.
        /// </summary>
        public GeoHashBits SouthEast;
        
        /// <summary>
        /// The GeoHash to the northwest.
        /// </summary>
        public GeoHashBits NorthWest;
        
        /// <summary>
        /// The GeoHash to the southwest.
        /// </summary>
        public GeoHashBits SouthWest;
    }
}