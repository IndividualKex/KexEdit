using KexEdit.Legacy;
using KexEdit.Sim.Schema;
using KexEdit.Persistence;
using KexEdit.UI.Timeline;
using NUnit.Framework;
using Unity.Collections;

namespace Tests {
    [TestFixture]
    public class KeyframeUIStateTests {
        [Test]
        public void UIStateChunk_CreateAndDispose_NoLeaks() {
            var chunk = UIStateChunk.Create(Allocator.Temp);
            Assert.IsTrue(chunk.KeyframeStates.IsCreated);
            Assert.AreEqual(0, chunk.KeyframeStates.Length);
            chunk.Dispose();
        }

        [Test]
        public void UIStateChunk_SetAndGetKeyframe_RoundTrips() {
            var chunk = UIStateChunk.Create(Allocator.Temp);

            var state = new KeyframeUIState {
                NodeId = 42,
                PropertyId = 3,
                KeyframeIndex = 1,
                Id = 12345,
                HandleType = 1,
                Flags = 2
            };
            chunk.SetKeyframeState(state);

            Assert.IsTrue(chunk.TryGetKeyframeState(42, 3, 1, out var retrieved));
            Assert.AreEqual(42u, retrieved.NodeId);
            Assert.AreEqual(3, retrieved.PropertyId);
            Assert.AreEqual(1, retrieved.KeyframeIndex);
            Assert.AreEqual(12345u, retrieved.Id);
            Assert.AreEqual(1, retrieved.HandleType);
            Assert.AreEqual(2, retrieved.Flags);

            chunk.Dispose();
        }

        [Test]
        public void UIStateChunk_SetExistingKeyframe_Updates() {
            var chunk = UIStateChunk.Create(Allocator.Temp);

            chunk.SetKeyframeState(new KeyframeUIState { NodeId = 1, PropertyId = 0, KeyframeIndex = 0, Id = 100 });
            Assert.AreEqual(1, chunk.KeyframeStates.Length);

            chunk.SetKeyframeState(new KeyframeUIState { NodeId = 1, PropertyId = 0, KeyframeIndex = 0, Id = 200 });
            Assert.AreEqual(1, chunk.KeyframeStates.Length);

            Assert.IsTrue(chunk.TryGetKeyframeState(1, 0, 0, out var state));
            Assert.AreEqual(200u, state.Id);

            chunk.Dispose();
        }

        [Test]
        public void UIStateChunk_RemoveKeyframe_DeletesEntry() {
            var chunk = UIStateChunk.Create(Allocator.Temp);

            chunk.SetKeyframeState(new KeyframeUIState { NodeId = 1, PropertyId = 0, KeyframeIndex = 0, Id = 100 });
            chunk.SetKeyframeState(new KeyframeUIState { NodeId = 1, PropertyId = 0, KeyframeIndex = 1, Id = 101 });
            Assert.AreEqual(2, chunk.KeyframeStates.Length);

            chunk.RemoveKeyframeState(1, 0, 0);
            Assert.AreEqual(1, chunk.KeyframeStates.Length);
            Assert.IsFalse(chunk.TryGetKeyframeState(1, 0, 0, out _));
            Assert.IsTrue(chunk.TryGetKeyframeState(1, 0, 1, out _));

            chunk.Dispose();
        }

        [Test]
        public void UIStateChunk_RemoveNode_DeletesAllEntriesForNode() {
            var chunk = UIStateChunk.Create(Allocator.Temp);

            chunk.SetKeyframeState(new KeyframeUIState { NodeId = 1, PropertyId = 0, KeyframeIndex = 0, Id = 100 });
            chunk.SetKeyframeState(new KeyframeUIState { NodeId = 1, PropertyId = 1, KeyframeIndex = 0, Id = 101 });
            chunk.SetKeyframeState(new KeyframeUIState { NodeId = 2, PropertyId = 0, KeyframeIndex = 0, Id = 200 });
            Assert.AreEqual(3, chunk.KeyframeStates.Length);

            chunk.RemoveNodeKeyframeStates(1);
            Assert.AreEqual(1, chunk.KeyframeStates.Length);
            Assert.IsFalse(chunk.TryGetKeyframeState(1, 0, 0, out _));
            Assert.IsFalse(chunk.TryGetKeyframeState(1, 1, 0, out _));
            Assert.IsTrue(chunk.TryGetKeyframeState(2, 0, 0, out _));

            chunk.Dispose();
        }

