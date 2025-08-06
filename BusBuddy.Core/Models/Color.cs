namespace BusBuddy.Core.Models
{
    /// <summary>
    /// Core color structure for Bus Buddy application
    /// Represents ARGB color values without UI framework dependencies
    /// Can be converted to WPF colors in the UI layer
    /// </summary>
    public struct Color
    {
        /// <summary>
        /// Red component (0-255)
        /// </summary>
        public byte R { get; set; }

        /// <summary>
        /// Green component (0-255)
        /// </summary>
        public byte G { get; set; }

        /// <summary>
        /// Blue component (0-255)
        /// </summary>
        public byte B { get; set; }

        /// <summary>
        /// Alpha component (0-255, where 255 is fully opaque)
        /// </summary>
        public byte A { get; set; }

        /// <summary>
        /// Creates a new Color with the specified ARGB values
        /// </summary>
        public Color(byte a, byte r, byte g, byte b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        /// <summary>
        /// Creates a new Color with the specified RGB values (fully opaque)
        /// </summary>
        public Color(byte r, byte g, byte b) : this(255, r, g, b)
        {
        }

        /// <summary>
        /// Returns a hexadecimal string representation of the color
        /// </summary>
        public override string ToString()
        {
            return $"#{A:X2}{R:X2}{G:X2}{B:X2}";
        }

        /// <summary>
        /// Creates a Color from ARGB integer value
        /// </summary>
        public static Color FromArgb(int argb)
        {
            return new Color(
                (byte)((argb >> 24) & 0xFF),  // Alpha
                (byte)((argb >> 16) & 0xFF),  // Red
                (byte)((argb >> 8) & 0xFF),   // Green
                (byte)(argb & 0xFF)           // Blue
            );
        }

        /// <summary>
        /// Converts to ARGB integer value
        /// </summary>
        public int ToArgb()
        {
            return (A << 24) | (R << 16) | (G << 8) | B;
        }

        /// <summary>
        /// Equality comparison
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is Color color &&
                   A == color.A &&
                   R == color.R &&
                   G == color.G &&
                   B == color.B;
        }

        /// <summary>
        /// Hash code generation
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(A, R, G, B);
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        public static bool operator ==(Color left, Color right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        public static bool operator !=(Color left, Color right)
        {
            return !left.Equals(right);
        }
    }
}
