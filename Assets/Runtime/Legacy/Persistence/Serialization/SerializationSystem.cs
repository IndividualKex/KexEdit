using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static KexEdit.Legacy.Constants;
using CoasterAggregate = KexEdit.Coaster.Coaster;

namespace KexEdit.Legacy.Serialization {
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
                .WithAll<Node, CoasterReference>()
                .Build(EntityManager);
            _connectionQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Connection, CoasterReference>()
                .Build(EntityManager);
            _portQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Port>()
                .Build(EntityManager);
        }

        protected override void OnUpdate() { }

        public void Record(Entity target) {
            var state = SerializeGraph(target);
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

        public Entity Undo(Entity target) {
            if (_undoStack.Count == 0) return Entity.Null;
            var current = SerializeGraph(target);
            var prev = _undoStack.Pop();
            _redoStack.Push(current);
            return DeserializeGraph(prev, restoreUIState: false);
        }

        public Entity Redo(Entity target) {
            if (_redoStack.Count == 0) return Entity.Null;
            var current = SerializeGraph(target);
            var next = _redoStack.Pop();
            _undoStack.Push(current);
            return DeserializeGraph(next, restoreUIState: false);
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

        public byte[] SerializeGraph(Entity target) {
            return SerializeToKEXD(target);
        }

        public byte[] SerializeToKEXD(Entity target) {
            ref readonly var coasterData = ref SystemAPI.GetComponentRW<CoasterData>(target).ValueRO.Value;

            var uiMeta = new KexEdit.Persistence.UIMetadataChunk(Allocator.Temp);
            using var nodeEntities = _nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in nodeEntities) {
                if (SystemAPI.GetComponent<CoasterReference>(entity).Value != target) continue;
                var node = SystemAPI.GetComponent<Node>(entity);
                uiMeta.Positions[node.Id] = node.Position;
            }

            var writer = new KexEdit.Persistence.ChunkWriter(Allocator.Temp);
            KexEdit.Persistence.CoasterSerializer.Write(writer, in coasterData);
            KexEdit.Persistence.ExtensionSerializer.WriteUIMetadata(ref writer, in uiMeta);

            var data = writer.ToArray();
            var result = data.ToArray();

            writer.Dispose();
            uiMeta.Dispose();
            data.Dispose();

            return result;
        }

        public Entity DeserializeGraph(byte[] data, bool restoreUIState = true) {
            var coaster = EntityManager.CreateEntity(typeof(Coaster), typeof(CoasterData));
            EntityManager.SetName(coaster, "Coaster");

            if (data.Length == 0) {
                EntityManager.SetComponentData(coaster, new CoasterData {
                    Value = CoasterAggregate.Create(Allocator.Persistent)
                });
                return coaster;
            }

            return DeserializeKexd(data, coaster, restoreUIState);
        }

        public Entity DeserializeLegacyGraph(byte[] data, bool restoreUIState = true) {
            var coaster = EntityManager.CreateEntity(typeof(Coaster), typeof(CoasterData));
            EntityManager.SetName(coaster, "Coaster");

            if (data.Length == 0) {
                EntityManager.SetComponentData(coaster, new CoasterData {
                    Value = CoasterAggregate.Create(Allocator.Persistent)
                });
                return coaster;
            }

            return DeserializeLegacy(data, coaster, restoreUIState);
        }

        public static bool IsKexdFormat(byte[] data) {
            return data.Length >= 4 &&
                   data[0] == 'K' && data[1] == 'E' &&
                   data[2] == 'X' && data[3] == 'D';
        }

        private Entity DeserializeKexd(byte[] data, Entity coaster, bool restoreUIState) {
            var buffer = new NativeArray<byte>(data, Allocator.Temp);

            var reader = new KexEdit.Persistence.ChunkReader(buffer);
            var coasterAggregate = KexEdit.Persistence.CoasterSerializer.Read(reader, Allocator.Persistent);
            reader.Dispose();

            buffer.Dispose();
            buffer = new NativeArray<byte>(data, Allocator.Temp);
            reader = new KexEdit.Persistence.ChunkReader(buffer);
            var extensions = KexEdit.Persistence.ExtensionSerializer.ReadExtensions(ref reader, Allocator.Temp);
            reader.Dispose();

            EntityManager.SetComponentData(coaster, new CoasterData {
                Value = coasterAggregate
            });

            KexdAdapter.ImportToEcs(
                in coasterAggregate,
                in extensions.UIMetadata,
                coaster,
                EntityManager,
                restoreUIState
            );

            buffer.Dispose();
            extensions.Dispose();
            return coaster;
        }

        private Entity DeserializeLegacy(byte[] data, Entity coaster, bool restoreUIState) {
            var buffer = new NativeArray<byte>(data, Allocator.Temp);
            SerializedGraph serializedGraph = new();
            GraphSerializer.Deserialize(ref serializedGraph, ref buffer);
            buffer.Dispose();

            LegacyImporter.Import(in serializedGraph, Allocator.Persistent, out var coasterAggregate);
            EntityManager.SetComponentData(coaster, new CoasterData {
                Value = coasterAggregate
            });

            if (restoreUIState) {
                ref var timelineState = ref SystemAPI.GetSingletonRW<TimelineState>().ValueRW;
                ref var nodeGraphState = ref SystemAPI.GetSingletonRW<NodeGraphState>().ValueRW;
                ref var cameraState = ref SystemAPI.GetSingletonRW<CameraState>().ValueRW;
                serializedGraph.UIState.ToState(out timelineState, out nodeGraphState, out cameraState);
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var node in serializedGraph.Nodes) {
                DeserializeNode(node, coaster, ref ecb, restoreUIState);
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
                ecb.AddComponent<CoasterReference>(connection, coaster);
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

            return coaster;
        }

        public SerializedNode SerializeNode(Entity entity, in CoasterAggregate coaster, Allocator allocator) {
            Node node = SystemAPI.GetComponent<Node>(entity);
            uint nodeId = node.Id;

            var inputPortBuffer = SystemAPI.GetBuffer<InputPortReference>(entity);
            var outputPortBuffer = SystemAPI.GetBuffer<OutputPortReference>(entity);

            NativeArray<SerializedPort> inputPorts = new(inputPortBuffer.Length, allocator);
            for (int i = 0; i < inputPortBuffer.Length; i++) {
                var portEntity = inputPortBuffer[i];
                Port port = SystemAPI.GetComponent<Port>(portEntity);
                uint portId = port.Id;
                SerializedPort portData = new() { Port = port };
                switch (port.Type) {
                    case PortType.Anchor:
                        PointData anchorValue = SystemAPI.GetComponent<AnchorPort>(portEntity).Value;
                        portData.Value = anchorValue;
                        break;
                    case PortType.Path:
                        break;
                    case PortType.Duration:
                        float durationValue = coaster.Durations.TryGetValue(nodeId, out var d) ? d.Value : 0f;
                        portData.Value.Roll = durationValue;
                        break;
                    case PortType.Position:
                        float3 positionValue = coaster.Vectors.TryGetValue(nodeId, out var pos) ? pos : float3.zero;
                        portData.Value.Roll = positionValue.x;
                        portData.Value.Velocity = positionValue.y;
                        portData.Value.Energy = positionValue.z;
                        break;
                    case PortType.Roll:
                        float rollValue = coaster.Scalars.TryGetValue(portId, out var roll) ? roll : 0f;
                        portData.Value.Roll = rollValue;
                        break;
                    case PortType.Pitch:
                        float pitchValue = coaster.Scalars.TryGetValue(portId, out var pitch) ? pitch : 0f;
                        portData.Value.Roll = pitchValue;
                        break;
                    case PortType.Yaw:
                        float yawValue = coaster.Scalars.TryGetValue(portId, out var yaw) ? yaw : 0f;
                        portData.Value.Roll = yawValue;
                        break;
                    case PortType.Velocity:
                        float velocityValue = coaster.Scalars.TryGetValue(portId, out var velocity) ? velocity : 0f;
                        portData.Value.Roll = velocityValue;
                        break;
                    case PortType.Heart:
                        float heartValue = coaster.Scalars.TryGetValue(portId, out var heart) ? heart : 0f;
                        portData.Value.Roll = heartValue;
                        break;
                    case PortType.Friction:
                        float frictionPhysicsValue = coaster.Scalars.TryGetValue(portId, out var friction) ? friction : 0f;
                        float frictionUIValue = frictionPhysicsValue * FRICTION_PHYSICS_TO_UI_SCALE;
                        portData.Value.Roll = frictionUIValue;
                        break;
                    case PortType.Resistance:
                        float resistancePhysicsValue = coaster.Scalars.TryGetValue(portId, out var resistance) ? resistance : 0f;
                        float resistanceUIValue = resistancePhysicsValue * RESISTANCE_PHYSICS_TO_UI_SCALE;
                        portData.Value.Roll = resistanceUIValue;
                        break;
                    case PortType.Radius:
                        float radiusValue = coaster.Scalars.TryGetValue(portId, out var radius) ? radius : 0f;
                        portData.Value.Roll = radiusValue;
                        break;
                    case PortType.Arc:
                        float arcValue = coaster.Scalars.TryGetValue(portId, out var arc) ? arc : 0f;
                        portData.Value.Roll = arcValue;
                        break;
                    case PortType.Axis:
                        float axisValue = coaster.Scalars.TryGetValue(portId, out var axis) ? axis : 0f;
                        portData.Value.Roll = axisValue;
                        break;
                    case PortType.InWeight:
                        float inWeightValue = coaster.Scalars.TryGetValue(portId, out var inWeight) ? inWeight : 0f;
                        portData.Value.Roll = inWeightValue;
                        break;
                    case PortType.OutWeight:
                        float outWeightValue = coaster.Scalars.TryGetValue(portId, out var outWeight) ? outWeight : 0f;
                        portData.Value.Roll = outWeightValue;
                        break;
                    case PortType.LeadIn:
                        float leadInValue = coaster.Scalars.TryGetValue(portId, out var leadIn) ? leadIn : 0f;
                        portData.Value.Roll = leadInValue;
                        break;
                    case PortType.LeadOut:
                        float leadOutValue = coaster.Scalars.TryGetValue(portId, out var leadOut) ? leadOut : 0f;
                        portData.Value.Roll = leadOutValue;
                        break;
                    case PortType.Rotation:
                        float3 rotationValue = coaster.GetRotation(nodeId);
                        portData.Value.Roll = rotationValue.x;
                        portData.Value.Velocity = rotationValue.y;
                        portData.Value.Energy = rotationValue.z;
                        break;
                    case PortType.Scale:
                        float scaleValue = coaster.Scalars.TryGetValue(portId, out var scale) ? scale : 0f;
                        portData.Value.Roll = scaleValue;
                        break;
                    case PortType.Start:
                        float startValue = coaster.Scalars.TryGetValue(portId, out var start) ? start : 0f;
                        portData.Value.Roll = startValue;
                        break;
                    case PortType.End:
                        float endValue = coaster.Scalars.TryGetValue(portId, out var end) ? end : 0f;
                        portData.Value.Roll = endValue;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                inputPorts[i] = portData;
            }

            Anchor anchor = SystemAPI.GetComponent<Anchor>(entity);

            // Sync Anchor component fields from port values to ensure consistency on save
            if (node.Type == NodeType.Anchor) {
                for (int i = 0; i < inputPorts.Length; i++) {
                    var port = inputPorts[i];
                    switch (port.Port.Type) {
                        case PortType.Velocity:
                            anchor.Value.Velocity = port.Value.Roll;
                            break;
                        case PortType.Position:
                            anchor.Value.HeartPosition = new float3(port.Value.Roll, port.Value.Velocity, port.Value.Energy);
                            break;
                    }
                }
            }

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
            bool steering = node.Type switch {
                NodeType.GeometricSection => SystemAPI.HasComponent<Steering>(entity)
                    ? SystemAPI.GetComponent<Steering>(entity)
                    : true,
                _ => true,
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
                NodeType.Mesh => SystemAPI.GetComponent<NodeMeshReference>(entity).FilePath,
                NodeType.Append => SystemAPI.GetComponent<AppendReference>(entity).FilePath,
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
            if (node.Type == NodeType.GeometricSection) fieldFlags |= NodeFieldFlags.HasSteering;
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
                Steering = steering,
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

        public Entity DeserializeNode(SerializedNode node, Entity coaster, ref EntityCommandBuffer ecb, bool restoreUIState) {
            var entity = ecb.CreateEntity();

            NodeType type = node.Node.Type;
            if (restoreUIState) node.Node.Selected = false;
            ecb.AddComponent(entity, node.Node);
            ecb.AddComponent<CoasterReference>(entity, coaster);
            ecb.AddComponent<Dirty>(entity);

            ecb.AddBuffer<InputPortReference>(entity);
            foreach (var port in node.InputPorts) {
                var portEntity = ecb.CreateEntity();
                ecb.AddComponent<Port>(portEntity, port.Port);
                ecb.AddComponent<Dirty>(portEntity);
                switch (port.Port.Type) {
                    case PortType.Anchor:
                        ecb.AddComponent<AnchorPort>(portEntity, port.Value);
                        break;
                    case PortType.Path:
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
                    case PortType.InWeight:
                        ecb.AddComponent<InWeightPort>(portEntity, port.Value.Roll);
                        break;
                    case PortType.OutWeight:
                        ecb.AddComponent<OutWeightPort>(portEntity, port.Value.Roll);
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

            if (type == NodeType.ForceSection ||
                type == NodeType.GeometricSection ||
                type == NodeType.CurvedSection ||
                type == NodeType.CopyPathSection ||
                type == NodeType.Bridge ||
                type == NodeType.ReversePath) {
                ecb.AddBuffer<CorePointBuffer>(entity);
#if VALIDATE_COASTER_PARITY
                ecb.AddBuffer<CoasterPointBuffer>(entity);
#endif
                ecb.AddBuffer<ReadNormalForce>(entity);
                ecb.AddBuffer<ReadLateralForce>(entity);
                ecb.AddBuffer<ReadPitchSpeed>(entity);
                ecb.AddBuffer<ReadYawSpeed>(entity);
                ecb.AddBuffer<ReadRollSpeed>(entity);
            }

            if (type == NodeType.ForceSection ||
                type == NodeType.GeometricSection ||
                type == NodeType.CurvedSection ||
                type == NodeType.CopyPathSection ||
                type == NodeType.Bridge) {
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
                ecb.AddComponent<TrackStyleHash>(entity);
            }

            if (type == NodeType.Mesh) {
                ecb.AddComponent<Render>(entity, node.Render);
            }

            if (type == NodeType.ForceSection ||
                type == NodeType.GeometricSection) {
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

                    bool steering = (node.FieldFlags & NodeFieldFlags.HasSteering) == 0 || node.Steering;
                    ecb.AddComponent<Steering>(entity, steering);
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
                ecb.AddComponent(entity, new NodeMeshReference {
                    Value = Entity.Null,
                    FilePath = node.MeshFilePath,
                    Requested = false
                });
            }
            else if (type == NodeType.Append) {
                ecb.AddComponent(entity, new AppendReference {
                    Value = Entity.Null,
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
