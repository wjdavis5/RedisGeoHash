namespace GeoHash
{
    /// <summary>
    /// Represents a geographic area defined by a GeoHash and its latitude/longitude ranges.
    /// </summary>
    public struct GeoHashArea
    {
        /// <summary>
        /// The GeoHash bits for this area.
        /// </summary>
        public GeoHashBits Hash;
        
        /// <summary>
        /// The longitude range of this area.
        /// </summary>
        public GeoHashRange Longitude;
        
        /// <summary>
        /// The latitude range of this area.
        /// </summary>
        public GeoHashRange Latitude;
    }
}