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

    [BurstCompile]
    public struct Coaster : IDisposable {
        public Graph Graph;
        public KeyframeStore Keyframes;
        public NativeHashMap<ulong, float> Scalars;
        public NativeHashMap<ulong, float3> Vectors;
        public NativeHashMap<uint, Duration> Durations;
        public NativeHashMap<uint, int> Facing;
        public NativeHashSet<uint> Steering;
        public NativeHashSet<uint> Driven;
        public NativeHashMap<uint, int> Priority;
        public NativeHashSet<uint> Render;

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
                Scalars = new NativeHashMap<ulong, float>(16, allocator),
                Vectors = new NativeHashMap<ulong, float3>(16, allocator),
                Durations = new NativeHashMap<uint, Duration>(16, allocator),
                Facing = new NativeHashMap<uint, int>(16, allocator),
                Steering = new NativeHashSet<uint>(8, allocator),
                Driven = new NativeHashSet<uint>(8, allocator),
                Priority = new NativeHashMap<uint, int>(16, allocator),
                Render = new NativeHashSet<uint>(8, allocator),
            };
        }

        public void Dispose() {
            if (Graph.NodeIds.IsCreated) Graph.Dispose();
            if (Keyframes.Keyframes.IsCreated) Keyframes.Dispose();
            if (Scalars.IsCreated) Scalars.Dispose();
            if (Vectors.IsCreated) Vectors.Dispose();
            if (Durations.IsCreated) Durations.Dispose();
            if (Facing.IsCreated) Facing.Dispose();
            if (Steering.IsCreated) Steering.Dispose();
            if (Driven.IsCreated) Driven.Dispose();
            if (Priority.IsCreated) Priority.Dispose();
            if (Render.IsCreated) Render.Dispose();
        }
    }
}