        [Test]
        public void UIStateChunk_RemoveProperty_DeletesAllEntriesForProperty() {
            var chunk = UIStateChunk.Create(Allocator.Temp);

            chunk.SetKeyframeState(new KeyframeUIState { NodeId = 1, PropertyId = 0, KeyframeIndex = 0, Id = 100 });
            chunk.SetKeyframeState(new KeyframeUIState { NodeId = 1, PropertyId = 0, KeyframeIndex = 1, Id = 101 });
            chunk.SetKeyframeState(new KeyframeUIState { NodeId = 1, PropertyId = 1, KeyframeIndex = 0, Id = 102 });
            Assert.AreEqual(3, chunk.KeyframeStates.Length);

            chunk.RemovePropertyKeyframeStates(1, 0);
            Assert.AreEqual(1, chunk.KeyframeStates.Length);
            Assert.IsFalse(chunk.TryGetKeyframeState(1, 0, 0, out _));
            Assert.IsFalse(chunk.TryGetKeyframeState(1, 0, 1, out _));
            Assert.IsTrue(chunk.TryGetKeyframeState(1, 1, 0, out _));

            chunk.Dispose();
        }
    }

    [TestFixture]
    public class PropertyMappingTests {
        [Test]
        public void ToPropertyId_AllPropertyTypes_Map() {
            Assert.AreEqual(PropertyId.RollSpeed, PropertyMapping.ToPropertyId(PropertyType.RollSpeed));
            Assert.AreEqual(PropertyId.NormalForce, PropertyMapping.ToPropertyId(PropertyType.NormalForce));
            Assert.AreEqual(PropertyId.LateralForce, PropertyMapping.ToPropertyId(PropertyType.LateralForce));
            Assert.AreEqual(PropertyId.PitchSpeed, PropertyMapping.ToPropertyId(PropertyType.PitchSpeed));
            Assert.AreEqual(PropertyId.YawSpeed, PropertyMapping.ToPropertyId(PropertyType.YawSpeed));
            Assert.AreEqual(PropertyId.DrivenVelocity, PropertyMapping.ToPropertyId(PropertyType.FixedVelocity));
            Assert.AreEqual(PropertyId.HeartOffset, PropertyMapping.ToPropertyId(PropertyType.Heart));
            Assert.AreEqual(PropertyId.Friction, PropertyMapping.ToPropertyId(PropertyType.Friction));
            Assert.AreEqual(PropertyId.Resistance, PropertyMapping.ToPropertyId(PropertyType.Resistance));
            Assert.AreEqual(PropertyId.TrackStyle, PropertyMapping.ToPropertyId(PropertyType.TrackStyle));
        }

        [Test]
        public void ToPropertyType_AllPropertyIds_Map() {
            Assert.AreEqual(PropertyType.RollSpeed, PropertyMapping.ToPropertyType(PropertyId.RollSpeed));
            Assert.AreEqual(PropertyType.NormalForce, PropertyMapping.ToPropertyType(PropertyId.NormalForce));
            Assert.AreEqual(PropertyType.LateralForce, PropertyMapping.ToPropertyType(PropertyId.LateralForce));
            Assert.AreEqual(PropertyType.PitchSpeed, PropertyMapping.ToPropertyType(PropertyId.PitchSpeed));
            Assert.AreEqual(PropertyType.YawSpeed, PropertyMapping.ToPropertyType(PropertyId.YawSpeed));
            Assert.AreEqual(PropertyType.FixedVelocity, PropertyMapping.ToPropertyType(PropertyId.DrivenVelocity));
            Assert.AreEqual(PropertyType.Heart, PropertyMapping.ToPropertyType(PropertyId.HeartOffset));
            Assert.AreEqual(PropertyType.Friction, PropertyMapping.ToPropertyType(PropertyId.Friction));
            Assert.AreEqual(PropertyType.Resistance, PropertyMapping.ToPropertyType(PropertyId.Resistance));
            Assert.AreEqual(PropertyType.TrackStyle, PropertyMapping.ToPropertyType(PropertyId.TrackStyle));
        }

