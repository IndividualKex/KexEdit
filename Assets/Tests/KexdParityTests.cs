using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using KexEdit.Sim.Schema;
using KexEdit.Persistence;
using KexEdit.Graph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DocumentAggregate = KexEdit.Document.Document;
using LegacyNodeType = KexEdit.Legacy.NodeType;
using CoreNodeType = KexEdit.Sim.Schema.NodeType;
using CoreDuration = KexEdit.Legacy.Duration;
using CoreDurationType = KexEdit.Legacy.DurationType;
using NodeMeta = KexEdit.Document.NodeMeta;

namespace Tests {
    [TestFixture]
    public class KexdParityTests {
        private World _world;
        private SerializationSystem _serializationSystem;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp() {
            _world = new World("Parity Test World");
            _serializationSystem = _world.CreateSystemManaged<SerializationSystem>();
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown() {
            if (_world != null && _world.IsCreated) {
                _world.Dispose();
            }
        }

        [Test]
        public void AnchorNode_PortValues_RoundTrip() {
            var coaster = DocumentAggregate.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)CoreNodeType.Anchor, float2.zero);

            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Vector, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 1).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 2).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 3).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 4).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 5).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 6).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());

            float3 position = new float3(10, 20, 30);
            float roll = 0.1f, pitch = 0.2f, yaw = 0.3f;
            float velocity = 15f;
            float heart = 1.5f;
            float friction = 0.025f;
            float resistance = 3e-5f;

            coaster.Vectors[DocumentAggregate.InputKey(nodeId, 0)] = position;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 1)] = roll;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 2)] = pitch;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 3)] = yaw;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 4)] = velocity;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 5)] = heart;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 6)] = friction;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 7)] = resistance;

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
            _entityManager.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

            var nodeEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(nodeEntity, new Node {
                Id = nodeId,
                Type = LegacyNodeType.Anchor,
                Position = float2.zero,
                Selected = false
            });
            _entityManager.AddComponentData(nodeEntity, new CoasterReference { Value = coasterEntity });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            _entityManager.DestroyEntity(coasterEntity);
            _entityManager.DestroyEntity(nodeEntity);

            var loadedEntity = _serializationSystem.DeserializeGraph(kexdData, false);
            var loadedCoaster = _entityManager.GetComponentData<CoasterData>(loadedEntity).Value;

            Assert.IsTrue(loadedCoaster.Vectors.TryGetValue(DocumentAggregate.InputKey(nodeId, 0), out var loadedPosition),
                "Position should be preserved");
            Assert.AreEqual(position.x, loadedPosition.x, 0.001f, "Position.x mismatch");
            Assert.AreEqual(position.y, loadedPosition.y, 0.001f, "Position.y mismatch");
            Assert.AreEqual(position.z, loadedPosition.z, 0.001f, "Position.z mismatch");

            // Verify scalar values are preserved in Coaster
            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 4), out var loadedVelocity),
                "Velocity scalar should be preserved");
            Assert.AreEqual(velocity, loadedVelocity, 0.001f, "Velocity mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 5), out var loadedHeart),
                "Heart scalar should be preserved");
            Assert.AreEqual(heart, loadedHeart, 0.001f, "Heart mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 6), out var loadedFriction),
                "Friction scalar should be preserved");
            Assert.AreEqual(friction, loadedFriction, 0.001f, "Friction mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 7), out var loadedResistance),
                "Resistance scalar should be preserved");
            Assert.AreEqual(resistance, loadedResistance, 0.001f, "Resistance mismatch");

            _entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void ForceSection_Duration_RoundTrip() {
            var coaster = DocumentAggregate.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)CoreNodeType.Force, float2.zero);

            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Path, 0).ToEncoded());

            float durationValue = 3.5f;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, NodeMeta.Duration)] = durationValue;

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
            _entityManager.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

            var nodeEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(nodeEntity, new Node {
                Id = nodeId,
                Type = LegacyNodeType.ForceSection,
                Position = float2.zero,
                Selected = false
            });
            _entityManager.AddComponentData(nodeEntity, new CoasterReference { Value = coasterEntity });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            _entityManager.DestroyEntity(coasterEntity);
            _entityManager.DestroyEntity(nodeEntity);

            var loadedEntity = _serializationSystem.DeserializeGraph(kexdData, false);
            var loadedCoaster = _entityManager.GetComponentData<CoasterData>(loadedEntity).Value;

            ulong durKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Duration);
            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(durKey, out var loadedDuration),
                "Duration should be preserved");
            Assert.AreEqual(durationValue, loadedDuration, 0.001f, "Duration value mismatch");

            _entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void CurvedSection_AllScalars_RoundTrip() {
            var coaster = DocumentAggregate.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)CoreNodeType.Curved, float2.zero);

            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 1).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 2).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 3).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 4).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Path, 0).ToEncoded());

            float radius = 25f, arc = 90f, axis = 45f, leadIn = 5f, leadOut = 10f;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 1)] = radius;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 2)] = arc;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 3)] = axis;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 4)] = leadIn;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 5)] = leadOut;

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
            _entityManager.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

            var nodeEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(nodeEntity, new Node {
                Id = nodeId,
                Type = LegacyNodeType.CurvedSection,
                Position = float2.zero,
                Selected = false
            });
            _entityManager.AddComponentData(nodeEntity, new CoasterReference { Value = coasterEntity });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            _entityManager.DestroyEntity(coasterEntity);
            _entityManager.DestroyEntity(nodeEntity);

            var loadedEntity = _serializationSystem.DeserializeGraph(kexdData, false);
            var loadedCoaster = _entityManager.GetComponentData<CoasterData>(loadedEntity).Value;

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 1), out var loadedRadius), "Radius not found");
            Assert.AreEqual(radius, loadedRadius, 0.001f, "Radius mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 2), out var loadedArc), "Arc not found");
            Assert.AreEqual(arc, loadedArc, 0.001f, "Arc mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 3), out var loadedAxis), "Axis not found");
            Assert.AreEqual(axis, loadedAxis, 0.001f, "Axis mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 4), out var loadedLeadIn), "LeadIn not found");
            Assert.AreEqual(leadIn, loadedLeadIn, 0.001f, "LeadIn mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 5), out var loadedLeadOut), "LeadOut not found");
            Assert.AreEqual(leadOut, loadedLeadOut, 0.001f, "LeadOut mismatch");

            _entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void Bridge_WeightPorts_RoundTrip() {
            var coaster = DocumentAggregate.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)CoreNodeType.Bridge, float2.zero);

            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Anchor, 1).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 1).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Path, 0).ToEncoded());

            float outWeight = 0.4f, inWeight = 0.6f;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 2)] = outWeight;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 3)] = inWeight;

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
            _entityManager.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

            var nodeEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(nodeEntity, new Node {
                Id = nodeId,
                Type = LegacyNodeType.Bridge,
                Position = float2.zero,
                Selected = false
            });
            _entityManager.AddComponentData(nodeEntity, new CoasterReference { Value = coasterEntity });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            _entityManager.DestroyEntity(coasterEntity);
            _entityManager.DestroyEntity(nodeEntity);

            var loadedEntity = _serializationSystem.DeserializeGraph(kexdData, false);
            var loadedCoaster = _entityManager.GetComponentData<CoasterData>(loadedEntity).Value;

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 2), out var loadedOutWeight), "OutWeight not found");
            Assert.AreEqual(outWeight, loadedOutWeight, 0.001f, "OutWeight mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 3), out var loadedInWeight), "InWeight not found");
            Assert.AreEqual(inWeight, loadedInWeight, 0.001f, "InWeight mismatch");

            _entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void CopyPath_TrimPorts_RoundTrip() {
            var coaster = DocumentAggregate.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)CoreNodeType.CopyPath, float2.zero);

            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Path, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 1).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Path, 0).ToEncoded());

            float start = 0.25f, end = 0.75f;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 2)] = start;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, 3)] = end;

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
            _entityManager.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

            var nodeEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(nodeEntity, new Node {
                Id = nodeId,
                Type = LegacyNodeType.CopyPathSection,
                Position = float2.zero,
                Selected = false
            });
            _entityManager.AddComponentData(nodeEntity, new CoasterReference { Value = coasterEntity });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            _entityManager.DestroyEntity(coasterEntity);
            _entityManager.DestroyEntity(nodeEntity);

            var loadedEntity = _serializationSystem.DeserializeGraph(kexdData, false);
            var loadedCoaster = _entityManager.GetComponentData<CoasterData>(loadedEntity).Value;

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 2), out var loadedStart), "Start not found");
            Assert.AreEqual(start, loadedStart, 0.001f, "Start mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(DocumentAggregate.InputKey(nodeId, 3), out var loadedEnd), "End not found");
            Assert.AreEqual(end, loadedEnd, 0.001f, "End mismatch");

            _entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void Steering_Flag_RoundTrip() {
            var coaster = DocumentAggregate.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)CoreNodeType.Geometric, float2.zero);

            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Path, 0).ToEncoded());

            coaster.Flags[DocumentAggregate.InputKey(nodeId, NodeMeta.Steering)] = 1;
            coaster.Scalars[DocumentAggregate.InputKey(nodeId, NodeMeta.Duration)] = 1f;

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
            _entityManager.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

            var nodeEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(nodeEntity, new Node {
                Id = nodeId,
                Type = LegacyNodeType.GeometricSection,
                Position = float2.zero,
                Selected = false
            });
            _entityManager.AddComponentData(nodeEntity, new CoasterReference { Value = coasterEntity });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            _entityManager.DestroyEntity(coasterEntity);
            _entityManager.DestroyEntity(nodeEntity);

            var loadedEntity = _serializationSystem.DeserializeGraph(kexdData, false);
            var loadedCoaster = _entityManager.GetComponentData<CoasterData>(loadedEntity).Value;

            ulong steeringKey = DocumentAggregate.InputKey(nodeId, NodeMeta.Steering);
            Assert.IsTrue(loadedCoaster.Flags.TryGetValue(steeringKey, out int s) && s == 1, "Steering flag should be preserved");

            var nodeQuery = _entityManager.CreateEntityQuery(typeof(Node), typeof(Steering));
            using var nodes = nodeQuery.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, nodes.Length, "Should have exactly one node with Steering component");
            Assert.IsTrue(_entityManager.GetComponentData<Steering>(nodes[0]).Value, "Steering should be true");

            _entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void Connections_RoundTrip() {
            var coaster = DocumentAggregate.Create(Allocator.Persistent);

            // Create Anchor node (6 inputs, 1 output)
            var anchorId = coaster.Graph.AddNode((uint)CoreNodeType.Anchor, float2.zero);
            coaster.Graph.AddInputPort(anchorId, new PortSpec(PortDataType.Vector, 0).ToEncoded());
            coaster.Graph.AddInputPort(anchorId, new PortSpec(PortDataType.Vector, 1).ToEncoded());
            coaster.Graph.AddInputPort(anchorId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            coaster.Graph.AddInputPort(anchorId, new PortSpec(PortDataType.Scalar, 1).ToEncoded());
            coaster.Graph.AddInputPort(anchorId, new PortSpec(PortDataType.Scalar, 2).ToEncoded());
            coaster.Graph.AddInputPort(anchorId, new PortSpec(PortDataType.Scalar, 3).ToEncoded());
            uint anchorOutputPort = coaster.Graph.AddOutputPort(anchorId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());

            // Create Force node (2 inputs, 2 outputs)
            var forceId = coaster.Graph.AddNode((uint)CoreNodeType.Force, new float2(100, 0));
            uint forceInputPort = coaster.Graph.AddInputPort(forceId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddInputPort(forceId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            uint forceOutputPort = coaster.Graph.AddOutputPort(forceId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddOutputPort(forceId, new PortSpec(PortDataType.Path, 0).ToEncoded());

            // Create Geometric node (2 inputs, 2 outputs)
            var geoId = coaster.Graph.AddNode((uint)CoreNodeType.Geometric, new float2(200, 0));
            uint geoInputPort = coaster.Graph.AddInputPort(geoId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddInputPort(geoId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            coaster.Graph.AddOutputPort(geoId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddOutputPort(geoId, new PortSpec(PortDataType.Path, 0).ToEncoded());

            // Connect: Anchor → Force → Geometric
            uint edge1 = coaster.Graph.AddEdge(anchorOutputPort, forceInputPort);
            uint edge2 = coaster.Graph.AddEdge(forceOutputPort, geoInputPort);

            coaster.Scalars[DocumentAggregate.InputKey(forceId, NodeMeta.Duration)] = 2f;
            coaster.Scalars[DocumentAggregate.InputKey(geoId, NodeMeta.Duration)] = 3f;

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData), typeof(UIStateData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });
            _entityManager.SetComponentData(coasterEntity, new UIStateData { Value = UIStateChunk.Create(Allocator.Persistent) });

            var node1 = _entityManager.CreateEntity();
            _entityManager.AddComponentData(node1, new Node { Id = anchorId, Type = LegacyNodeType.Anchor });
            _entityManager.AddComponentData(node1, new CoasterReference { Value = coasterEntity });

            var node2 = _entityManager.CreateEntity();
            _entityManager.AddComponentData(node2, new Node { Id = forceId, Type = LegacyNodeType.ForceSection });
            _entityManager.AddComponentData(node2, new CoasterReference { Value = coasterEntity });

            var node3 = _entityManager.CreateEntity();
            _entityManager.AddComponentData(node3, new Node { Id = geoId, Type = LegacyNodeType.GeometricSection });
            _entityManager.AddComponentData(node3, new CoasterReference { Value = coasterEntity });

            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            _entityManager.DestroyEntity(coasterEntity);
            _entityManager.DestroyEntity(node1);
            _entityManager.DestroyEntity(node2);
            _entityManager.DestroyEntity(node3);

            var loadedEntity = _serializationSystem.DeserializeGraph(kexdData, false);
            var loadedCoaster = _entityManager.GetComponentData<CoasterData>(loadedEntity).Value;

            Assert.AreEqual(2, loadedCoaster.Graph.EdgeIds.Length, "Should have 2 edges in coaster");

            var connectionQuery = _entityManager.CreateEntityQuery(typeof(Connection));
            using var connections = connectionQuery.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(2, connections.Length, "Should have 2 Connection entities");

            var nodeQuery = _entityManager.CreateEntityQuery(typeof(Node));
            using var nodes = nodeQuery.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(3, nodes.Length, "Should have 3 nodes");

            var portQuery = _entityManager.CreateEntityQuery(typeof(Port));
            using var ports = portQuery.ToEntityArray(Allocator.Temp);

            foreach (var connEntity in connections) {
                var conn = _entityManager.GetComponentData<Connection>(connEntity);
                Assert.IsTrue(_entityManager.Exists(conn.Source), $"Connection source entity should exist for edge {conn.Id}");
                Assert.IsTrue(_entityManager.Exists(conn.Target), $"Connection target entity should exist for edge {conn.Id}");

                var sourcePort = _entityManager.GetComponentData<Port>(conn.Source);
                var targetPort = _entityManager.GetComponentData<Port>(conn.Target);

                Assert.IsFalse(sourcePort.IsInput, "Connection source should be an output port");
                Assert.IsTrue(targetPort.IsInput, "Connection target should be an input port");
            }

            _entityManager.DestroyEntity(loadedEntity);
        }
    }
}
