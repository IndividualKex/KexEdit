using System;
using KexEdit.Nodes.Storage;
using KexGraph;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Coaster {
    public enum DurationType : byte { Time, Distance }

    [BurstCompile]
    public readonly struct Duration {
        public readonly float Value;
        public readonly DurationType Type;

        public Duration(float value, DurationType type) {
            Value = value;
            Type = type;
        }
    }

    public static class NodeMeta {
        public const int Duration = 248;
        public const int Priority = 249;
        public const int DurationType = 250;
        public const int Facing = 251;
        public const int Steering = 252;
        public const int Driven = 253;
        public const int Render = 254;
    }

    [BurstCompile]
    public struct Coaster : IDisposable {
        public Graph Graph;
        public KeyframeStore Keyframes;
        public NativeHashMap<ulong, int> Flags;
        public NativeHashMap<ulong, float> Scalars;
        public NativeHashMap<ulong, float3> Vectors;

        [BurstCompile]
        public static ulong InputKey(uint nodeId, int inputIndex) =>
            ((ulong)nodeId << 8) | (byte)inputIndex;

        [BurstCompile]
        public static void UnpackInputKey(ulong key, out uint nodeId, out int inputIndex) {
            nodeId = (uint)(key >> 8);
            inputIndex = (int)(key & 0xFF);
        }

        public static Coaster Create(Allocator allocator) {
            return new Coaster {
                Graph = Graph.Create(allocator),
                Keyframes = KeyframeStore.Create(allocator),
                Flags = new NativeHashMap<ulong, int>(16, allocator),
                Scalars = new NativeHashMap<ulong, float>(16, allocator),
                Vectors = new NativeHashMap<ulong, float3>(16, allocator),
            };
        }

        public void Dispose() {
            if (Graph.NodeIds.IsCreated) Graph.Dispose();
            if (Keyframes.Keyframes.IsCreated) Keyframes.Dispose();
            if (Flags.IsCreated) Flags.Dispose();
            if (Scalars.IsCreated) Scalars.Dispose();
            if (Vectors.IsCreated) Vectors.Dispose();
        }
    }
}
