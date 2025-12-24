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

            // New schema: Position (vector), Roll, Pitch, Yaw, Velocity, Heart, Friction, Resistance (all scalars)
            coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Vector, 0).ToEncoded());  // Position
            uint rollPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 0).ToEncoded());
            uint pitchPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 1).ToEncoded());
            uint yawPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 2).ToEncoded());
            uint velocityPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 3).ToEncoded());
            uint heartPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 4).ToEncoded());
            uint frictionPort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 5).ToEncoded());
            uint resistancePort = coaster.Graph.AddInputPort(nodeId, new PortSpec(PortDataType.Scalar, 6).ToEncoded());
            coaster.Graph.AddOutputPort(nodeId, new PortSpec(PortDataType.Anchor, 0).ToEncoded());

            float3 position = new float3(10, 20, 30);
            float roll = 0.1f, pitch = 0.2f, yaw = 0.3f;
            float velocity = 15f;
            float heart = 1.5f;
            float friction = 0.025f;
            float resistance = 3e-5f;

            coaster.Vectors[nodeId] = position;
            coaster.Scalars[rollPort] = roll;
            coaster.Scalars[pitchPort] = pitch;
            coaster.Scalars[yawPort] = yaw;
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

            // Verify scalar values are preserved in Coaster
            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(velocityPort, out var loadedVelocity),
                "Velocity scalar should be preserved");
            Assert.AreEqual(velocity, loadedVelocity, 0.001f, "Velocity mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(heartPort, out var loadedHeart),
                "Heart scalar should be preserved");
            Assert.AreEqual(heart, loadedHeart, 0.001f, "Heart mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(frictionPort, out var loadedFriction),
                "Friction scalar should be preserved");
            Assert.AreEqual(friction, loadedFriction, 0.001f, "Friction mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(resistancePort, out var loadedResistance),
                "Resistance scalar should be preserved");
            Assert.AreEqual(resistance, loadedResistance, 0.001f, "Resistance mismatch");

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
            var loadedCoaster = _entityManager.GetComponentData<CoasterData>(loadedEntity).Value;

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(radiusPort, out var loadedRadius), "Radius not found");
            Assert.AreEqual(radius, loadedRadius, 0.001f, "Radius mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(arcPort, out var loadedArc), "Arc not found");
            Assert.AreEqual(arc, loadedArc, 0.001f, "Arc mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(axisPort, out var loadedAxis), "Axis not found");
            Assert.AreEqual(axis, loadedAxis, 0.001f, "Axis mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(leadInPort, out var loadedLeadIn), "LeadIn not found");
            Assert.AreEqual(leadIn, loadedLeadIn, 0.001f, "LeadIn mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(leadOutPort, out var loadedLeadOut), "LeadOut not found");
            Assert.AreEqual(leadOut, loadedLeadOut, 0.001f, "LeadOut mismatch");

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
            var loadedCoaster = _entityManager.GetComponentData<CoasterData>(loadedEntity).Value;

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(outWeightPort, out var loadedOutWeight), "OutWeight not found");
            Assert.AreEqual(outWeight, loadedOutWeight, 0.001f, "OutWeight mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(inWeightPort, out var loadedInWeight), "InWeight not found");
            Assert.AreEqual(inWeight, loadedInWeight, 0.001f, "InWeight mismatch");

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
            var loadedCoaster = _entityManager.GetComponentData<CoasterData>(loadedEntity).Value;

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(startPort, out var loadedStart), "Start not found");
            Assert.AreEqual(start, loadedStart, 0.001f, "Start mismatch");

            Assert.IsTrue(loadedCoaster.Scalars.TryGetValue(endPort, out var loadedEnd), "End not found");
            Assert.AreEqual(end, loadedEnd, 0.001f, "End mismatch");

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

        [Test]
        public void LegacyBridge_RollPitchYaw_StoredAsSeparateScalars() {
            // Simulate legacy Bridge node with separate Roll/Pitch/Yaw ports
            // Verify they are stored as separate scalars, not consolidated
            var serializedGraph = new SerializedGraph();
            serializedGraph.UIState = new SerializedUIState();

            float roll = 15f, pitch = 30f, yaw = 45f;
            float2 bridgePos = new float2(100, 200);

            var inputPorts = new NativeArray<SerializedPort>(8, Allocator.Temp);
            inputPorts[0] = new SerializedPort { Port = new Port { Id = 1, Type = PortType.Position, IsInput = true }, Value = new PointData { Roll = 0, Velocity = 3, Energy = 0 } };
            inputPorts[1] = new SerializedPort { Port = new Port { Id = 2, Type = PortType.Roll, IsInput = true }, Value = new PointData { Roll = roll } };
            inputPorts[2] = new SerializedPort { Port = new Port { Id = 3, Type = PortType.Pitch, IsInput = true }, Value = new PointData { Roll = pitch } };
            inputPorts[3] = new SerializedPort { Port = new Port { Id = 4, Type = PortType.Yaw, IsInput = true }, Value = new PointData { Roll = yaw } };
            inputPorts[4] = new SerializedPort { Port = new Port { Id = 5, Type = PortType.Velocity, IsInput = true }, Value = new PointData { Roll = 10 } };
            inputPorts[5] = new SerializedPort { Port = new Port { Id = 6, Type = PortType.Heart, IsInput = true }, Value = new PointData { Roll = 1.1f } };
            inputPorts[6] = new SerializedPort { Port = new Port { Id = 7, Type = PortType.Friction, IsInput = true }, Value = new PointData { Roll = 0.021f } };
            inputPorts[7] = new SerializedPort { Port = new Port { Id = 8, Type = PortType.Resistance, IsInput = true }, Value = new PointData { Roll = 2e-5f } };

            var outputPorts = new NativeArray<SerializedPort>(1, Allocator.Temp);
            outputPorts[0] = new SerializedPort { Port = new Port { Id = 9, Type = PortType.Anchor, IsInput = false } };

            var nodes = new NativeArray<SerializedNode>(1, Allocator.Temp);
            nodes[0] = new SerializedNode {
                Node = new Node { Id = 1, Type = LegacyNodeType.Bridge, Position = bridgePos },
                InputPorts = inputPorts,
                OutputPorts = outputPorts,
                Anchor = PointData.Create(10f),
                RollSpeedKeyframes = new NativeArray<RollSpeedKeyframe>(0, Allocator.Temp),
                NormalForceKeyframes = new NativeArray<NormalForceKeyframe>(0, Allocator.Temp),
                LateralForceKeyframes = new NativeArray<LateralForceKeyframe>(0, Allocator.Temp),
                PitchSpeedKeyframes = new NativeArray<PitchSpeedKeyframe>(0, Allocator.Temp),
                YawSpeedKeyframes = new NativeArray<YawSpeedKeyframe>(0, Allocator.Temp),
                FixedVelocityKeyframes = new NativeArray<FixedVelocityKeyframe>(0, Allocator.Temp),
                HeartKeyframes = new NativeArray<HeartKeyframe>(0, Allocator.Temp),
                FrictionKeyframes = new NativeArray<FrictionKeyframe>(0, Allocator.Temp),
                ResistanceKeyframes = new NativeArray<ResistanceKeyframe>(0, Allocator.Temp),
                TrackStyleKeyframes = new NativeArray<TrackStyleKeyframe>(0, Allocator.Temp),
            };

            serializedGraph.Nodes = nodes;
            serializedGraph.Edges = new NativeArray<SerializedEdge>(0, Allocator.Temp);

            LegacyImporter.Import(in serializedGraph, Allocator.Persistent, out var coaster);

            // Verify Roll/Pitch/Yaw are stored as separate scalars (in radians)
            float rollRad = math.radians(roll);
            float pitchRad = math.radians(pitch);
            float yawRad = math.radians(yaw);

            Assert.IsTrue(coaster.Scalars.TryGetValue(2, out var loadedRoll), "Roll port scalar should be stored");
            Assert.IsTrue(coaster.Scalars.TryGetValue(3, out var loadedPitch), "Pitch port scalar should be stored");
            Assert.IsTrue(coaster.Scalars.TryGetValue(4, out var loadedYaw), "Yaw port scalar should be stored");
            Assert.AreEqual(rollRad, loadedRoll, 0.001f, "Roll value mismatch");
            Assert.AreEqual(pitchRad, loadedPitch, 0.001f, "Pitch value mismatch");
            Assert.AreEqual(yawRad, loadedYaw, 0.001f, "Yaw value mismatch");

            coaster.Dispose();
            serializedGraph.Dispose();
        }

        [Test]
        public void SyntheticAnchor_UIPosition_PreservedOnRoundTrip() {
            // Create a Bridge node with embedded anchor data but no target connection
            // This should create a synthetic Anchor node with a calculated position
            var serializedGraph = new SerializedGraph();
            serializedGraph.UIState = new SerializedUIState();

            float2 bridgePos = new float2(100, 200);
            float2 expectedSyntheticPos = bridgePos + new float2(-100f, 50f);

            var inputPorts = new NativeArray<SerializedPort>(8, Allocator.Temp);
            inputPorts[0] = new SerializedPort { Port = new Port { Id = 1, Type = PortType.Position, IsInput = true }, Value = new PointData { Roll = 5, Velocity = 10, Energy = 15 } };
            inputPorts[1] = new SerializedPort { Port = new Port { Id = 2, Type = PortType.Roll, IsInput = true }, Value = new PointData { Roll = 0 } };
            inputPorts[2] = new SerializedPort { Port = new Port { Id = 3, Type = PortType.Pitch, IsInput = true }, Value = new PointData { Roll = 0 } };
            inputPorts[3] = new SerializedPort { Port = new Port { Id = 4, Type = PortType.Yaw, IsInput = true }, Value = new PointData { Roll = 0 } };
            inputPorts[4] = new SerializedPort { Port = new Port { Id = 5, Type = PortType.Velocity, IsInput = true }, Value = new PointData { Roll = 10 } };
            inputPorts[5] = new SerializedPort { Port = new Port { Id = 6, Type = PortType.Heart, IsInput = true }, Value = new PointData { Roll = 1.1f } };
            inputPorts[6] = new SerializedPort { Port = new Port { Id = 7, Type = PortType.Friction, IsInput = true }, Value = new PointData { Roll = 0.021f } };
            inputPorts[7] = new SerializedPort { Port = new Port { Id = 8, Type = PortType.Resistance, IsInput = true }, Value = new PointData { Roll = 2e-5f } };

            var outputPorts = new NativeArray<SerializedPort>(1, Allocator.Temp);
            outputPorts[0] = new SerializedPort { Port = new Port { Id = 9, Type = PortType.Anchor, IsInput = false } };

            var nodes = new NativeArray<SerializedNode>(1, Allocator.Temp);
            nodes[0] = new SerializedNode {
                Node = new Node { Id = 1, Type = LegacyNodeType.Bridge, Position = bridgePos },
                InputPorts = inputPorts,
                OutputPorts = outputPorts,
                Anchor = PointData.Create(new float3(5, 10, 15), 10f),
                RollSpeedKeyframes = new NativeArray<RollSpeedKeyframe>(0, Allocator.Temp),
                NormalForceKeyframes = new NativeArray<NormalForceKeyframe>(0, Allocator.Temp),
                LateralForceKeyframes = new NativeArray<LateralForceKeyframe>(0, Allocator.Temp),
                PitchSpeedKeyframes = new NativeArray<PitchSpeedKeyframe>(0, Allocator.Temp),
                YawSpeedKeyframes = new NativeArray<YawSpeedKeyframe>(0, Allocator.Temp),
                FixedVelocityKeyframes = new NativeArray<FixedVelocityKeyframe>(0, Allocator.Temp),
                HeartKeyframes = new NativeArray<HeartKeyframe>(0, Allocator.Temp),
                FrictionKeyframes = new NativeArray<FrictionKeyframe>(0, Allocator.Temp),
                ResistanceKeyframes = new NativeArray<ResistanceKeyframe>(0, Allocator.Temp),
                TrackStyleKeyframes = new NativeArray<TrackStyleKeyframe>(0, Allocator.Temp),
            };

            serializedGraph.Nodes = nodes;
            serializedGraph.Edges = new NativeArray<SerializedEdge>(0, Allocator.Temp);

            LegacyImporter.Import(in serializedGraph, Allocator.Persistent, out var coaster);

            // Should have created a synthetic Anchor node (Bridge + synthetic Anchor = 2 nodes)
            Assert.AreEqual(2, coaster.Graph.NodeIds.Length, "Should have 2 nodes (Bridge + synthetic Anchor)");

            // Find the synthetic Anchor node (not the Bridge)
            int syntheticIndex = -1;
            for (int i = 0; i < coaster.Graph.NodeIds.Length; i++) {
                if ((CoreNodeType)coaster.Graph.NodeTypes[i] == CoreNodeType.Anchor) {
                    syntheticIndex = i;
                    break;
                }
            }
            Assert.AreNotEqual(-1, syntheticIndex, "Should have a synthetic Anchor node");

            // Verify the synthetic node has the expected position in Graph.NodePositions
            float2 syntheticPos = coaster.Graph.NodePositions[syntheticIndex];
            Assert.AreEqual(expectedSyntheticPos.x, syntheticPos.x, 0.1f, "Synthetic Anchor X position mismatch");
            Assert.AreEqual(expectedSyntheticPos.y, syntheticPos.y, 0.1f, "Synthetic Anchor Y position mismatch");

            // Create ECS entity for the Bridge node only (simulating legacy load)
            var coasterEntity = _entityManager.CreateEntity(typeof(KexEdit.Legacy.Coaster), typeof(CoasterData));
            _entityManager.SetComponentData(coasterEntity, new CoasterData { Value = coaster });

            var bridgeEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(bridgeEntity, new Node { Id = 1, Type = LegacyNodeType.Bridge, Position = bridgePos });
            _entityManager.AddComponentData(bridgeEntity, new CoasterReference { Value = coasterEntity });

            // Serialize to KEXD (should include positions from Graph.NodePositions)
            var kexdData = _serializationSystem.SerializeToKEXD(coasterEntity);

            _entityManager.DestroyEntity(coasterEntity);
            _entityManager.DestroyEntity(bridgeEntity);

            // Deserialize KEXD
            var loadedEntity = _serializationSystem.DeserializeGraph(kexdData, false);

            // Verify synthetic Anchor node position was preserved
            var nodeQuery = _entityManager.CreateEntityQuery(typeof(Node));
            using var nodeEntities = nodeQuery.ToEntityArray(Allocator.Temp);

            bool foundSyntheticAnchor = false;
            foreach (var entity in nodeEntities) {
                var node = _entityManager.GetComponentData<Node>(entity);
                if (node.Type == LegacyNodeType.Anchor) {
                    foundSyntheticAnchor = true;
                    Assert.AreEqual(expectedSyntheticPos.x, node.Position.x, 0.1f,
                        "Synthetic Anchor X position should be preserved after KEXD round-trip");
                    Assert.AreEqual(expectedSyntheticPos.y, node.Position.y, 0.1f,
                        "Synthetic Anchor Y position should be preserved after KEXD round-trip");
                }
            }

            Assert.IsTrue(foundSyntheticAnchor, "Should have a synthetic Anchor node after KEXD round-trip");

            _entityManager.DestroyEntity(loadedEntity);
        }
    }
}