        [Test]
        public void Bidirectional_RoundTrips() {
            var propertyTypes = new[] {
                PropertyType.RollSpeed, PropertyType.NormalForce, PropertyType.LateralForce,
                PropertyType.PitchSpeed, PropertyType.YawSpeed, PropertyType.FixedVelocity,
                PropertyType.Heart, PropertyType.Friction, PropertyType.Resistance, PropertyType.TrackStyle
            };

            foreach (var type in propertyTypes) {
                var id = PropertyMapping.ToPropertyId(type);
                var backToType = PropertyMapping.ToPropertyType(id);
                Assert.AreEqual(type, backToType, $"Round-trip failed for {type}");
            }
        }
    }

    [TestFixture]
    public class KeyframeConversionTests {
        [Test]
        public void ToCore_PreservesAllFields() {
            var legacy = new KexEdit.Legacy.Keyframe {
                Id = 123,
                Time = 1.5f,
                Value = 2.5f,
                InInterpolation = KexEdit.Legacy.InterpolationType.Bezier,
                OutInterpolation = KexEdit.Legacy.InterpolationType.Linear,
                HandleType = HandleType.Aligned,
                Flags = KeyframeFlags.LockTime,
                InTangent = 0.5f,
                OutTangent = -0.5f,
                InWeight = 0.3f,
                OutWeight = 0.4f,
                Selected = true
            };

            var core = KeyframeConversion.ToCore(legacy);

            Assert.AreEqual(1.5f, core.Time, 1e-6f);
            Assert.AreEqual(2.5f, core.Value, 1e-6f);
            Assert.AreEqual(KexEdit.Sim.InterpolationType.Bezier, core.InInterpolation);
            Assert.AreEqual(KexEdit.Sim.InterpolationType.Linear, core.OutInterpolation);
            Assert.AreEqual(0.5f, core.InTangent, 1e-6f);
            Assert.AreEqual(-0.5f, core.OutTangent, 1e-6f);
            Assert.AreEqual(0.3f, core.InWeight, 1e-6f);
            Assert.AreEqual(0.4f, core.OutWeight, 1e-6f);
        }

        [Test]
        public void ToLegacy_PreservesAllFields() {
            var core = new KexEdit.Sim.Keyframe(
                time: 1.5f,
                value: 2.5f,
                inInterpolation: KexEdit.Sim.InterpolationType.Bezier,
                outInterpolation: KexEdit.Sim.InterpolationType.Linear,
                inTangent: 0.5f,
                outTangent: -0.5f,
                inWeight: 0.3f,
                outWeight: 0.4f
            );

            var legacy = KeyframeConversion.ToLegacy(core, id: 456, HandleType.Free, KeyframeFlags.LockValue, selected: true);

            Assert.AreEqual(456u, legacy.Id);
            Assert.AreEqual(1.5f, legacy.Time, 1e-6f);
            Assert.AreEqual(2.5f, legacy.Value, 1e-6f);
            Assert.AreEqual(KexEdit.Legacy.InterpolationType.Bezier, legacy.InInterpolation);
            Assert.AreEqual(KexEdit.Legacy.InterpolationType.Linear, legacy.OutInterpolation);
            Assert.AreEqual(HandleType.Free, legacy.HandleType);
            Assert.AreEqual(KeyframeFlags.LockValue, legacy.Flags);
            Assert.AreEqual(0.5f, legacy.InTangent, 1e-6f);
            Assert.AreEqual(-0.5f, legacy.OutTangent, 1e-6f);
            Assert.AreEqual(0.3f, legacy.InWeight, 1e-6f);
            Assert.AreEqual(0.4f, legacy.OutWeight, 1e-6f);
            Assert.IsTrue(legacy.Selected);
        }

        [Test]
        public void RoundTrip_PreservesCoreFields() {
            var original = new KexEdit.Sim.Keyframe(
                time: 3.0f,
                value: -1.5f,
                inInterpolation: KexEdit.Sim.InterpolationType.Constant,
                outInterpolation: KexEdit.Sim.InterpolationType.Bezier,
                inTangent: 1.0f,
                outTangent: 2.0f,
                inWeight: 0.2f,
                outWeight: 0.8f
            );

            var legacy = KeyframeConversion.ToLegacy(original, id: 789, HandleType.Aligned, KeyframeFlags.None);
            var backToCore = KeyframeConversion.ToCore(legacy);

            Assert.AreEqual(original.Time, backToCore.Time, 1e-6f);
            Assert.AreEqual(original.Value, backToCore.Value, 1e-6f);
            Assert.AreEqual(original.InInterpolation, backToCore.InInterpolation);
            Assert.AreEqual(original.OutInterpolation, backToCore.OutInterpolation);
            Assert.AreEqual(original.InTangent, backToCore.InTangent, 1e-6f);
            Assert.AreEqual(original.OutTangent, backToCore.OutTangent, 1e-6f);
            Assert.AreEqual(original.InWeight, backToCore.InWeight, 1e-6f);
            Assert.AreEqual(original.OutWeight, backToCore.OutWeight, 1e-6f);
        }

