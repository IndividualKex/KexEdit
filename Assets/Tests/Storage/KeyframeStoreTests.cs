using KexEdit.Sim;
using KexEdit.Sim.Schema;
using KexEdit.Sim.Schema;
using NUnit.Framework;
using Unity.Collections;

namespace Storage.Tests {
    [TestFixture]
    [Category("Unit")]
    public class KeyframeStoreTests {
        [Test]
        public void Set_NewCurve_CanBeRetrieved() {
            var store = KeyframeStore.Create(Allocator.Temp);
            var keyframes = new NativeArray<Keyframe>(2, Allocator.Temp);
            keyframes[0] = new Keyframe(0f, 0f);
            keyframes[1] = new Keyframe(1f, 10f);

            store.Set(1, PropertyId.RollSpeed, keyframes);
            bool found = store.TryGet(1, PropertyId.RollSpeed, out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(0f, result[0].Time);
            Assert.AreEqual(10f, result[1].Value);

            keyframes.Dispose();
            store.Dispose();
        }

        [Test]
        public void TryGet_NonexistentCurve_ReturnsFalse() {
            var store = KeyframeStore.Create(Allocator.Temp);

            bool found = store.TryGet(1, PropertyId.RollSpeed, out _);

            Assert.IsFalse(found);

            store.Dispose();
        }

        [Test]
        public void Set_ReplacesExistingCurve() {
            var store = KeyframeStore.Create(Allocator.Temp);
            var first = new NativeArray<Keyframe>(1, Allocator.Temp);
            first[0] = new Keyframe(0f, 5f);
            var second = new NativeArray<Keyframe>(1, Allocator.Temp);
            second[0] = new Keyframe(0f, 99f);

            store.Set(1, PropertyId.RollSpeed, first);
            store.Set(1, PropertyId.RollSpeed, second);
            store.TryGet(1, PropertyId.RollSpeed, out var result);

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(99f, result[0].Value);

            first.Dispose();
            second.Dispose();
            store.Dispose();
        }

        [Test]
        public void Set_EmptyArray_RemovesCurve() {
            var store = KeyframeStore.Create(Allocator.Temp);
            var keyframes = new NativeArray<Keyframe>(1, Allocator.Temp);
            keyframes[0] = new Keyframe(0f, 5f);
            var empty = new NativeArray<Keyframe>(0, Allocator.Temp);

            store.Set(1, PropertyId.RollSpeed, keyframes);
            store.Set(1, PropertyId.RollSpeed, empty);
            bool found = store.TryGet(1, PropertyId.RollSpeed, out _);

            Assert.IsFalse(found);

            keyframes.Dispose();
            empty.Dispose();
            store.Dispose();
        }

        [Test]
        public void Remove_ExistingCurve_CannotBeRetrieved() {
            var store = KeyframeStore.Create(Allocator.Temp);
            var keyframes = new NativeArray<Keyframe>(1, Allocator.Temp);
            keyframes[0] = new Keyframe(0f, 5f);

            store.Set(1, PropertyId.RollSpeed, keyframes);
            store.Remove(1, PropertyId.RollSpeed);
            bool found = store.TryGet(1, PropertyId.RollSpeed, out _);

            Assert.IsFalse(found);

            keyframes.Dispose();
            store.Dispose();
        }

        [Test]
        public void RemoveNode_RemovesAllPropertiesForNode() {
            var store = KeyframeStore.Create(Allocator.Temp);
            var keyframes = new NativeArray<Keyframe>(1, Allocator.Temp);
            keyframes[0] = new Keyframe(0f, 5f);

            store.Set(1, PropertyId.RollSpeed, keyframes);
            store.Set(1, PropertyId.NormalForce, keyframes);
            store.Set(2, PropertyId.RollSpeed, keyframes);

            store.RemoveNode(1);

            Assert.IsFalse(store.TryGet(1, PropertyId.RollSpeed, out _));
            Assert.IsFalse(store.TryGet(1, PropertyId.NormalForce, out _));
            Assert.IsTrue(store.TryGet(2, PropertyId.RollSpeed, out _));

            keyframes.Dispose();
            store.Dispose();
        }

        [Test]
        public void MakeKey_UnpackKey_Roundtrips() {
            uint nodeId = 12345;
            PropertyId propertyId = PropertyId.LateralForce;

            ulong key = KeyframeStore.MakeKey(nodeId, propertyId);
            KeyframeStore.UnpackKey(key, out uint resultNodeId, out PropertyId resultPropertyId);

            Assert.AreEqual(nodeId, resultNodeId);
            Assert.AreEqual(propertyId, resultPropertyId);
        }

        [Test]
        public void DifferentNodesAndProperties_StoredSeparately() {
            var store = KeyframeStore.Create(Allocator.Temp);
            var kf1 = new NativeArray<Keyframe>(1, Allocator.Temp);
            kf1[0] = new Keyframe(0f, 1f);
            var kf2 = new NativeArray<Keyframe>(1, Allocator.Temp);
            kf2[0] = new Keyframe(0f, 2f);
            var kf3 = new NativeArray<Keyframe>(1, Allocator.Temp);
            kf3[0] = new Keyframe(0f, 3f);

            store.Set(1, PropertyId.RollSpeed, kf1);
            store.Set(1, PropertyId.NormalForce, kf2);
            store.Set(2, PropertyId.RollSpeed, kf3);

            store.TryGet(1, PropertyId.RollSpeed, out var r1);
            store.TryGet(1, PropertyId.NormalForce, out var r2);
            store.TryGet(2, PropertyId.RollSpeed, out var r3);

            Assert.AreEqual(1f, r1[0].Value);
            Assert.AreEqual(2f, r2[0].Value);
            Assert.AreEqual(3f, r3[0].Value);

            kf1.Dispose();
            kf2.Dispose();
            kf3.Dispose();
            store.Dispose();
        }

        [Test]
        public void Clear_RemovesAllData() {
            var store = KeyframeStore.Create(Allocator.Temp);
            var keyframes = new NativeArray<Keyframe>(1, Allocator.Temp);
            keyframes[0] = new Keyframe(0f, 5f);

            store.Set(1, PropertyId.RollSpeed, keyframes);
            store.Set(2, PropertyId.NormalForce, keyframes);
            store.Clear();

            Assert.IsFalse(store.TryGet(1, PropertyId.RollSpeed, out _));
            Assert.IsFalse(store.TryGet(2, PropertyId.NormalForce, out _));
            Assert.AreEqual(0, store.Keyframes.Length);
            Assert.AreEqual(0, store.Ranges.Count);

            keyframes.Dispose();
            store.Dispose();
        }
    }
}
