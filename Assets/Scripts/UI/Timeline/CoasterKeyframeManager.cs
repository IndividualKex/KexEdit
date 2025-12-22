using System.Collections.Generic;
using KexEdit.Legacy;
using KexEdit.Sim.Schema;
using KexEdit.Persistence;
using Unity.Collections;
using AppDocument = KexEdit.Document.Document;

namespace KexEdit.UI.Timeline {
    public class CoasterKeyframeManager {
        private AppDocument _coaster;
        private UIStateChunk _uiState;
        private readonly Dictionary<uint, uint> _nextKeyframeIds;

        public CoasterKeyframeManager(AppDocument coaster, UIStateChunk uiState) {
            _coaster = coaster;
            _uiState = uiState;
            _nextKeyframeIds = new Dictionary<uint, uint>();
        }

        public void UpdateCoaster(AppDocument coaster) {
            _coaster = coaster;
        }

        public bool HasKeyframes(uint nodeId, PropertyType type) {
            var propertyId = PropertyMapping.ToPropertyId(type);
            return _coaster.Keyframes.TryGet(nodeId, propertyId, out var keyframes) && keyframes.Length > 0;
        }

        public void GetKeyframes(uint nodeId, PropertyType type, List<Keyframe> output) {
            output.Clear();

            var propertyId = PropertyMapping.ToPropertyId(type);
            if (!_coaster.Keyframes.TryGet(nodeId, propertyId, out var coreKeyframes)) {
                return;
            }

            for (int i = 0; i < coreKeyframes.Length; i++) {
                var coreKf = coreKeyframes[i];

                uint id;
                HandleType handleType;
                KeyframeFlags flags;

                bool selected;
                if (_uiState.TryGetKeyframeState(nodeId, (byte)propertyId, i, out var uiStateEntry)) {
                    id = uiStateEntry.Id;
                    handleType = (HandleType)uiStateEntry.HandleType;
                    flags = (KeyframeFlags)uiStateEntry.Flags;
                    selected = uiStateEntry.Selected != 0;
                } else {
                    id = AllocateKeyframeId(nodeId);
                    handleType = HandleType.Aligned;
                    flags = KeyframeFlags.None;
                    selected = false;

                    _uiState.SetKeyframeState(new KeyframeUIState {
                        NodeId = nodeId,
                        PropertyId = (byte)propertyId,
                        KeyframeIndex = i,
                        Id = id,
                        HandleType = (byte)handleType,
                        Flags = (byte)flags,
                        Selected = 0
                    });
                }

                var legacyKf = KeyframeConversion.ToLegacy(coreKf, id, handleType, flags, selected);
                output.Add(legacyKf);
            }
        }

        public void UpdateKeyframe(uint nodeId, PropertyType type, Keyframe keyframe) {
            var propertyId = PropertyMapping.ToPropertyId(type);

            if (!_coaster.Keyframes.TryGet(nodeId, propertyId, out var coreKeyframes)) {
                return;
            }

            for (int i = 0; i < coreKeyframes.Length; i++) {
                if (_uiState.TryGetKeyframeState(nodeId, (byte)propertyId, i, out var uiStateEntry) && uiStateEntry.Id == keyframe.Id) {
                    var tempArray = new NativeArray<KexEdit.Sim.Keyframe>(coreKeyframes.Length, Allocator.Temp);
                    for (int j = 0; j < coreKeyframes.Length; j++) {
                        tempArray[j] = j == i ? KeyframeConversion.ToCore(keyframe) : coreKeyframes[j];
                    }

                    _coaster.Keyframes.Set(nodeId, propertyId, in tempArray);
                    tempArray.Dispose();

                    _uiState.SetKeyframeState(new KeyframeUIState {
                        NodeId = nodeId,
                        PropertyId = (byte)propertyId,
                        KeyframeIndex = i,
                        Id = keyframe.Id,
                        HandleType = (byte)keyframe.HandleType,
                        Flags = (byte)keyframe.Flags,
                        Selected = (byte)(keyframe.Selected ? 1 : 0)
                    });
                    return;
                }
            }
        }

