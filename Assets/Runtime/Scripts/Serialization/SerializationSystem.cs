using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static KexEdit.Constants;

namespace KexEdit.Serialization {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SerializationSystem : SystemBase {
        public static SerializationSystem Instance { get; private set; }

        private const int MAX_UNDO_STEPS = 32;

        private Stack<byte[]> _undoStack = new();
        private Stack<byte[]> _redoStack = new();
        private Stack<byte[]> _tempStack = new();
        private EntityQuery _nodeQuery;
        private EntityQuery _connectionQuery;
        private EntityQuery _portQuery;

        public bool CanUndo => Instance._undoStack.Count > 0;
        public bool CanRedo => Instance._redoStack.Count > 0;

        public static event Action Recorded;

        public SerializationSystem() {
            Instance = this;
        }

        protected override void OnCreate() {
            _nodeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAspect<NodeAspect>()
                .Build(EntityManager);
            _connectionQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAspect<ConnectionAspect>()
                .Build(EntityManager);
            _portQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Port>()
                .Build(EntityManager);
        }

        protected override void OnUpdate() { }

        public void Record() {
            var state = SerializeGraph();
            _undoStack.Push(state);
            _redoStack.Clear();

            if (_undoStack.Count > MAX_UNDO_STEPS) {
                _tempStack.Clear();
                for (int i = 0; i < MAX_UNDO_STEPS; i++) {
                    _tempStack.Push(_undoStack.Pop());
                }
                _undoStack.Clear();
                while (_tempStack.Count > 0) {
                    _undoStack.Push(_tempStack.Pop());
                }
            }

            Recorded?.Invoke();
        }

        public void Undo() {
            if (_undoStack.Count == 0) return;
            var current = SerializeGraph();
            var prev = _undoStack.Pop();
            _redoStack.Push(current);
            DeserializeGraph(prev, restoreUIState: false);
        }

        public void Redo() {
            if (_redoStack.Count == 0) return;
            var current = SerializeGraph();
            var next = _redoStack.Pop();
            _undoStack.Push(current);
            DeserializeGraph(next, restoreUIState: false);
        }

