using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Sim.Schema {
    [BurstCompile]
    public struct KeyframeStore : IDisposable {
        public NativeList<Keyframe> Keyframes;
        public NativeHashMap<ulong, int2> Ranges;

        public static KeyframeStore Create(Allocator allocator, int keyframeCapacity = 256, int curveCapacity = 64) {
            return new KeyframeStore {
                Keyframes = new NativeList<Keyframe>(keyframeCapacity, allocator),
                Ranges = new NativeHashMap<ulong, int2>(curveCapacity, allocator)
            };
        }

        [BurstCompile]
        public static ulong MakeKey(uint nodeId, PropertyId propertyId) {
            return ((ulong)nodeId << 8) | (byte)propertyId;
        }

        [BurstCompile]
        public static void UnpackKey(ulong key, out uint nodeId, out PropertyId propertyId) {
            nodeId = (uint)(key >> 8);
            propertyId = (PropertyId)(byte)(key & 0xFF);
        }

        public void Set(uint nodeId, PropertyId propertyId, in NativeArray<Keyframe> keyframes) {
            ulong key = MakeKey(nodeId, propertyId);
            Ranges.Remove(key);

            if (keyframes.Length == 0) return;

            int start = Keyframes.Length;
            for (int i = 0; i < keyframes.Length; i++) {
                Keyframes.Add(keyframes[i]);
            }
            Ranges[key] = new int2(start, keyframes.Length);
        }

        public bool TryGet(uint nodeId, PropertyId propertyId, out NativeSlice<Keyframe> keyframes) {
            ulong key = MakeKey(nodeId, propertyId);
            if (Ranges.TryGetValue(key, out int2 range)) {
                keyframes = new NativeSlice<Keyframe>(Keyframes.AsArray(), range.x, range.y);
                return true;
            }
            keyframes = default;
            return false;
        }

        public void Remove(uint nodeId, PropertyId propertyId) {
            ulong key = MakeKey(nodeId, propertyId);
            Ranges.Remove(key);
        }

        public void RemoveNode(uint nodeId) {
            var toRemove = new NativeList<ulong>(Allocator.Temp);
            foreach (var kv in Ranges) {
                UnpackKey(kv.Key, out uint id, out _);
                if (id == nodeId) toRemove.Add(kv.Key);
            }
            for (int i = 0; i < toRemove.Length; i++) {
                Ranges.Remove(toRemove[i]);
            }
            toRemove.Dispose();
        }

        public void Clear() {
            Keyframes.Clear();
            Ranges.Clear();
        }

        public void Dispose() {
            if (Keyframes.IsCreated) Keyframes.Dispose();
            if (Ranges.IsCreated) Ranges.Dispose();
        }
    }
}
