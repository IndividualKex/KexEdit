using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct GraphSystem : ISystem {
        private EntityQuery _nodeQuery;
        private EntityQuery _connectionQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            _nodeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAspect<NodeAspect>()
                .Build(state.EntityManager);
            _connectionQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Connection>()
                .Build(state.EntityManager);

            state.RequireForUpdate(_nodeQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            PropagateAnchors(ref state);
            PropagateInputPorts(ref state);
            PropagateConnections(ref state);
        }

        private void PropagateAnchors(ref SystemState state) {
            var nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var nodeEntity in nodes) {
                var node = SystemAPI.GetAspect<NodeAspect>(nodeEntity);
                if (node.Type != NodeType.Anchor || !node.Dirty) continue;

                var outputPort = SystemAPI.GetBuffer<OutputPortReference>(nodeEntity)[0];
                ref Dirty dirty = ref SystemAPI.GetComponentRW<Dirty>(outputPort).ValueRW;

                ref AnchorPort port = ref SystemAPI.GetComponentRW<AnchorPort>(outputPort).ValueRW;
                port.Value = SystemAPI.GetComponent<Anchor>(nodeEntity);

                node.Dirty = false;
                dirty = true;
            }
            nodes.Dispose();
        }

        private void PropagateInputPorts(ref SystemState state) {
            var nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var nodeEntity in nodes) {
                var node = SystemAPI.GetAspect<NodeAspect>(nodeEntity);

                for (int i = 0; i < node.InputPorts.Length; i++) {
                    var inputPort = node.InputPorts[i];
                    ref Dirty inputPortDirty = ref SystemAPI.GetComponentRW<Dirty>(inputPort).ValueRW;
                    if (!inputPortDirty) continue;
                    PortType type = SystemAPI.GetComponent<Port>(inputPort).Type;
                    ref Anchor anchor = ref SystemAPI.GetComponentRW<Anchor>(nodeEntity).ValueRW;

                    if (type == PortType.Anchor) {
                        if (i == 0) {
                            anchor.Value = SystemAPI.GetComponent<AnchorPort>(inputPort);
                        }
                    }
                    else if (type == PortType.Path) {
                        // Handle in job
                    }
                    else if (type == PortType.Duration) {
                        float duration = SystemAPI.GetComponent<DurationPort>(inputPort);
                        ref var durationComponent = ref SystemAPI.GetComponentRW<Duration>(nodeEntity).ValueRW;
                        durationComponent.Value = duration;
                    }
                    else if (type == PortType.Position) {
                        float3 position = SystemAPI.GetComponent<PositionPort>(inputPort);
                        if (node.Type == NodeType.Mesh) {
                            anchor.Value.Position = position;
                        }
                        else {
                            anchor.Value.SetPosition(position);
                        }
                    }
                    else if (type == PortType.Roll) {
                        float roll = SystemAPI.GetComponent<RollPort>(inputPort);
                        anchor.Value.SetRoll(roll);
                    }
                    else if (type == PortType.Pitch) {
                        float pitch = SystemAPI.GetComponent<PitchPort>(inputPort);
                        anchor.Value.SetPitch(pitch);
                    }
                    else if (type == PortType.Yaw) {
                        float yaw = SystemAPI.GetComponent<YawPort>(inputPort);
                        anchor.Value.SetYaw(yaw);
                    }
                    else if (type == PortType.Velocity) {
                        float velocity = SystemAPI.GetComponent<VelocityPort>(inputPort);
                        anchor.Value.SetVelocity(velocity);
                    }
                    else if (type == PortType.Heart) {
                        float heart = SystemAPI.GetComponent<HeartPort>(inputPort);
                        anchor.Value.SetHeart(heart);
                    }
                    else if (type == PortType.Friction) {
                        float friction = SystemAPI.GetComponent<FrictionPort>(inputPort);
                        anchor.Value.SetFriction(friction);
                    }
                    else if (type == PortType.Resistance) {
                        float resistance = SystemAPI.GetComponent<ResistancePort>(inputPort);
                        anchor.Value.SetResistance(resistance);
                    }
                    else if (type == PortType.Radius) {
                        float radius = SystemAPI.GetComponent<RadiusPort>(inputPort);
                        ref var curveData = ref SystemAPI.GetComponentRW<CurveData>(nodeEntity).ValueRW;
                        curveData.Radius = radius;
                    }
                    else if (type == PortType.Arc) {
                        float arc = SystemAPI.GetComponent<ArcPort>(inputPort);
                        ref var curveData = ref SystemAPI.GetComponentRW<CurveData>(nodeEntity).ValueRW;
                        curveData.Arc = arc;
                    }
                    else if (type == PortType.Axis) {
                        float axis = SystemAPI.GetComponent<AxisPort>(inputPort);
                        ref var curveData = ref SystemAPI.GetComponentRW<CurveData>(nodeEntity).ValueRW;
                        curveData.Axis = axis;
                    }
                    else if (type == PortType.LeadIn) {
                        float leadIn = SystemAPI.GetComponent<LeadInPort>(inputPort);
                        ref var curveData = ref SystemAPI.GetComponentRW<CurveData>(nodeEntity).ValueRW;
                        curveData.LeadIn = leadIn;
                    }
                    else if (type == PortType.LeadOut) {
                        float leadOut = SystemAPI.GetComponent<LeadOutPort>(inputPort);
                        ref var curveData = ref SystemAPI.GetComponentRW<CurveData>(nodeEntity).ValueRW;
                        curveData.LeadOut = leadOut;
                    }
                    else if (type == PortType.Rotation) {
                        // Encode rotation into anchor
                        float3 rotation = SystemAPI.GetComponent<RotationPort>(inputPort);
                        anchor.Value.Roll = rotation.x;
                        anchor.Value.Velocity = rotation.y;
                        anchor.Value.Energy = rotation.z;
                    }
                    else if (type == PortType.Scale) {
                        // Encode scale into anchor
                        float scale = SystemAPI.GetComponent<ScalePort>(inputPort);
                        anchor.Value.NormalForce = scale;
                    }
                    else if (type == PortType.Start || type == PortType.End) {
                        // Start and End ports are read directly by BuildCopyPathSectionSystem
                    }
                    else {
                        throw new System.NotImplementedException($"Unknown input port type: {type}");
                    }

                    inputPortDirty = false;
                    node.Dirty = true;
                }
            }

            nodes.Dispose();
        }

        private void PropagateConnections(ref SystemState state) {
            var connections = _connectionQuery.ToComponentDataArray<Connection>(Allocator.Temp);
            var map = new NativeParallelMultiHashMap<Entity, Entity>(connections.Length, Allocator.Temp);
            foreach (var connection in connections) {
                map.Add(connection.Source, connection.Target);
            }
            connections.Dispose();

            var propagated = new NativeHashSet<Entity>(connections.Length, Allocator.Temp);
            var nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var nodeEntity in nodes) {
                var node = SystemAPI.GetAspect<NodeAspect>(nodeEntity);
                foreach (var sourcePort in node.OutputPorts) {
                    ref Dirty sourcePortDirty = ref SystemAPI.GetComponentRW<Dirty>(sourcePort).ValueRW;
                    if (!sourcePortDirty || !map.ContainsKey(sourcePort)) continue;

                    foreach (var targetPort in map.GetValuesForKey(sourcePort)) {
                        if (propagated.Contains(targetPort)) continue;
                        PropagateConnection(ref state, node, sourcePort, targetPort);
                        propagated.Add(targetPort);
                    }

                    sourcePortDirty = false;
                }
            }

            propagated.Dispose();
            map.Dispose();
            nodes.Dispose();
        }

        private void PropagateConnection(ref SystemState state, NodeAspect node, Entity sourcePort, Entity targetPort) {
            ref Dirty targetPortDirty = ref SystemAPI.GetComponentRW<Dirty>(targetPort).ValueRW;

            if (SystemAPI.HasComponent<AnchorPort>(sourcePort) && SystemAPI.HasComponent<AnchorPort>(targetPort)) {
                AnchorPort sourcePointPort = SystemAPI.GetComponent<AnchorPort>(sourcePort);
                ref AnchorPort targetPointPort = ref SystemAPI.GetComponentRW<AnchorPort>(targetPort).ValueRW;
                targetPointPort.Value = sourcePointPort.Value;
            }
            else if (SystemAPI.HasBuffer<PathPort>(sourcePort) && SystemAPI.HasBuffer<PathPort>(targetPort)) {
                var sourceBuffer = SystemAPI.GetBuffer<Point>(node.Self);
                var portBuffer = SystemAPI.GetBuffer<PathPort>(targetPort);
                portBuffer.Clear();
                foreach (var point in sourceBuffer) {
                    portBuffer.Add((PathPort)point.Value);
                }
            }
            else {
                UnityEngine.Debug.LogWarning("Unknown propagation");
            }

            targetPortDirty = true;
        }
    }
}
