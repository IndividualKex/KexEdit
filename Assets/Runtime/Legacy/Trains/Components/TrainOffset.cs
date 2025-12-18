using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.Legacy {
    public struct TrainOffset : IComponentData {
        public float3 Position;

        public static TrainOffset Default => new() { Position = float3.zero };
    }
}
