using System;
using KexEdit.Sim.Schema;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Document {
    public static class NodeMeta {
        public const int OverrideHeart = 240;
        public const int OverrideFriction = 241;
        public const int OverrideResistance = 242;
        public const int OverrideTrackStyle = 243;
        public const int Duration = 248;
        public const int Priority = 249;
        public const int DurationType = 250;
        public const int Facing = 251;
        public const int Steering = 252;
        public const int Driven = 253;
        public const int Render = 254;
    }

    [BurstCompile]
    public struct Document : IDisposable {
        public KexEdit.Graph.Graph Graph;
        public KeyframeStore Keyframes;
        public NativeHashMap<ulong, float> Scalars;
        public NativeHashMap<ulong, float3> Vectors;
        public NativeHashMap<ulong, int> Flags;

        [BurstCompile]
        public static ulong InputKey(uint nodeId, int inputIndex) =>
            ((ulong)nodeId << 8) | (byte)inputIndex;

        [BurstCompile]
        public static void UnpackInputKey(ulong key, out uint nodeId, out int inputIndex) {
            nodeId = (uint)(key >> 8);
            inputIndex = (int)(key & 0xFF);
        }

        public static Document Create(Allocator allocator) {
            return new Document {
                Graph = KexEdit.Graph.Graph.Create(allocator),
                Keyframes = KeyframeStore.Create(allocator),
                Scalars = new NativeHashMap<ulong, float>(16, allocator),
                Vectors = new NativeHashMap<ulong, float3>(16, allocator),
                Flags = new NativeHashMap<ulong, int>(16, allocator),
            };
        }

        public void Dispose() {
            if (Graph.NodeIds.IsCreated) Graph.Dispose();
            if (Keyframes.Keyframes.IsCreated) Keyframes.Dispose();
            if (Scalars.IsCreated) Scalars.Dispose();
            if (Vectors.IsCreated) Vectors.Dispose();
            if (Flags.IsCreated) Flags.Dispose();
        }
    }
}
