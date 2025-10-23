namespace GeoHash
{
    /// <summary>
    /// Represents the bits and step information for a GeoHash.
    /// </summary>
    public struct GeoHashBits
    {
        /// <summary>
        /// The GeoHash value as an unsigned 64-bit integer.
        /// </summary>
        public ulong Bits;
        
        /// <summary>
        /// The step/precision level of the GeoHash.
        /// </summary>
        public ushort Step;
    }
}