        public void Clear() {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        public static void LoadGraph(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return;
            
            byte[] data = File.ReadAllBytes(filePath);
            if (data?.Length > 0) {
                Instance.DeserializeGraph(data, restoreUIState: false);
            }
        }

        public byte[] SerializeGraph() {
            SerializedGraph graph = new();

            var timelineState = SystemAPI.GetSingleton<TimelineState>();
            var nodeGraphState = SystemAPI.GetSingleton<NodeGraphState>();
            var cameraState = SystemAPI.GetSingleton<CameraState>();
            graph.UIState = SerializedUIState.FromState(timelineState, nodeGraphState, cameraState);

            using var nodeEntities = _nodeQuery.ToEntityArray(Allocator.Temp);
            graph.Nodes = new(nodeEntities.Length, Allocator.Temp);
            for (int i = 0; i < nodeEntities.Length; i++) {
                graph.Nodes[i] = SerializeNode(nodeEntities[i], Allocator.Temp);
            }

            using var connectionEntities = _connectionQuery.ToEntityArray(Allocator.Temp);
            graph.Edges = new(connectionEntities.Length, Allocator.Temp);
            for (int i = 0; i < connectionEntities.Length; i++) {
                var connection = SystemAPI.GetComponent<Connection>(connectionEntities[i]);
                var source = SystemAPI.GetComponent<Port>(connection.Source).Id;
                var target = SystemAPI.GetComponent<Port>(connection.Target).Id;
                graph.Edges[i] = new SerializedEdge {
                    Id = connection.Id,
                    SourceId = source,
                    TargetId = target,
                    Selected = connection.Selected,
                };
            }

            int size = SizeCalculator.CalculateSize(ref graph);
            var buffer = new NativeArray<byte>(size, Allocator.Temp);

            int actualSize = GraphSerializer.Serialize(ref graph, ref buffer);

            var result = new byte[actualSize];
            buffer.Slice(0, actualSize).CopyTo(result);
            buffer.Dispose();

            graph.Dispose();
            return result;
        }

        public void DeserializeGraph(byte[] data, bool restoreUIState = true) {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<Node>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
            }
            foreach (var (_, entity) in SystemAPI.Query<Port>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
            }
            foreach (var (_, entity) in SystemAPI.Query<Connection>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            if (data.Length == 0) return;

            var buffer = new NativeArray<byte>(data, Allocator.Temp);
            SerializedGraph serializedGraph = new();
            GraphSerializer.Deserialize(ref serializedGraph, ref buffer);
            buffer.Dispose();

            if (restoreUIState) {
                ref var timelineState = ref SystemAPI.GetSingletonRW<TimelineState>().ValueRW;
                ref var nodeGraphState = ref SystemAPI.GetSingletonRW<NodeGraphState>().ValueRW;
                ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;
                serializedGraph.UIState.ToState(out timelineState, out nodeGraphState, out cameraState);
            }

            ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var node in serializedGraph.Nodes) {
                DeserializeNode(node, ecb);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            using var ports = _portQuery.ToEntityArray(Allocator.Temp);
            var portMap = new NativeHashMap<uint, Entity>(ports.Length, Allocator.Temp);
            foreach (var port in ports) {
                uint id = SystemAPI.GetComponent<Port>(port).Id;
                portMap[id] = port;
            }

            ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var edge in serializedGraph.Edges) {
                var source = portMap[edge.SourceId];
                var target = portMap[edge.TargetId];
                var connection = ecb.CreateEntity();
                ecb.AddComponent<Dirty>(connection);
                ecb.AddComponent(connection, new Connection {
                    Id = edge.Id,
                    Source = source,
                    Target = target,
                    Selected = edge.Selected,
                });
                ecb.SetName(connection, "Connection");
            }
            ecb.Playback(EntityManager);
            ecb.Dispose();

            portMap.Dispose();
            serializedGraph.Dispose();
        }

