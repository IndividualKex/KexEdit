using System;
using KexEdit.Core;
using KexEdit.Nodes;
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
        public NativeHashMap<uint, float> Scalars;
        public NativeHashMap<uint, float3> Vectors;
        public NativeHashMap<uint, float3> Rotations;
        public NativeHashMap<uint, Duration> Durations;
        public NativeHashSet<uint> Steering;
        public NativeHashMap<uint, Point> Anchors;

        public static Coaster Create(Allocator allocator) {
            return new Coaster {
                Graph = Graph.Create(allocator),
                Keyframes = KeyframeStore.Create(allocator),
                Scalars = new NativeHashMap<uint, float>(16, allocator),
                Vectors = new NativeHashMap<uint, float3>(16, allocator),
                Rotations = new NativeHashMap<uint, float3>(16, allocator),
                Durations = new NativeHashMap<uint, Duration>(16, allocator),
                Steering = new NativeHashSet<uint>(8, allocator),
                Anchors = new NativeHashMap<uint, Point>(8, allocator),
            };
        }

        public void SetRotation(uint nodeId, float3 rotation) {
            Rotations[nodeId] = rotation;
        }

        public float3 GetRotation(uint nodeId) {
            return Rotations.TryGetValue(nodeId, out var rotation) ? rotation : float3.zero;
        }

        public void Dispose() {
            if (Graph.NodeIds.IsCreated) Graph.Dispose();
            if (Keyframes.Keyframes.IsCreated) Keyframes.Dispose();
            if (Scalars.IsCreated) Scalars.Dispose();
            if (Vectors.IsCreated) Vectors.Dispose();
            if (Rotations.IsCreated) Rotations.Dispose();
            if (Durations.IsCreated) Durations.Dispose();
            if (Steering.IsCreated) Steering.Dispose();
            if (Anchors.IsCreated) Anchors.Dispose();
        }
    }
}
