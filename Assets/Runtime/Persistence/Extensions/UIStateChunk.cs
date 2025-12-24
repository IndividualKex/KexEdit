using System;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Persistence {
    public struct KeyframeUIState {
        public uint NodeId;
        public byte PropertyId;
        public int KeyframeIndex;
        public uint Id;
        public byte HandleType;
        public byte Flags;
    }

    public struct UIStateChunk : IDisposable {
        public NativeHashMap<uint, float2> NodePositions;
        public NativeList<KeyframeUIState> KeyframeStates;

        public float TimelineOffset;
        public float TimelineZoom;
        public float GraphPanX;
        public float GraphPanY;
        public float GraphZoom;
        public float3 CameraPosition;
        public float3 CameraTargetPosition;
        public float CameraDistance;
        public float CameraTargetDistance;
        public float CameraPitch;
        public float CameraTargetPitch;
        public float CameraYaw;
        public float CameraTargetYaw;
        public float CameraSpeedMultiplier;

        public static UIStateChunk Create(Allocator allocator) {
            return new UIStateChunk {
                NodePositions = new NativeHashMap<uint, float2>(64, allocator),
                KeyframeStates = new NativeList<KeyframeUIState>(allocator),
                TimelineOffset = 0f,
                TimelineZoom = 1f,
                GraphPanX = 0f,
                GraphPanY = 0f,
                GraphZoom = 1f,
                CameraPosition = float3.zero,
                CameraTargetPosition = float3.zero,
                CameraDistance = 50f,
                CameraTargetDistance = 50f,
                CameraPitch = 30f,
                CameraTargetPitch = 30f,
                CameraYaw = 0f,
                CameraTargetYaw = 0f,
                CameraSpeedMultiplier = 1f
            };
        }

        public void Dispose() {
            if (NodePositions.IsCreated) NodePositions.Dispose();
            if (KeyframeStates.IsCreated) KeyframeStates.Dispose();
        }

        public void Clear() {
            NodePositions.Clear();
            KeyframeStates.Clear();
        }

        public bool TryGetKeyframeState(uint nodeId, byte propertyId, int keyframeIndex, out KeyframeUIState state) {
            for (int i = 0; i < KeyframeStates.Length; i++) {
                var s = KeyframeStates[i];
                if (s.NodeId == nodeId && s.PropertyId == propertyId && s.KeyframeIndex == keyframeIndex) {
                    state = s;
                    return true;
                }
            }
            state = default;
            return false;
        }

        public void SetKeyframeState(in KeyframeUIState state) {
            for (int i = 0; i < KeyframeStates.Length; i++) {
                var s = KeyframeStates[i];
                if (s.NodeId == state.NodeId && s.PropertyId == state.PropertyId && s.KeyframeIndex == state.KeyframeIndex) {
                    KeyframeStates[i] = state;
                    return;
                }
            }
            KeyframeStates.Add(state);
        }

        public void RemoveKeyframeState(uint nodeId, byte propertyId, int keyframeIndex) {
            for (int i = KeyframeStates.Length - 1; i >= 0; i--) {
                var s = KeyframeStates[i];
                if (s.NodeId == nodeId && s.PropertyId == propertyId && s.KeyframeIndex == keyframeIndex) {
                    KeyframeStates.RemoveAtSwapBack(i);
                    return;
                }
            }
        }

        public void RemoveNodeKeyframeStates(uint nodeId) {
            for (int i = KeyframeStates.Length - 1; i >= 0; i--) {
                if (KeyframeStates[i].NodeId == nodeId) {
                    KeyframeStates.RemoveAtSwapBack(i);
                }
            }
        }

        public void RemovePropertyKeyframeStates(uint nodeId, byte propertyId) {
            for (int i = KeyframeStates.Length - 1; i >= 0; i--) {
                var s = KeyframeStates[i];
                if (s.NodeId == nodeId && s.PropertyId == propertyId) {
                    KeyframeStates.RemoveAtSwapBack(i);
                }
            }
        }
    }
}
