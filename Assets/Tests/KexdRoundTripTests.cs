using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using KexEdit.Persistence;
using KexGraph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Coaster = KexEdit.Coaster.Coaster;
using LegacyNodeType = KexEdit.Legacy.NodeType;

namespace Tests {
    [TestFixture]
    public class KexdRoundTripTests {
        private World _world;
        private SerializationSystem _serializationSystem;

        [SetUp]
        public void SetUp() {
            _world = new World("Test World");
            _serializationSystem = _world.CreateSystemManaged<SerializationSystem>();
        }

        [TearDown]
        public void TearDown() {
            if (_world != null && _world.IsCreated) {
                _world.Dispose();
            }
        }

        [Test]
        public void SerializeToKEXD_ProducesValidFormat() {
            var entityManager = _world.EntityManager;
            var coasterEntity = entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData));
            entityManager.SetName(coasterEntity, "Coaster");

            var coaster = Coaster.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)LegacyNodeType.Anchor, new float2(100, 200));

            entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

            var nodeEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(nodeEntity, new Node {
                Id = nodeId,
                Type = LegacyNodeType.Anchor,
                Position = new float2(100, 200),
                Selected = false
            });
            entityManager.AddComponentData(nodeEntity, new CoasterReference { Value = coasterEntity });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            Assert.IsNotNull(kexdData);
            Assert.Greater(kexdData.Length, 8);
            Assert.AreEqual((byte)'K', kexdData[0]);
            Assert.AreEqual((byte)'E', kexdData[1]);
            Assert.AreEqual((byte)'X', kexdData[2]);
            Assert.AreEqual((byte)'D', kexdData[3]);

            var nativeData = new NativeArray<byte>(kexdData, Allocator.Temp);
            var reader = new ChunkReader(nativeData);
            reader.ReadByte();
            reader.ReadByte();
            reader.ReadByte();
            reader.ReadByte();
            uint version = reader.ReadUInt();
            Assert.GreaterOrEqual(version, 1);

            var hasCore = false;
            var hasUIMetadata = false;

            while (reader.HasData) {
                if (!reader.TryReadHeader(out var header)) break;

                if (header.TypeString == "CORE") {
                    hasCore = true;
                    reader.SkipChunk(header);
                } else if (header.TypeString == ExtensionSchema.UIMetadataType) {
                    hasUIMetadata = true;
                    reader.SkipChunk(header);
                } else {
                    reader.SkipChunk(header);
                }
            }

            Assert.IsTrue(hasCore, "KEXD file must contain CORE chunk");
            Assert.IsTrue(hasUIMetadata, "KEXD file must contain UIMD chunk");

            reader.Dispose();
            nativeData.Dispose();
            entityManager.DestroyEntity(coasterEntity);
            entityManager.DestroyEntity(nodeEntity);
        }

        [Test]
        public void SerializeToKEXD_PreservesNodePositions() {
            var entityManager = _world.EntityManager;
            var coasterEntity = entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData));
            entityManager.SetName(coasterEntity, "Coaster");

            var coaster = Coaster.Create(Allocator.Persistent);
            var node1Id = coaster.Graph.AddNode((uint)LegacyNodeType.Anchor, new float2(100, 200));
            var node2Id = coaster.Graph.AddNode((uint)LegacyNodeType.ForceSection, new float2(300, 400));

            entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

            var node1Entity = entityManager.CreateEntity();
            entityManager.AddComponentData(node1Entity, new Node {
                Id = node1Id,
                Type = LegacyNodeType.Anchor,
                Position = new float2(100, 200),
                Selected = false
            });
            entityManager.AddComponentData(node1Entity, new CoasterReference { Value = coasterEntity });

            var node2Entity = entityManager.CreateEntity();
            entityManager.AddComponentData(node2Entity, new Node {
                Id = node2Id,
                Type = LegacyNodeType.ForceSection,
                Position = new float2(300, 400),
                Selected = false
            });
            entityManager.AddComponentData(node2Entity, new CoasterReference { Value = coasterEntity });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            var nativeData = new NativeArray<byte>(kexdData, Allocator.Temp);
            var reader = new ChunkReader(nativeData);
            var extensions = ExtensionSerializer.ReadExtensions(ref reader, Allocator.Temp);

            Assert.IsTrue(extensions.HasUIMetadata, "KEXD file must contain UI metadata");
            Assert.AreEqual(2, extensions.UIMetadata.Positions.Count);
            Assert.IsTrue(extensions.UIMetadata.Positions.TryGetValue(node1Id, out var pos1));
            Assert.IsTrue(extensions.UIMetadata.Positions.TryGetValue(node2Id, out var pos2));

            Assert.AreEqual(100, pos1.x, 0.001f);
            Assert.AreEqual(200, pos1.y, 0.001f);
            Assert.AreEqual(300, pos2.x, 0.001f);
            Assert.AreEqual(400, pos2.y, 0.001f);

            reader.Dispose();
            nativeData.Dispose();
            extensions.Dispose();
            entityManager.DestroyEntity(coasterEntity);
            entityManager.DestroyEntity(node1Entity);
            entityManager.DestroyEntity(node2Entity);
        }

        [Test]
        public void SerializeToKEXD_PreservesCoasterData() {
            var entityManager = _world.EntityManager;
            var coasterEntity = entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData));
            entityManager.SetName(coasterEntity, "Coaster");

            var coaster = Coaster.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)LegacyNodeType.Anchor, new float2(100, 200));
            coaster.Scalars[nodeId] = 42.5f;
            coaster.Vectors[nodeId] = new float3(1, 2, 3);

            entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

            var nodeEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(nodeEntity, new Node {
                Id = nodeId,
                Type = LegacyNodeType.Anchor,
                Position = new float2(100, 200),
                Selected = false
            });
            entityManager.AddComponentData(nodeEntity, new CoasterReference { Value = coasterEntity });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            var nativeData = new NativeArray<byte>(kexdData, Allocator.Temp);
            var reader = new ChunkReader(nativeData);
            var loadedCoaster = CoasterSerializer.Read(reader, Allocator.Temp);

            Assert.AreEqual(1, loadedCoaster.Graph.NodeIds.Length);
            Assert.AreEqual(nodeId, loadedCoaster.Graph.NodeIds[0]);
            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(nodeId, out float scalarValue));
            Assert.AreEqual(42.5f, scalarValue, 0.001f);
            Assert.IsTrue(loadedCoaster.Vectors.TryGetValue(nodeId, out float3 vectorValue));
            Assert.AreEqual(1, vectorValue.x, 0.001f);
            Assert.AreEqual(2, vectorValue.y, 0.001f);
            Assert.AreEqual(3, vectorValue.z, 0.001f);

            reader.Dispose();
            nativeData.Dispose();
            loadedCoaster.Dispose();
            entityManager.DestroyEntity(coasterEntity);
            entityManager.DestroyEntity(nodeEntity);
        }
    }
}
