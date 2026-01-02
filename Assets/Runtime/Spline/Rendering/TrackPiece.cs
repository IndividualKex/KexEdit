using System;

namespace KexEdit.Spline.Rendering {
    public readonly struct TrackPiece : IComparable<TrackPiece> {
        public readonly float NominalLength;
        public readonly int MeshIndex;

        public TrackPiece(float nominalLength, int meshIndex) {
            NominalLength = nominalLength;
            MeshIndex = meshIndex;
        }

        // Sort largest to smallest for greedy algorithm
        public int CompareTo(TrackPiece other) => other.NominalLength.CompareTo(NominalLength);
    }
}
