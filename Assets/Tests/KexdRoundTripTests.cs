using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using KexEdit.Persistence;
using KexEdit.Graph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Coaster = KexEdit.Document.Document;
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
            var coasterEntity = entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            entityManager.SetName(coasterEntity, "Coaster");

            var coaster = Coaster.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)LegacyNodeType.Anchor, new float2(100, 200));

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
            var hasUIState = false;

            while (reader.HasData) {
                if (!reader.TryReadHeader(out var header)) break;

                if (header.TypeString == "CORE") {
                    hasCore = true;
                    reader.SkipChunk(header);
                } else if (header.TypeString == ExtensionSchema.UIStateType) {
                    hasUIState = true;
                    reader.SkipChunk(header);
                } else {
                    reader.SkipChunk(header);
                }
            }

            Assert.IsTrue(hasCore, "KEXD file must contain CORE chunk");
            Assert.IsTrue(hasUIState, "KEXD file must contain UIST chunk");

            nativeData.Dispose();
            entityManager.DestroyEntity(coasterEntity);
            entityManager.DestroyEntity(nodeEntity);
        }

        [Test]
        public void SerializeToKEXD_PreservesNodePositions() {
            var entityManager = _world.EntityManager;
            var coasterEntity = entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            entityManager.SetName(coasterEntity, "Coaster");

            var coaster = Coaster.Create(Allocator.Persistent);
            var node1Id = coaster.Graph.AddNode((uint)LegacyNodeType.Anchor, new float2(100, 200));
            var node2Id = coaster.Graph.AddNode((uint)LegacyNodeType.ForceSection, new float2(300, 400));

            entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
            entityManager.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

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
            CoasterSerializer.Read(ref reader, Allocator.Temp).Dispose();
            bool hasUIState = UIExtensionCodec.TryRead(ref reader, Allocator.Temp, out var uiState);

            Assert.IsTrue(hasUIState, "KEXD file must contain UI state");
            Assert.AreEqual(2, uiState.NodePositions.Count);
            Assert.IsTrue(uiState.NodePositions.TryGetValue(node1Id, out var pos1));
            Assert.IsTrue(uiState.NodePositions.TryGetValue(node2Id, out var pos2));

            Assert.AreEqual(100, pos1.x, 0.001f);
            Assert.AreEqual(200, pos1.y, 0.001f);
            Assert.AreEqual(300, pos2.x, 0.001f);
            Assert.AreEqual(400, pos2.y, 0.001f);

            nativeData.Dispose();
            uiState.Dispose();
            entityManager.DestroyEntity(coasterEntity);
            entityManager.DestroyEntity(node1Entity);
            entityManager.DestroyEntity(node2Entity);
        }

        [Test]
        public void SerializeToKEXD_PreservesCoasterData() {
            var entityManager = _world.EntityManager;
            var coasterEntity = entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            entityManager.SetName(coasterEntity, "Coaster");

            var coaster = Coaster.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)LegacyNodeType.Anchor, new float2(100, 200));
            coaster.Scalars[nodeId] = 42.5f;
            coaster.Vectors[nodeId] = new float3(1, 2, 3);

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

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            var nativeData = new NativeArray<byte>(kexdData, Allocator.Temp);
            var reader = new ChunkReader(nativeData);
            var loadedCoaster = CoasterSerializer.Read(ref reader, Allocator.Temp);

            Assert.AreEqual(1, loadedCoaster.Graph.NodeIds.Length);
            Assert.AreEqual(nodeId, loadedCoaster.Graph.NodeIds[0]);
            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(nodeId, out float scalarValue));
            Assert.AreEqual(42.5f, scalarValue, 0.001f);
            Assert.IsTrue(loadedCoaster.Vectors.TryGetValue(nodeId, out float3 vectorValue));
            Assert.AreEqual(1, vectorValue.x, 0.001f);
            Assert.AreEqual(2, vectorValue.y, 0.001f);
            Assert.AreEqual(3, vectorValue.z, 0.001f);

            nativeData.Dispose();
            loadedCoaster.Dispose();
            entityManager.DestroyEntity(coasterEntity);
            entityManager.DestroyEntity(nodeEntity);
        }

        [Test]
        public void FormatDetection_RecognizesKEXDFormat() {
            byte[] kexdData = new byte[] { (byte)'K', (byte)'E', (byte)'X', (byte)'D', 1, 0, 0, 0 };
            var deserializedEntity = _serializationSystem.DeserializeGraph(kexdData, false);

            Assert.IsTrue(_world.EntityManager.Exists(deserializedEntity));
            Assert.IsTrue(_world.EntityManager.HasComponent<CoasterData>(deserializedEntity));

            _world.EntityManager.DestroyEntity(deserializedEntity);
        }

        [Test]
        public void EmptyGraph_RoundTrip_Succeeds() {
            var entityManager = _world.EntityManager;
            var coasterEntity = entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            entityManager.SetName(coasterEntity, "Coaster");

            var coaster = Coaster.Create(Allocator.Persistent);
            entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
            entityManager.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            entityManager.DestroyEntity(coasterEntity);

            var loadedEntity = _serializationSystem.DeserializeGraph(kexdData, false);

            Assert.IsTrue(entityManager.Exists(loadedEntity));
            Assert.IsTrue(entityManager.HasComponent<CoasterData>(loadedEntity));

            var loadedCoaster = entityManager.GetComponentData<CoasterData>(loadedEntity).Value;
            Assert.AreEqual(0, loadedCoaster.Graph.NodeIds.Length);

            entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void SingleAnchor_RoundTrip_PreservesData() {
            var entityManager = _world.EntityManager;
            var coasterEntity = entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            entityManager.SetName(coasterEntity, "Coaster");

            var coaster = Coaster.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)KexEdit.Sim.Schema.NodeType.Anchor, new float2(100, 200));

            coaster.Vectors[nodeId] = new float3(10, 20, 30);
            // Rotation stored as separate scalars (using fake port IDs for this test)
            uint rollPortId = 1000u, pitchPortId = 1001u, yawPortId = 1002u;
            coaster.Scalars[rollPortId] = 0.1f;
            coaster.Scalars[pitchPortId] = 0.2f;
            coaster.Scalars[yawPortId] = 0.3f;

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

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            entityManager.DestroyEntity(coasterEntity);
            entityManager.DestroyEntity(nodeEntity);

            var loadedEntity = _serializationSystem.DeserializeGraph(kexdData, false);

            Assert.IsTrue(entityManager.Exists(loadedEntity));

            var loadedCoaster = entityManager.GetComponentData<CoasterData>(loadedEntity).Value;
            Assert.AreEqual(1, loadedCoaster.Graph.NodeIds.Length);
            Assert.AreEqual(nodeId, loadedCoaster.Graph.NodeIds[0]);

            Assert.IsTrue(loadedCoaster.Vectors.TryGetValue(nodeId, out var position));
            Assert.AreEqual(10, position.x, 0.001f);
            Assert.AreEqual(20, position.y, 0.001f);
            Assert.AreEqual(30, position.z, 0.001f);

            var query = entityManager.CreateEntityQuery(typeof(Node), typeof(CoasterReference));
            using var nodes = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, nodes.Length);

            var loadedNode = entityManager.GetComponentData<Node>(nodes[0]);
            Assert.AreEqual(nodeId, loadedNode.Id);
            Assert.AreEqual(100, loadedNode.Position.x, 0.001f);
            Assert.AreEqual(200, loadedNode.Position.y, 0.001f);

            entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void NodePositions_RoundTrip_FromUIMetadata() {
            var entityManager = _world.EntityManager;
            var coasterEntity = entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            entityManager.SetName(coasterEntity, "Coaster");

            var coaster = Coaster.Create(Allocator.Persistent);
            var node1Id = coaster.Graph.AddNode((uint)KexEdit.Sim.Schema.NodeType.Anchor, new float2(150, 250));
            var node2Id = coaster.Graph.AddNode((uint)KexEdit.Sim.Schema.NodeType.Force, new float2(350, 450));

            entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
            entityManager.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

            var node1Entity = entityManager.CreateEntity();
            entityManager.AddComponentData(node1Entity, new Node {
                Id = node1Id,
                Type = LegacyNodeType.Anchor,
                Position = new float2(150, 250),
                Selected = false
            });
            entityManager.AddComponentData(node1Entity, new CoasterReference { Value = coasterEntity });

            var node2Entity = entityManager.CreateEntity();
            entityManager.AddComponentData(node2Entity, new Node {
                Id = node2Id,
                Type = LegacyNodeType.ForceSection,
                Position = new float2(350, 450),
                Selected = false
            });
            entityManager.AddComponentData(node2Entity, new CoasterReference { Value = coasterEntity });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            entityManager.DestroyEntity(coasterEntity);
            entityManager.DestroyEntity(node1Entity);
            entityManager.DestroyEntity(node2Entity);

            var loadedEntity = _serializationSystem.DeserializeGraph(kexdData, false);

            var query = entityManager.CreateEntityQuery(typeof(Node), typeof(CoasterReference));
            using var nodes = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(2, nodes.Length);

            bool found1 = false, found2 = false;
            foreach (var nodeEntity in nodes) {
                var node = entityManager.GetComponentData<Node>(nodeEntity);
                if (node.Id == node1Id) {
                    found1 = true;
                    Assert.AreEqual(150, node.Position.x, 0.001f);
                    Assert.AreEqual(250, node.Position.y, 0.001f);
                } else if (node.Id == node2Id) {
                    found2 = true;
                    Assert.AreEqual(350, node.Position.x, 0.001f);
                    Assert.AreEqual(450, node.Position.y, 0.001f);
                }
            }

            Assert.IsTrue(found1, "Node 1 not found after deserialization");
            Assert.IsTrue(found2, "Node 2 not found after deserialization");

            entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void KeyframeIds_RoundTrip_AreUnique() {
            var entityManager = _world.EntityManager;
            var coasterEntity = entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            entityManager.SetName(coasterEntity, "Coaster");

            var coaster = Coaster.Create(Allocator.Persistent);
            var forceNodeId = coaster.Graph.AddNode((uint)KexEdit.Sim.Schema.NodeType.Force, new float2(100, 100));

            // Add 2 keyframes for NormalForce property
            var keyframes = new NativeArray<KexEdit.Sim.Keyframe>(2, Allocator.Temp);
            keyframes[0] = new KexEdit.Sim.Keyframe(0f, 1f);
            keyframes[1] = new KexEdit.Sim.Keyframe(1f, 2f);
            coaster.Keyframes.Set(forceNodeId, KexEdit.Sim.Schema.PropertyId.NormalForce, keyframes);
            keyframes.Dispose();

            entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
            entityManager.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

            var nodeEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(nodeEntity, new Node {
                Id = forceNodeId,
                Type = LegacyNodeType.ForceSection,
                Position = new float2(100, 100),
                Selected = false
            });
            entityManager.AddComponentData(nodeEntity, new CoasterReference { Value = coasterEntity });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);
            entityManager.DestroyEntity(coasterEntity);
            entityManager.DestroyEntity(nodeEntity);

            var loadedEntity = _serializationSystem.DeserializeGraph(kexdData, false);
            var loadedCoaster = entityManager.GetComponentData<CoasterData>(loadedEntity).Value;

            uint loadedForceNodeId = 0;
            for (int i = 0; i < loadedCoaster.Graph.NodeIds.Length; i++) {
                if ((KexEdit.Sim.Schema.NodeType)loadedCoaster.Graph.NodeTypes[i] == KexEdit.Sim.Schema.NodeType.Force) {
                    loadedForceNodeId = loadedCoaster.Graph.NodeIds[i];
                    break;
                }
            }
            Assert.AreNotEqual(0u, loadedForceNodeId, "Expected to find Force node");

            bool hasKeyframes = loadedCoaster.Keyframes.TryGet(loadedForceNodeId, KexEdit.Sim.Schema.PropertyId.NormalForce, out var kfs);
            Assert.IsTrue(hasKeyframes, "Expected keyframes for NormalForce property");
            Assert.AreEqual(2, kfs.Length, "Expected 2 keyframes");

            entityManager.DestroyEntity(loadedEntity);
        }
    }
}