        [Test]
        public void InterpolationType_AllTypesConvert() {
            var legacyTypes = new[] {
                KexEdit.Legacy.InterpolationType.Constant,
                KexEdit.Legacy.InterpolationType.Linear,
                KexEdit.Legacy.InterpolationType.Bezier
            };

            foreach (var legacyType in legacyTypes) {
                var legacy = new KexEdit.Legacy.Keyframe {
                    InInterpolation = legacyType,
                    OutInterpolation = legacyType
                };
                var core = KeyframeConversion.ToCore(legacy);
                var backToLegacy = KeyframeConversion.ToLegacy(core, 0, HandleType.Aligned, KeyframeFlags.None);

                Assert.AreEqual(legacyType, backToLegacy.InInterpolation);
                Assert.AreEqual(legacyType, backToLegacy.OutInterpolation);
            }
        }
    }

    [TestFixture]
    public class CoasterKeyframeManagerTests {
        private KexEdit.Document.Document _coaster;
        private UIStateChunk _uiState;
        private CoasterKeyframeManager _manager;

        [SetUp]
        public void SetUp() {
            _coaster = KexEdit.Document.Document.Create(Allocator.Persistent);
            _uiState = UIStateChunk.Create(Allocator.Persistent);
            _manager = new CoasterKeyframeManager(_coaster, _uiState);
        }

        [TearDown]
        public void TearDown() {
            _coaster.Dispose();
            _uiState.Dispose();
        }

        [Test]
        public void GetKeyframes_EmptyStore_ReturnsEmpty() {
            var output = new System.Collections.Generic.List<KexEdit.Legacy.Keyframe>();
            _manager.GetKeyframes(1, PropertyType.RollSpeed, output);
            Assert.AreEqual(0, output.Count);
        }

        [Test]
        public void AddKeyframe_AddsToStore() {
            var keyframe = KexEdit.Legacy.Keyframe.Create(1.0f, 2.0f);
            _manager.AddKeyframe(1, PropertyType.RollSpeed, keyframe);

            var output = new System.Collections.Generic.List<KexEdit.Legacy.Keyframe>();
            _manager.GetKeyframes(1, PropertyType.RollSpeed, output);

            Assert.AreEqual(1, output.Count);
            Assert.AreEqual(1.0f, output[0].Time, 1e-6f);
            Assert.AreEqual(2.0f, output[0].Value, 1e-6f);
        }

        [Test]
        public void AddKeyframe_MaintainsSortOrder() {
            _manager.AddKeyframe(1, PropertyType.RollSpeed, KexEdit.Legacy.Keyframe.Create(3.0f, 30f));
            _manager.AddKeyframe(1, PropertyType.RollSpeed, KexEdit.Legacy.Keyframe.Create(1.0f, 10f));
            _manager.AddKeyframe(1, PropertyType.RollSpeed, KexEdit.Legacy.Keyframe.Create(2.0f, 20f));

            var output = new System.Collections.Generic.List<KexEdit.Legacy.Keyframe>();
            _manager.GetKeyframes(1, PropertyType.RollSpeed, output);

            Assert.AreEqual(3, output.Count);
            Assert.AreEqual(1.0f, output[0].Time, 1e-6f);
            Assert.AreEqual(2.0f, output[1].Time, 1e-6f);
            Assert.AreEqual(3.0f, output[2].Time, 1e-6f);
        }

        [Test]
        public void AddKeyframe_AssignsUniqueIds() {
            _manager.AddKeyframe(1, PropertyType.RollSpeed, KexEdit.Legacy.Keyframe.Create(1.0f, 10f));
            _manager.AddKeyframe(1, PropertyType.RollSpeed, KexEdit.Legacy.Keyframe.Create(2.0f, 20f));

            var output = new System.Collections.Generic.List<KexEdit.Legacy.Keyframe>();
            _manager.GetKeyframes(1, PropertyType.RollSpeed, output);

            Assert.AreNotEqual(0u, output[0].Id);
            Assert.AreNotEqual(0u, output[1].Id);
            Assert.AreNotEqual(output[0].Id, output[1].Id);
        }

