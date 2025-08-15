using Unity.Entities;

namespace KexEdit {
    public struct ReadNormalForce : IBufferElementData {
        public float Value;

        public static implicit operator float(ReadNormalForce force) => force.Value;
        public static implicit operator ReadNormalForce(float value) => new() { Value = value };
    }
}
