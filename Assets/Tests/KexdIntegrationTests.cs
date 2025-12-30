using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using KexEdit.Persistence;
using KexEdit.Graph;
using NUnit.Framework;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Coaster = KexEdit.Document.Document;
using LegacyNodeType = KexEdit.Legacy.NodeType;

namespace Tests {
    [TestFixture]
    public class KexdIntegrationTests {
        private World _world;
        private SerializationSystem _serializationSystem;
        private string _testFilePath;

        [SetUp]
        public void SetUp() {
            _world = new World("Test World");
            _serializationSystem = _world.CreateSystemManaged<SerializationSystem>();
            _testFilePath = Path.Combine(Application.temporaryCachePath, "test_integration.kex");

            var entityManager = _world.EntityManager;
            var singletonEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(singletonEntity, new TimelineState());
            entityManager.AddComponentData(singletonEntity, new NodeGraphState());
            entityManager.AddComponentData(singletonEntity, new CameraState());
        }

        [TearDown]
        public void TearDown() {
            if (_world != null && _world.IsCreated) {
                _world.Dispose();
            }

            if (File.Exists(_testFilePath)) {
                File.Delete(_testFilePath);
            }

            string kexdPath = Path.ChangeExtension(_testFilePath, ".kexd");
            if (File.Exists(kexdPath)) {
                File.Delete(kexdPath);
            }
        }

        [Test]
        public void ParallelKEXD_SaveFlow_ProducesValidFile() {
            var entityManager = _world.EntityManager;
            var coasterEntity = entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            entityManager.SetName(coasterEntity, "Coaster");

            var coaster = Coaster.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)LegacyNodeType.Anchor, new float2(100, 200));
            coaster.Scalars[nodeId] = 42.5f;

            entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
            entityManager.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

            var nodeEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(nodeEntity, new Node {
                Id = nodeId,
                Type = LegacyNodeType.Anchor,
                Position = new float2(100, 200),
                Selected = false
            });
            entityManager.AddComponentData(nodeEntity, new CoasterReference { Value = coasterEntity });
            entityManager.AddComponentData(nodeEntity, new Anchor());
            entityManager.AddBuffer<InputPortReference>(nodeEntity);
            entityManager.AddBuffer<OutputPortReference>(nodeEntity);

            var legacyData = _serializationSystem.SerializeGraph(coasterEntity);
            File.WriteAllBytes(_testFilePath, legacyData);

            var coasterQuery = entityManager.CreateEntityQuery(typeof(KexEdit.Legacy.Coaster));
            using var entities = coasterQuery.ToEntityArray(Allocator.Temp);

            Assert.Greater(entities.Length, 0, "Should have at least one coaster entity");

            var foundCoaster = entities[0];
            var kexdData = _serializationSystem.SerializeToKEXD(foundCoaster);

            string kexdPath = Path.ChangeExtension(_testFilePath, ".kexd");
            File.WriteAllBytes(kexdPath, kexdData);

            Assert.IsTrue(File.Exists(_testFilePath), "Legacy .kex file should exist");
            Assert.IsTrue(File.Exists(kexdPath), "KEXD .kexd file should exist");

            var kexdFileData = File.ReadAllBytes(kexdPath);
            Assert.AreEqual((byte)'K', kexdFileData[0]);
            Assert.AreEqual((byte)'E', kexdFileData[1]);
            Assert.AreEqual((byte)'X', kexdFileData[2]);
            Assert.AreEqual((byte)'D', kexdFileData[3]);

            var nativeData = new NativeArray<byte>(kexdFileData, Allocator.Temp);
            var reader = new ChunkReader(nativeData);
            var loadedCoaster = CoasterSerializer.Read(ref reader, Allocator.Temp);

            Assert.AreEqual(1, loadedCoaster.Graph.NodeIds.Length);
            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(nodeId, out float value));
            Assert.AreEqual(42.5f, value, 0.001f);

            nativeData.Dispose();
            loadedCoaster.Dispose();
            entityManager.DestroyEntity(coasterEntity);
            entityManager.DestroyEntity(nodeEntity);
        }

        [Test]
        public void ParallelKEXD_ValidatesSerializationSystemInstance() {
            Assert.IsNotNull(SerializationSystem.Instance,
                "SerializationSystem.Instance should be accessible (mimics FileManager debug code)");
            Assert.AreEqual(_serializationSystem, SerializationSystem.Instance,
                "SerializationSystem.Instance should point to the test system");
        }
    }
}