        [Test]
        public void UpdateKeyframe_ModifiesValue() {
            _manager.AddKeyframe(1, PropertyType.RollSpeed, KexEdit.Legacy.Keyframe.Create(1.0f, 10f));

            var output = new System.Collections.Generic.List<KexEdit.Legacy.Keyframe>();
            _manager.GetKeyframes(1, PropertyType.RollSpeed, output);
            var kf = output[0];

            kf = kf.WithValue(99f);
            _manager.UpdateKeyframe(1, PropertyType.RollSpeed, kf);

            _manager.GetKeyframes(1, PropertyType.RollSpeed, output);
            Assert.AreEqual(1, output.Count);
            Assert.AreEqual(99f, output[0].Value, 1e-6f);
        }

        [Test]
        public void RemoveKeyframe_RemovesFromStore() {
            _manager.AddKeyframe(1, PropertyType.RollSpeed, KexEdit.Legacy.Keyframe.Create(1.0f, 10f));
            _manager.AddKeyframe(1, PropertyType.RollSpeed, KexEdit.Legacy.Keyframe.Create(2.0f, 20f));

            var output = new System.Collections.Generic.List<KexEdit.Legacy.Keyframe>();
            _manager.GetKeyframes(1, PropertyType.RollSpeed, output);
            var idToRemove = output[0].Id;

            _manager.RemoveKeyframe(1, PropertyType.RollSpeed, idToRemove);

            _manager.GetKeyframes(1, PropertyType.RollSpeed, output);
            Assert.AreEqual(1, output.Count);
            Assert.AreEqual(2.0f, output[0].Time, 1e-6f);
        }

        [Test]
        public void EvaluateAt_ReturnsInterpolatedValue() {
            var kf1 = KexEdit.Legacy.Keyframe.Create(0.0f, 0f);
            kf1 = new KexEdit.Legacy.Keyframe {
                Id = kf1.Id,
                Time = 0.0f,
                Value = 0f,
                InInterpolation = KexEdit.Legacy.InterpolationType.Linear,
                OutInterpolation = KexEdit.Legacy.InterpolationType.Linear,
                HandleType = HandleType.Aligned,
                Flags = KeyframeFlags.None,
                InTangent = 0f,
                OutTangent = 0f,
                InWeight = 1f / 3f,
                OutWeight = 1f / 3f
            };

            var kf2 = new KexEdit.Legacy.Keyframe {
                Id = 0,
                Time = 1.0f,
                Value = 10f,
                InInterpolation = KexEdit.Legacy.InterpolationType.Linear,
                OutInterpolation = KexEdit.Legacy.InterpolationType.Linear,
                HandleType = HandleType.Aligned,
                Flags = KeyframeFlags.None,
                InTangent = 0f,
                OutTangent = 0f,
                InWeight = 1f / 3f,
                OutWeight = 1f / 3f
            };

            _manager.AddKeyframe(1, PropertyType.RollSpeed, kf1);
            _manager.AddKeyframe(1, PropertyType.RollSpeed, kf2);

            var value = _manager.EvaluateAt(1, PropertyType.RollSpeed, 0.5f);
            Assert.AreEqual(5.0f, value, 0.01f);
        }

        [Test]
        public void MultipleProperties_IndependentlyManaged() {
            _manager.AddKeyframe(1, PropertyType.RollSpeed, KexEdit.Legacy.Keyframe.Create(1.0f, 10f));
            _manager.AddKeyframe(1, PropertyType.NormalForce, KexEdit.Legacy.Keyframe.Create(2.0f, 20f));

            var rollOutput = new System.Collections.Generic.List<KexEdit.Legacy.Keyframe>();
            var forceOutput = new System.Collections.Generic.List<KexEdit.Legacy.Keyframe>();

            _manager.GetKeyframes(1, PropertyType.RollSpeed, rollOutput);
            _manager.GetKeyframes(1, PropertyType.NormalForce, forceOutput);

            Assert.AreEqual(1, rollOutput.Count);
            Assert.AreEqual(1, forceOutput.Count);
            Assert.AreEqual(10f, rollOutput[0].Value, 1e-6f);
            Assert.AreEqual(20f, forceOutput[0].Value, 1e-6f);
        }
    }
}