        public SerializedNode SerializeNode(Entity entity, Allocator allocator) {
            Node node = SystemAPI.GetComponent<Node>(entity);

            var inputPortBuffer = SystemAPI.GetBuffer<InputPortReference>(entity);
            var outputPortBuffer = SystemAPI.GetBuffer<OutputPortReference>(entity);

            NativeArray<SerializedPort> inputPorts = new(inputPortBuffer.Length, allocator);
            for (int i = 0; i < inputPortBuffer.Length; i++) {
                var portEntity = inputPortBuffer[i];
                Port port = SystemAPI.GetComponent<Port>(portEntity);
                SerializedPort portData = new() { Port = port };
                switch (port.Type) {
                    case PortType.Anchor:
                        PointData anchorValue = SystemAPI.GetComponent<AnchorPort>(portEntity).Value;
                        portData.Value = anchorValue;
                        break;
                    case PortType.Path:
                        break;
                    case PortType.Duration:
                        float durationValue = SystemAPI.GetComponent<DurationPort>(portEntity).Value;
                        portData.Value.Roll = durationValue;
                        break;
                    case PortType.Position:
                        float3 positionValue = SystemAPI.GetComponent<PositionPort>(portEntity).Value;
                        portData.Value.Roll = positionValue.x;
                        portData.Value.Velocity = positionValue.y;
                        portData.Value.Energy = positionValue.z;
                        break;
                    case PortType.Roll:
                        float rollValue = SystemAPI.GetComponent<RollPort>(portEntity).Value;
                        portData.Value.Roll = rollValue;
                        break;
                    case PortType.Pitch:
                        float pitchValue = SystemAPI.GetComponent<PitchPort>(portEntity).Value;
                        portData.Value.Roll = pitchValue;
                        break;
                    case PortType.Yaw:
                        float yawValue = SystemAPI.GetComponent<YawPort>(portEntity).Value;
                        portData.Value.Roll = yawValue;
                        break;
                    case PortType.Velocity:
                        float velocityValue = SystemAPI.GetComponent<VelocityPort>(portEntity).Value;
                        portData.Value.Roll = velocityValue;
                        break;
                    case PortType.Heart:
                        float heartValue = SystemAPI.GetComponent<HeartPort>(portEntity).Value;
                        portData.Value.Roll = heartValue;
                        break;
                    case PortType.Friction:
                        float frictionPhysicsValue = SystemAPI.GetComponent<FrictionPort>(portEntity).Value;
                        float frictionUIValue = frictionPhysicsValue * FRICTION_PHYSICS_TO_UI_SCALE;
                        portData.Value.Roll = frictionUIValue;
                        break;
                    case PortType.Resistance:
                        float resistancePhysicsValue = SystemAPI.GetComponent<ResistancePort>(portEntity).Value;
                        float resistanceUIValue = resistancePhysicsValue * RESISTANCE_PHYSICS_TO_UI_SCALE;
                        portData.Value.Roll = resistanceUIValue;
                        break;
                    case PortType.Radius:
                        float radiusValue = SystemAPI.GetComponent<RadiusPort>(portEntity).Value;
                        portData.Value.Roll = radiusValue;
                        break;
                    case PortType.Arc:
                        float arcValue = SystemAPI.GetComponent<ArcPort>(portEntity).Value;
                        portData.Value.Roll = arcValue;
                        break;
                    case PortType.Axis:
                        float axisValue = SystemAPI.GetComponent<AxisPort>(portEntity).Value;
                        portData.Value.Roll = axisValue;
                        break;
                    case PortType.LeadIn:
                        float leadInValue = SystemAPI.GetComponent<LeadInPort>(portEntity).Value;
                        portData.Value.Roll = leadInValue;
                        break;
                    case PortType.LeadOut:
                        float leadOutValue = SystemAPI.GetComponent<LeadOutPort>(portEntity).Value;
                        portData.Value.Roll = leadOutValue;
                        break;
                    case PortType.Rotation:
                        float3 rotationValue = SystemAPI.GetComponent<RotationPort>(portEntity).Value;
                        portData.Value.Roll = rotationValue.x;
                        portData.Value.Velocity = rotationValue.y;
                        portData.Value.Energy = rotationValue.z;
                        break;
                    case PortType.Scale:
                        float scaleValue = SystemAPI.GetComponent<ScalePort>(portEntity).Value;
                        portData.Value.Roll = scaleValue;
                        break;
                    case PortType.Start:
                        float startValue = SystemAPI.GetComponent<StartPort>(portEntity).Value;
                        portData.Value.Roll = startValue;
                        break;
                    case PortType.End:
                        float endValue = SystemAPI.GetComponent<EndPort>(portEntity).Value;
                        portData.Value.Roll = endValue;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                inputPorts[i] = portData;
            }

            Anchor anchor = SystemAPI.GetComponent<Anchor>(entity);

            CurveData curveData = node.Type switch {
                NodeType.CurvedSection => SystemAPI.GetComponent<CurveData>(entity),
                _ => default,
            };

            Duration duration = node.Type switch {
                NodeType.ForceSection or NodeType.GeometricSection => SystemAPI.GetComponent<Duration>(entity),
                _ => default,
            };
            bool render = node.Type switch {
                NodeType.ForceSection or NodeType.GeometricSection or
                NodeType.CurvedSection or NodeType.CopyPathSection or
                NodeType.Bridge or NodeType.Mesh => SystemAPI.GetComponent<Render>(entity),
                _ => false,
            };
            PropertyOverrides overrides = node.Type switch {
                NodeType.ForceSection or NodeType.GeometricSection or
                NodeType.CurvedSection or NodeType.CopyPathSection or
                NodeType.Bridge => SystemAPI.HasComponent<PropertyOverrides>(entity)
                    ? SystemAPI.GetComponent<PropertyOverrides>(entity)
                    : PropertyOverrides.Default,
                _ => PropertyOverrides.Default,
            };
            SelectedProperties selectedProperties = node.Type switch {
                NodeType.ForceSection or NodeType.GeometricSection or
                NodeType.CurvedSection or NodeType.CopyPathSection or
                NodeType.Bridge => SystemAPI.GetComponent<SelectedProperties>(entity),
                _ => default,
            };
            FixedString512Bytes meshFilePath = node.Type switch {
                NodeType.Mesh => SystemAPI.ManagedAPI.GetComponent<MeshReference>(entity).FilePath,
                _ => default,
            };
            DynamicBuffer<RollSpeedKeyframe>? rollSpeedKeyframeBuffer = node.Type switch {
                NodeType.ForceSection or NodeType.GeometricSection or
                NodeType.CurvedSection => SystemAPI.GetBuffer<RollSpeedKeyframe>(entity),
                _ => null,
            };
            DynamicBuffer<NormalForceKeyframe>? normalForceKeyframeBuffer = node.Type switch {
                NodeType.ForceSection => SystemAPI.GetBuffer<NormalForceKeyframe>(entity),
                _ => null,
            };
            DynamicBuffer<LateralForceKeyframe>? lateralForceKeyframeBuffer = node.Type switch {
                NodeType.ForceSection => SystemAPI.GetBuffer<LateralForceKeyframe>(entity),
                _ => null,
            };
            DynamicBuffer<PitchSpeedKeyframe>? pitchSpeedKeyframeBuffer = node.Type switch {
                NodeType.GeometricSection => SystemAPI.GetBuffer<PitchSpeedKeyframe>(entity),
                _ => null,
            };
            DynamicBuffer<YawSpeedKeyframe>? yawSpeedKeyframeBuffer = node.Type switch {
                NodeType.GeometricSection => SystemAPI.GetBuffer<YawSpeedKeyframe>(entity),
                _ => null,
            };
            DynamicBuffer<FixedVelocityKeyframe>? fixedVelocityKeyframeBuffer = node.Type switch {
                NodeType.ForceSection or NodeType.GeometricSection or
                NodeType.CurvedSection or NodeType.CopyPathSection or
                NodeType.Bridge => SystemAPI.GetBuffer<FixedVelocityKeyframe>(entity),
                _ => null,
            };
            DynamicBuffer<HeartKeyframe>? heartKeyframeBuffer = node.Type switch {
                NodeType.ForceSection or NodeType.GeometricSection or
                NodeType.CurvedSection or NodeType.CopyPathSection or
                NodeType.Bridge => SystemAPI.GetBuffer<HeartKeyframe>(entity),
                _ => null,
            };
            DynamicBuffer<FrictionKeyframe>? frictionKeyframeBuffer = node.Type switch {
                NodeType.ForceSection or NodeType.GeometricSection or
                NodeType.CurvedSection or NodeType.CopyPathSection or
                NodeType.Bridge => SystemAPI.GetBuffer<FrictionKeyframe>(entity),
                _ => null,
            };
            DynamicBuffer<ResistanceKeyframe>? resistanceKeyframeBuffer = node.Type switch {
                NodeType.ForceSection or NodeType.GeometricSection or
                NodeType.CurvedSection or NodeType.CopyPathSection or
                NodeType.Bridge => SystemAPI.GetBuffer<ResistanceKeyframe>(entity),
                _ => null,
            };
            DynamicBuffer<TrackStyleKeyframe>? trackStyleKeyframeBuffer = node.Type switch {
                NodeType.ForceSection or NodeType.GeometricSection or
                NodeType.CurvedSection or NodeType.CopyPathSection or
                NodeType.Bridge => SystemAPI.GetBuffer<TrackStyleKeyframe>(entity),
                _ => null,
            };

            NativeArray<RollSpeedKeyframe> rollSpeedKeyframes = rollSpeedKeyframeBuffer.HasValue ?
                rollSpeedKeyframeBuffer.Value.ToNativeArray(allocator) : new(0, allocator);

            NativeArray<NormalForceKeyframe> normalForceKeyframes = normalForceKeyframeBuffer.HasValue ?
                normalForceKeyframeBuffer.Value.ToNativeArray(allocator) : new(0, allocator);

            NativeArray<LateralForceKeyframe> lateralForceKeyframes = lateralForceKeyframeBuffer.HasValue ?
                lateralForceKeyframeBuffer.Value.ToNativeArray(allocator) : new(0, allocator);

            NativeArray<PitchSpeedKeyframe> pitchSpeedKeyframes = pitchSpeedKeyframeBuffer.HasValue ?
                pitchSpeedKeyframeBuffer.Value.ToNativeArray(allocator) : new(0, allocator);

            NativeArray<YawSpeedKeyframe> yawSpeedKeyframes = yawSpeedKeyframeBuffer.HasValue ?
                yawSpeedKeyframeBuffer.Value.ToNativeArray(allocator) : new(0, allocator);

            NativeArray<FixedVelocityKeyframe> fixedVelocityKeyframes = fixedVelocityKeyframeBuffer.HasValue ?
                fixedVelocityKeyframeBuffer.Value.ToNativeArray(allocator) : new(0, allocator);

            NativeArray<HeartKeyframe> heartKeyframes = heartKeyframeBuffer.HasValue ?
                heartKeyframeBuffer.Value.ToNativeArray(allocator) : new(0, allocator);

            NativeArray<FrictionKeyframe> frictionKeyframes = frictionKeyframeBuffer.HasValue ?
                frictionKeyframeBuffer.Value.ToNativeArray(allocator) : new(0, allocator);

            NativeArray<ResistanceKeyframe> resistanceKeyframes = resistanceKeyframeBuffer.HasValue ?
                resistanceKeyframeBuffer.Value.ToNativeArray(allocator) : new(0, allocator);

            NativeArray<TrackStyleKeyframe> trackStyleKeyframes = trackStyleKeyframeBuffer.HasValue ?
                trackStyleKeyframeBuffer.Value.ToNativeArray(allocator) : new(0, allocator);

            NativeArray<SerializedPort> outputPorts = new(outputPortBuffer.Length, allocator);
            for (int i = 0; i < outputPortBuffer.Length; i++) {
                var portEntity = outputPortBuffer[i];
                Port port = SystemAPI.GetComponent<Port>(portEntity);
                SerializedPort portData = new() { Port = port };
                switch (port.Type) {
                    case PortType.Anchor:
                        PointData anchorValue = SystemAPI.GetComponent<AnchorPort>(portEntity).Value;
                        portData.Value = anchorValue;
                        break;
                    case PortType.Path:
                        break;
                    case PortType.Duration:
                        float durationValue = SystemAPI.GetComponent<DurationPort>(portEntity).Value;
                        portData.Value.Roll = durationValue;
                        break;
                    case PortType.Position:
                        float3 positionValue = SystemAPI.GetComponent<PositionPort>(portEntity).Value;
                        portData.Value.Roll = positionValue.x;
                        portData.Value.Velocity = positionValue.y;
                        portData.Value.Energy = positionValue.z;
                        break;
                    case PortType.Roll:
                        float rollValue = SystemAPI.GetComponent<RollPort>(portEntity).Value;
                        portData.Value.Roll = rollValue;
                        break;
                    case PortType.Pitch:
                        float pitchValue = SystemAPI.GetComponent<PitchPort>(portEntity).Value;
                        portData.Value.Roll = pitchValue;
                        break;
                    case PortType.Yaw:
                        float yawValue = SystemAPI.GetComponent<YawPort>(portEntity).Value;
                        portData.Value.Roll = yawValue;
                        break;
                    case PortType.Velocity:
                        float velocityValue = SystemAPI.GetComponent<VelocityPort>(portEntity).Value;
                        portData.Value.Roll = velocityValue;
                        break;
                    case PortType.Heart:
                        float heartValue = SystemAPI.GetComponent<HeartPort>(portEntity).Value;
                        portData.Value.Roll = heartValue;
                        break;
                    case PortType.Friction:
                        float frictionPhysicsValue = SystemAPI.GetComponent<FrictionPort>(portEntity).Value;
                        float frictionUIValue = frictionPhysicsValue * FRICTION_PHYSICS_TO_UI_SCALE;
                        portData.Value.Roll = frictionUIValue;
                        break;
                    case PortType.Resistance:
                        float resistancePhysicsValue = SystemAPI.GetComponent<ResistancePort>(portEntity).Value;
                        float resistanceUIValue = resistancePhysicsValue * RESISTANCE_PHYSICS_TO_UI_SCALE;
                        portData.Value.Roll = resistanceUIValue;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                outputPorts[i] = portData;
            }

            NodeFieldFlags fieldFlags = NodeFieldFlags.None;
            if (render) fieldFlags |= NodeFieldFlags.HasRender;
            if (node.Selected) fieldFlags |= NodeFieldFlags.HasSelected;
            if (!overrides.Equals(PropertyOverrides.Default)) fieldFlags |= NodeFieldFlags.HasPropertyOverrides;
            if (!selectedProperties.Equals(default(SelectedProperties))) fieldFlags |= NodeFieldFlags.HasSelectedProperties;
            if (!curveData.Equals(default(CurveData))) fieldFlags |= NodeFieldFlags.HasCurveData;
            if (!duration.Equals(default(Duration))) fieldFlags |= NodeFieldFlags.HasDuration;
            if (!meshFilePath.IsEmpty) fieldFlags |= NodeFieldFlags.HasMeshFilePath;

            return new SerializedNode {
                Node = node,
                Anchor = anchor,
                FieldFlags = fieldFlags,
                CurveData = curveData,
                Duration = duration,
                Render = render,
                Selected = node.Selected,
                PropertyOverrides = overrides,
                SelectedProperties = selectedProperties,
                MeshFilePath = meshFilePath,
                InputPorts = inputPorts,
                OutputPorts = outputPorts,
                RollSpeedKeyframes = rollSpeedKeyframes,
                NormalForceKeyframes = normalForceKeyframes,
                LateralForceKeyframes = lateralForceKeyframes,
                PitchSpeedKeyframes = pitchSpeedKeyframes,
                YawSpeedKeyframes = yawSpeedKeyframes,
                FixedVelocityKeyframes = fixedVelocityKeyframes,
                HeartKeyframes = heartKeyframes,
                FrictionKeyframes = frictionKeyframes,
                ResistanceKeyframes = resistanceKeyframes,
                TrackStyleKeyframes = trackStyleKeyframes,
            };
        }

        public Entity DeserializeNode(SerializedNode node, EntityCommandBuffer ecb) {
            var entity = ecb.CreateEntity();

            NodeType type = node.Node.Type;
            ecb.AddComponent(entity, node.Node);
            ecb.AddComponent<Dirty>(entity);

            ecb.AddBuffer<InputPortReference>(entity);
            foreach (var port in node.InputPorts) {
                var portEntity = ecb.CreateEntity();
                ecb.AddComponent<Port>(portEntity, port.Port);
                ecb.AddComponent<Dirty>(portEntity, true);
                switch (port.Port.Type) {
                    case PortType.Anchor:
                        ecb.AddComponent<AnchorPort>(portEntity, port.Value);
                        break;
                    case PortType.Path:
                        ecb.AddBuffer<PathPort>(portEntity);
                        break;
                    case PortType.Duration:
                        ecb.AddComponent<DurationPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Position:
                        float3 positionValue = new(port.Value.Roll, port.Value.Velocity, port.Value.Energy);
                        ecb.AddComponent<PositionPort>(portEntity, positionValue);
                        break;
                    case PortType.Roll:
                        ecb.AddComponent<RollPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Pitch:
                        ecb.AddComponent<PitchPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Yaw:
                        ecb.AddComponent<YawPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Velocity:
                        ecb.AddComponent<VelocityPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Heart:
                        ecb.AddComponent<HeartPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Friction:
                        float frictionPhysicsValue = port.Value.Roll * FRICTION_UI_TO_PHYSICS_SCALE;
                        ecb.AddComponent<FrictionPort>(portEntity, frictionPhysicsValue);
                        break;
                    case PortType.Resistance:
                        float resistancePhysicsValue = port.Value.Roll * RESISTANCE_UI_TO_PHYSICS_SCALE;
                        ecb.AddComponent<ResistancePort>(portEntity, resistancePhysicsValue);
                        break;
                    case PortType.Radius:
                        ecb.AddComponent<RadiusPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Arc:
                        ecb.AddComponent<ArcPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Axis:
                        ecb.AddComponent<AxisPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.LeadIn:
                        ecb.AddComponent<LeadInPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.LeadOut:
                        ecb.AddComponent<LeadOutPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Rotation:
                        float3 rotationValue = new(port.Value.Roll, port.Value.Velocity, port.Value.Energy);
                        ecb.AddComponent<RotationPort>(portEntity, rotationValue);
                        break;
                    case PortType.Scale:
                        ecb.AddComponent<ScalePort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Start:
                        ecb.AddComponent<StartPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.End:
                        ecb.AddComponent<EndPort>(portEntity, port.Value.Roll);
                        break;
                    default:
                        throw new NotImplementedException();
                }
                ecb.AppendToBuffer<InputPortReference>(entity, portEntity);
                ecb.SetName(portEntity, "Input Port");
            }

            ecb.AddComponent<Anchor>(entity, node.Anchor);

            if (type == NodeType.ForceSection
                || type == NodeType.GeometricSection
                || type == NodeType.CurvedSection
                || type == NodeType.CopyPathSection
                || type == NodeType.Bridge
                || type == NodeType.ReversePath) {
                ecb.AddBuffer<Point>(entity);
            }

            if (type == NodeType.ForceSection
                || type == NodeType.GeometricSection
                || type == NodeType.CurvedSection
                || type == NodeType.CopyPathSection
                || type == NodeType.Bridge) {
                ecb.AddComponent<Render>(entity, node.Render);
                ecb.AddComponent(entity, node.PropertyOverrides);
                ecb.AddComponent(entity, node.SelectedProperties);
                ecb.AddBuffer<FixedVelocityKeyframe>(entity);
                foreach (var keyframe in node.FixedVelocityKeyframes) {
                    ecb.AppendToBuffer(entity, keyframe);
                }
                ecb.AddBuffer<HeartKeyframe>(entity);
                foreach (var keyframe in node.HeartKeyframes) {
                    ecb.AppendToBuffer(entity, keyframe);
                }
                ecb.AddBuffer<FrictionKeyframe>(entity);
                foreach (var keyframe in node.FrictionKeyframes) {
                    ecb.AppendToBuffer(entity, keyframe);
                }
                ecb.AddBuffer<ResistanceKeyframe>(entity);
                foreach (var keyframe in node.ResistanceKeyframes) {
                    ecb.AppendToBuffer(entity, keyframe);
                }
                ecb.AddBuffer<TrackStyleKeyframe>(entity);
                foreach (var keyframe in node.TrackStyleKeyframes) {
                    ecb.AppendToBuffer(entity, keyframe);
                }
                ecb.AddComponent<StyleHash>(entity);
                ecb.AddComponent<RenderedStyleHash>(entity);
            }

            if (type == NodeType.Mesh) {
                ecb.AddComponent<Render>(entity, node.Render);
            }

            if (type == NodeType.ForceSection
                || type == NodeType.GeometricSection) {
                ecb.AddComponent(entity, node.Duration);

                ecb.AddBuffer<RollSpeedKeyframe>(entity);
                foreach (var keyframe in node.RollSpeedKeyframes) {
                    ecb.AppendToBuffer(entity, keyframe);
                }

                if (type == NodeType.ForceSection) {
                    ecb.AddBuffer<NormalForceKeyframe>(entity);
                    foreach (var keyframe in node.NormalForceKeyframes) {
                        ecb.AppendToBuffer(entity, keyframe);
                    }

                    ecb.AddBuffer<LateralForceKeyframe>(entity);
                    foreach (var keyframe in node.LateralForceKeyframes) {
                        ecb.AppendToBuffer(entity, keyframe);
                    }
                }
                else if (type == NodeType.GeometricSection) {
                    ecb.AddBuffer<PitchSpeedKeyframe>(entity);
                    foreach (var keyframe in node.PitchSpeedKeyframes) {
                        ecb.AppendToBuffer(entity, keyframe);
                    }

                    ecb.AddBuffer<YawSpeedKeyframe>(entity);
                    foreach (var keyframe in node.YawSpeedKeyframes) {
                        ecb.AppendToBuffer(entity, keyframe);
                    }
                }
            }

            if (type == NodeType.CurvedSection) {
                ecb.AddComponent(entity, node.CurveData);
                ecb.AddBuffer<RollSpeedKeyframe>(entity);
                foreach (var keyframe in node.RollSpeedKeyframes) {
                    ecb.AppendToBuffer(entity, keyframe);
                }
            }

            if (type == NodeType.CopyPathSection) {
                ecb.AddComponent<CopyPathSectionTag>(entity);
            }
            else if (type == NodeType.Bridge) {
                ecb.AddComponent<BridgeTag>(entity);
            }
            else if (type == NodeType.Reverse) {
                ecb.AddComponent<ReverseTag>(entity);
            }
            else if (type == NodeType.ReversePath) {
                ecb.AddComponent<ReversePathTag>(entity);
            }
            else if (type == NodeType.Mesh) {
                ecb.AddComponent(entity, new MeshReference {
                    Value = null,
                    FilePath = node.MeshFilePath,
                    Loaded = false
                });
            }

            ecb.AddBuffer<OutputPortReference>(entity);
            foreach (var port in node.OutputPorts) {
                var portEntity = ecb.CreateEntity();
                ecb.AddComponent<Port>(portEntity, port.Port);
                ecb.AddComponent<Dirty>(portEntity);
                switch (port.Port.Type) {
                    case PortType.Anchor:
                        ecb.AddComponent<AnchorPort>(portEntity, port.Value);
                        break;
                    case PortType.Path:
                        ecb.AddBuffer<PathPort>(portEntity);
                        break;
                    case PortType.Duration:
                        ecb.AddComponent<DurationPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Position:
                        float3 positionValue = new(port.Value.Roll, port.Value.Velocity, port.Value.Energy);
                        ecb.AddComponent<PositionPort>(portEntity, positionValue);
                        break;
                    case PortType.Roll:
                        ecb.AddComponent<RollPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Pitch:
                        ecb.AddComponent<PitchPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Yaw:
                        ecb.AddComponent<YawPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Velocity:
                        ecb.AddComponent<VelocityPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Heart:
                        ecb.AddComponent<HeartPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.Friction:
                        float frictionPhysicsValue = port.Value.Roll * FRICTION_UI_TO_PHYSICS_SCALE;
                        ecb.AddComponent<FrictionPort>(portEntity, frictionPhysicsValue);
                        break;
                    case PortType.Resistance:
                        float resistancePhysicsValue = port.Value.Roll * RESISTANCE_UI_TO_PHYSICS_SCALE;
                        ecb.AddComponent<ResistancePort>(portEntity, resistancePhysicsValue);
                        break;
                    default:
                        throw new NotImplementedException();
                }
                ecb.AppendToBuffer<OutputPortReference>(entity, portEntity);
                ecb.SetName(portEntity, "Output Port");
            }

            ecb.SetName(entity, "Node");
            return entity;
        }
    }
}
