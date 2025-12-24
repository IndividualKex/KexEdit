using Unity.Collections;

namespace KexEdit.Persistence {
    public struct KeyframeUIState {
        public uint NodeId;
        public byte PropertyId;
        public int KeyframeIndex;
        public uint Id;
        public byte HandleType;
        public byte Flags;
    }

    public struct KeyframeUIChunk {
        public NativeList<KeyframeUIState> States;

        public static KeyframeUIChunk Create(Allocator allocator) {
            return new KeyframeUIChunk {
                States = new NativeList<KeyframeUIState>(allocator)
            };
        }

        public void Dispose() {
            if (States.IsCreated) States.Dispose();
        }

        public void Clear() {
            States.Clear();
        }

        public bool TryGet(uint nodeId, byte propertyId, int keyframeIndex, out KeyframeUIState state) {
            for (int i = 0; i < States.Length; i++) {
                var s = States[i];
                if (s.NodeId == nodeId && s.PropertyId == propertyId && s.KeyframeIndex == keyframeIndex) {
                    state = s;
                    return true;
                }
            }
            state = default;
            return false;
        }

        public void Set(in KeyframeUIState state) {
            for (int i = 0; i < States.Length; i++) {
                var s = States[i];
                if (s.NodeId == state.NodeId && s.PropertyId == state.PropertyId && s.KeyframeIndex == state.KeyframeIndex) {
                    States[i] = state;
                    return;
                }
            }
            States.Add(state);
        }

        public void Remove(uint nodeId, byte propertyId, int keyframeIndex) {
            for (int i = States.Length - 1; i >= 0; i--) {
                var s = States[i];
                if (s.NodeId == nodeId && s.PropertyId == propertyId && s.KeyframeIndex == keyframeIndex) {
                    States.RemoveAtSwapBack(i);
                    return;
                }
            }
        }

        public void RemoveNode(uint nodeId) {
            for (int i = States.Length - 1; i >= 0; i--) {
                if (States[i].NodeId == nodeId) {
                    States.RemoveAtSwapBack(i);
                }
            }
        }

        public void RemoveProperty(uint nodeId, byte propertyId) {
            for (int i = States.Length - 1; i >= 0; i--) {
                var s = States[i];
                if (s.NodeId == nodeId && s.PropertyId == propertyId) {
                    States.RemoveAtSwapBack(i);
                }
            }
        }
    }
}
