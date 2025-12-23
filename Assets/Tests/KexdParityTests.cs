using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using KexEdit.Nodes;
using KexEdit.Persistence;
using KexGraph;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using CoasterAggregate = KexEdit.Coaster.Coaster;
using LegacyNodeType = KexEdit.Legacy.NodeType;
using CoreNodeType = KexEdit.Nodes.NodeType;
using CoreDuration = KexEdit.Coaster.Duration;
using CoreDurationType = KexEdit.Coaster.DurationType;

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
            var coaster = CoasterAggregate.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)CoreNodeType.Anchor, float2.zero);

            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Vector, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Vector, 1).ToEncoded());
            uint velocityPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            uint heartPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 1).ToEncoded());
            uint frictionPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 2).ToEncoded());
            uint resistancePort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 3).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());

            float3 position = new float3(10, 20, 30);
            float3 rotation = new float3(0.1f, 0.2f, 0.3f);
            float velocity = 15f;
            float heart = 1.5f;
            float friction = 0.025f;
            float resistance = 3e-5f;

            coaster.Vectors[nodeId] = position;
            coaster.Rotations[nodeId] = rotation;
            coaster.Scalars[velocityPort] = velocity;
            coaster.Scalars[heartPort] = heart;
            coaster.Scalars[frictionPort] = friction;
            coaster.Scalars[resistancePort] = resistance;

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

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

            Assert.IsTrue(loadedCoaster.Vectors.TryGetValue(nodeId, out var loadedPosition),
                "Position should be preserved");
            Assert.AreEqual(position.x, loadedPosition.x, 0.001f, "Position.x mismatch");
            Assert.AreEqual(position.y, loadedPosition.y, 0.001f, "Position.y mismatch");
            Assert.AreEqual(position.z, loadedPosition.z, 0.001f, "Position.z mismatch");

            var loadedRotation = loadedCoaster.GetRotation(nodeId);
            Assert.AreEqual(rotation.x, loadedRotation.x, 0.001f, "Rotation.x mismatch");
            Assert.AreEqual(rotation.y, loadedRotation.y, 0.001f, "Rotation.y mismatch");
            Assert.AreEqual(rotation.z, loadedRotation.z, 0.001f, "Rotation.z mismatch");

            var portQuery = _entityManager.CreateEntityQuery(typeof(Port));
            using var ports = portQuery.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(7, ports.Length, "Expected 7 ports (6 input + 1 output)");

            bool foundVelocity = false, foundHeart = false, foundFriction = false, foundResistance = false;
            foreach (var portEntity in ports) {
                var port = _entityManager.GetComponentData<Port>(portEntity);
                if (!port.IsInput) continue;

                if (_entityManager.HasComponent<VelocityPort>(portEntity)) {
                    float v = _entityManager.GetComponentData<VelocityPort>(portEntity).Value;
                    Assert.AreEqual(velocity, v, 0.001f, "Velocity mismatch");
                    foundVelocity = true;
                }
                if (_entityManager.HasComponent<HeartPort>(portEntity)) {
                    float h = _entityManager.GetComponentData<HeartPort>(portEntity).Value;
                    Assert.AreEqual(heart, h, 0.001f, "Heart mismatch");
                    foundHeart = true;
                }
                if (_entityManager.HasComponent<FrictionPort>(portEntity)) {
                    float f = _entityManager.GetComponentData<FrictionPort>(portEntity).Value;
                    Assert.AreEqual(friction, f, 0.001f, "Friction mismatch");
                    foundFriction = true;
                }
                if (_entityManager.HasComponent<ResistancePort>(portEntity)) {
                    float r = _entityManager.GetComponentData<ResistancePort>(portEntity).Value;
                    Assert.AreEqual(resistance, r, 0.001f, "Resistance mismatch");
                    foundResistance = true;
                }
            }

            Assert.IsTrue(foundVelocity, "VelocityPort not found");
            Assert.IsTrue(foundHeart, "HeartPort not found");
            Assert.IsTrue(foundFriction, "FrictionPort not found");
            Assert.IsTrue(foundResistance, "ResistancePort not found");

            _entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void ForceSection_Duration_RoundTrip() {
            var coaster = CoasterAggregate.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)CoreNodeType.Force, float2.zero);

            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Path, 0).ToEncoded());

            float durationValue = 3.5f;
            coaster.Durations[nodeId] = new CoreDuration(durationValue, CoreDurationType.Time);

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

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

            Assert.IsTrue(loadedCoaster.Durations.TryGetValue(nodeId, out var loadedDuration),
                "Duration should be preserved");
            Assert.AreEqual(durationValue, loadedDuration.Value, 0.001f, "Duration value mismatch");

            var portQuery = _entityManager.CreateEntityQuery(typeof(Port), typeof(DurationPort));
            using var ports = portQuery.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, ports.Length, "Should have exactly one DurationPort");

            var durationPortValue = _entityManager.GetComponentData<DurationPort>(ports[0]).Value;
            Assert.AreEqual(durationValue, durationPortValue, 0.001f, "DurationPort component value mismatch");

            _entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void CurvedSection_AllScalars_RoundTrip() {
            var coaster = CoasterAggregate.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)CoreNodeType.Curved, float2.zero);

            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            uint radiusPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            uint arcPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 1).ToEncoded());
            uint axisPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 2).ToEncoded());
            uint leadInPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 3).ToEncoded());
            uint leadOutPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 4).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Path, 0).ToEncoded());

            float radius = 25f, arc = 90f, axis = 45f, leadIn = 5f, leadOut = 10f;
            coaster.Scalars[radiusPort] = radius;
            coaster.Scalars[arcPort] = arc;
            coaster.Scalars[axisPort] = axis;
            coaster.Scalars[leadInPort] = leadIn;
            coaster.Scalars[leadOutPort] = leadOut;

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

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

            var portQuery = _entityManager.CreateEntityQuery(typeof(Port));
            using var ports = portQuery.ToEntityArray(Allocator.Temp);

            bool foundRadius = false, foundArc = false, foundAxis = false;
            bool foundLeadIn = false, foundLeadOut = false;

            foreach (var portEntity in ports) {
                if (_entityManager.HasComponent<RadiusPort>(portEntity)) {
                    float v = _entityManager.GetComponentData<RadiusPort>(portEntity).Value;
                    Assert.AreEqual(radius, v, 0.001f, "Radius mismatch");
                    foundRadius = true;
                }
                if (_entityManager.HasComponent<ArcPort>(portEntity)) {
                    float v = _entityManager.GetComponentData<ArcPort>(portEntity).Value;
                    Assert.AreEqual(arc, v, 0.001f, "Arc mismatch");
                    foundArc = true;
                }
                if (_entityManager.HasComponent<AxisPort>(portEntity)) {
                    float v = _entityManager.GetComponentData<AxisPort>(portEntity).Value;
                    Assert.AreEqual(axis, v, 0.001f, "Axis mismatch");
                    foundAxis = true;
                }
                if (_entityManager.HasComponent<LeadInPort>(portEntity)) {
                    float v = _entityManager.GetComponentData<LeadInPort>(portEntity).Value;
                    Assert.AreEqual(leadIn, v, 0.001f, "LeadIn mismatch");
                    foundLeadIn = true;
                }
                if (_entityManager.HasComponent<LeadOutPort>(portEntity)) {
                    float v = _entityManager.GetComponentData<LeadOutPort>(portEntity).Value;
                    Assert.AreEqual(leadOut, v, 0.001f, "LeadOut mismatch");
                    foundLeadOut = true;
                }
            }

            Assert.IsTrue(foundRadius, "RadiusPort not found");
            Assert.IsTrue(foundArc, "ArcPort not found");
            Assert.IsTrue(foundAxis, "AxisPort not found");
            Assert.IsTrue(foundLeadIn, "LeadInPort not found");
            Assert.IsTrue(foundLeadOut, "LeadOutPort not found");

            _entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void Bridge_WeightPorts_RoundTrip() {
            var coaster = CoasterAggregate.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)CoreNodeType.Bridge, float2.zero);

            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Anchor, 1).ToEncoded());
            uint outWeightPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 1).ToEncoded());
            uint inWeightPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Path, 0).ToEncoded());

            float outWeight = 0.4f, inWeight = 0.6f;
            coaster.Scalars[outWeightPort] = outWeight;
            coaster.Scalars[inWeightPort] = inWeight;

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

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

            var portQuery = _entityManager.CreateEntityQuery(typeof(Port));
            using var ports = portQuery.ToEntityArray(Allocator.Temp);

            bool foundOutWeight = false, foundInWeight = false;

            foreach (var portEntity in ports) {
                if (_entityManager.HasComponent<OutWeightPort>(portEntity)) {
                    float v = _entityManager.GetComponentData<OutWeightPort>(portEntity).Value;
                    Assert.AreEqual(outWeight, v, 0.001f, "OutWeight mismatch");
                    foundOutWeight = true;
                }
                if (_entityManager.HasComponent<InWeightPort>(portEntity)) {
                    float v = _entityManager.GetComponentData<InWeightPort>(portEntity).Value;
                    Assert.AreEqual(inWeight, v, 0.001f, "InWeight mismatch");
                    foundInWeight = true;
                }
            }

            Assert.IsTrue(foundOutWeight, "OutWeightPort not found");
            Assert.IsTrue(foundInWeight, "InWeightPort not found");

            _entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void CopyPath_TrimPorts_RoundTrip() {
            var coaster = CoasterAggregate.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)CoreNodeType.CopyPath, float2.zero);

            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Path, 0).ToEncoded());
            uint startPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            uint endPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 1).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Path, 0).ToEncoded());

            float start = 0.25f, end = 0.75f;
            coaster.Scalars[startPort] = start;
            coaster.Scalars[endPort] = end;

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

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

            var portQuery = _entityManager.CreateEntityQuery(typeof(Port));
            using var ports = portQuery.ToEntityArray(Allocator.Temp);

            bool foundStart = false, foundEnd = false;

            foreach (var portEntity in ports) {
                if (_entityManager.HasComponent<StartPort>(portEntity)) {
                    float v = _entityManager.GetComponentData<StartPort>(portEntity).Value;
                    Assert.AreEqual(start, v, 0.001f, "Start mismatch");
                    foundStart = true;
                }
                if (_entityManager.HasComponent<EndPort>(portEntity)) {
                    float v = _entityManager.GetComponentData<EndPort>(portEntity).Value;
                    Assert.AreEqual(end, v, 0.001f, "End mismatch");
                    foundEnd = true;
                }
            }

            Assert.IsTrue(foundStart, "StartPort not found");
            Assert.IsTrue(foundEnd, "EndPort not found");

            _entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void Steering_Flag_RoundTrip() {
            var coaster = CoasterAggregate.Create(Allocator.Persistent);
            var nodeId = coaster.Graph.AddNode((uint)CoreNodeType.Geometric, float2.zero);

            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Path, 0).ToEncoded());

            coaster.Steering.Add(nodeId);
            coaster.Durations[nodeId] = new CoreDuration(1f, CoreDurationType.Time);

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

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

            Assert.IsTrue(loadedCoaster.Steering.Contains(nodeId), "Steering flag should be preserved");

            var nodeQuery = _entityManager.CreateEntityQuery(typeof(Node), typeof(Steering));
            using var nodes = nodeQuery.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, nodes.Length, "Should have exactly one node with Steering component");
            Assert.IsTrue(_entityManager.GetComponentData<Steering>(nodes[0]).Value, "Steering should be true");

            _entityManager.DestroyEntity(loadedEntity);
        }

        [Test]
        public void Connections_RoundTrip() {
            var coaster = CoasterAggregate.Create(Allocator.Persistent);

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

            coaster.Durations[forceId] = new CoreDuration(2f, CoreDurationType.Time);
            coaster.Durations[geoId] = new CoreDuration(3f, CoreDurationType.Time);

            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

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