        public void AddKeyframe(uint nodeId, PropertyType type, Keyframe keyframe) {
            var propertyId = PropertyMapping.ToPropertyId(type);

            if (!_coaster.Keyframes.TryGet(nodeId, propertyId, out var coreKeyframes)) {
                coreKeyframes = new NativeSlice<KexEdit.Sim.Keyframe>();
            }

            var newArray = new NativeArray<KexEdit.Sim.Keyframe>(coreKeyframes.Length + 1, Allocator.Temp);

            int insertIndex = 0;
            while (insertIndex < coreKeyframes.Length && coreKeyframes[insertIndex].Time < keyframe.Time) {
                insertIndex++;
            }

            for (int i = 0; i < insertIndex; i++) {
                newArray[i] = coreKeyframes[i];
            }
            newArray[insertIndex] = KeyframeConversion.ToCore(keyframe);
            for (int i = insertIndex; i < coreKeyframes.Length; i++) {
                newArray[i + 1] = coreKeyframes[i];
            }

            _coaster.Keyframes.Set(nodeId, propertyId, in newArray);
            newArray.Dispose();

            UpdateUIStateIndices(nodeId, propertyId, insertIndex, +1);

            uint id = keyframe.Id != 0 ? keyframe.Id : AllocateKeyframeId(nodeId);
            _uiState.SetKeyframeState(new KeyframeUIState {
                NodeId = nodeId,
                PropertyId = (byte)propertyId,
                KeyframeIndex = insertIndex,
                Id = id,
                HandleType = (byte)keyframe.HandleType,
                Flags = (byte)keyframe.Flags,
                Selected = (byte)(keyframe.Selected ? 1 : 0)
            });
        }

        public void RemoveKeyframe(uint nodeId, PropertyType type, uint id) {
            var propertyId = PropertyMapping.ToPropertyId(type);

            if (!_coaster.Keyframes.TryGet(nodeId, propertyId, out var coreKeyframes)) {
                return;
            }

            for (int i = coreKeyframes.Length - 1; i >= 0; i--) {
                if (_uiState.TryGetKeyframeState(nodeId, (byte)propertyId, i, out var uiStateEntry) && uiStateEntry.Id == id) {
                    var newArray = new NativeArray<KexEdit.Sim.Keyframe>(coreKeyframes.Length - 1, Allocator.Temp);

                    for (int j = 0; j < i; j++) {
                        newArray[j] = coreKeyframes[j];
                    }
                    for (int j = i + 1; j < coreKeyframes.Length; j++) {
                        newArray[j - 1] = coreKeyframes[j];
                    }

                    if (newArray.Length > 0) {
                        _coaster.Keyframes.Set(nodeId, propertyId, in newArray);
                    } else {
                        _coaster.Keyframes.Remove(nodeId, propertyId);
                    }
                    newArray.Dispose();

                    _uiState.RemoveKeyframeState(nodeId, (byte)propertyId, i);
                    UpdateUIStateIndices(nodeId, propertyId, i, -1);
                    return;
                }
            }
        }

        public float EvaluateAt(uint nodeId, PropertyType type, float time) {
            var propertyId = PropertyMapping.ToPropertyId(type);

            if (!_coaster.Keyframes.TryGet(nodeId, propertyId, out var coreKeyframes)) {
                return 0f;
            }

            var keyframesArray = new NativeArray<KexEdit.Sim.Keyframe>(coreKeyframes.Length, Allocator.Temp);
            for (int i = 0; i < coreKeyframes.Length; i++) {
                keyframesArray[i] = coreKeyframes[i];
            }

            float result = KexEdit.Sim.KeyframeEvaluator.Evaluate(in keyframesArray, time, defaultValue: 0f);
            keyframesArray.Dispose();
            return result;
        }

        private void UpdateUIStateIndices(uint nodeId, PropertyId propertyId, int fromIndex, int delta) {
            var statesToUpdate = new List<(int oldIndex, KeyframeUIState state)>();

            for (int i = 0; i < _uiState.KeyframeStates.Length; i++) {
                var state = _uiState.KeyframeStates[i];
                if (state.NodeId == nodeId && state.PropertyId == (byte)propertyId && state.KeyframeIndex >= fromIndex) {
                    statesToUpdate.Add((state.KeyframeIndex, state));
                }
            }

            foreach (var (oldIndex, state) in statesToUpdate) {
                _uiState.RemoveKeyframeState(nodeId, (byte)propertyId, oldIndex);
                _uiState.SetKeyframeState(new KeyframeUIState {
                    NodeId = state.NodeId,
                    PropertyId = state.PropertyId,
                    KeyframeIndex = state.KeyframeIndex + delta,
                    Id = state.Id,
                    HandleType = state.HandleType,
                    Flags = state.Flags,
                    Selected = state.Selected
                });
            }
        }

        private uint AllocateKeyframeId(uint nodeId) {
            if (!_nextKeyframeIds.TryGetValue(nodeId, out var nextId)) {
                nextId = 1;
            }
            _nextKeyframeIds[nodeId] = nextId + 1;
            return (nodeId << 16) | (nextId & 0xFFFF);
        }
    }
}
