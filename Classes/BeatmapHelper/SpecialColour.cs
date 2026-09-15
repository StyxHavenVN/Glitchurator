using System;

namespace StandalonePicturator.Classes.BeatmapHelper
{
    public class SpecialColour : IEquatable<SpecialColour>
    {
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }
        public string Name { get; set; }

        public SpecialColour(int r, int g, int b, string name)
        {
            R = r; G = g; B = b; Name = name;
        }

        public bool Equals(SpecialColour other) => other != null && R == other.R && G == other.G && B == other.B && Name == other.Name;
        public override bool Equals(object obj) => Equals(obj as SpecialColour);
        public override int GetHashCode() => HashCode.Combine(R, G, B, Name);
    }
}