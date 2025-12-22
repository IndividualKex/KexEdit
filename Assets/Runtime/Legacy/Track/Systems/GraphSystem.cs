using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct GraphSystem : ISystem {
        private EntityQuery _nodeQuery;
        private EntityQuery _connectionQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            _nodeQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Node, InputPortReference, OutputPortReference>()
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
                var node = SystemAPI.GetComponent<Node>(nodeEntity);
                if (node.Type != NodeType.Anchor || !SystemAPI.IsComponentEnabled<Dirty>(nodeEntity)) continue;

                var outputPort = SystemAPI.GetBuffer<OutputPortReference>(nodeEntity)[0];

                ref var portRef = ref SystemAPI.GetComponentRW<AnchorPort>(outputPort).ValueRW;
                portRef.Value = SystemAPI.GetComponent<Anchor>(nodeEntity);

                SystemAPI.SetComponentEnabled<Dirty>(nodeEntity, false);
                SystemAPI.SetComponentEnabled<Dirty>(outputPort, true);
            }
            nodes.Dispose();
        }

        private void PropagateInputPorts(ref SystemState state) {
            var nodes = _nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var nodeEntity in nodes) {
                var node = SystemAPI.GetComponent<Node>(nodeEntity);
                var inputPorts = SystemAPI.GetBuffer<InputPortReference>(nodeEntity);

                for (int i = 0; i < inputPorts.Length; i++) {
                    var inputPort = inputPorts[i];
                    if (!SystemAPI.IsComponentEnabled<Dirty>(inputPort)) continue;
                    PortType type = SystemAPI.GetComponent<Port>(inputPort).Type;
                    ref var anchorRef = ref SystemAPI.GetComponentRW<Anchor>(nodeEntity).ValueRW;

                    if (type == PortType.Anchor) {
                        if (i == 0) {
                            anchorRef.Value = SystemAPI.GetComponent<AnchorPort>(inputPort);
                        }
                    }
                    else if (type == PortType.Path) {
                        // Handle in job
                    }
                    else if (type == PortType.Duration) {
                        float duration = SystemAPI.GetComponent<DurationPort>(inputPort);
                        ref var durationComponentRef = ref SystemAPI.GetComponentRW<Duration>(nodeEntity).ValueRW;
                        durationComponentRef.Value = duration;
                    }
                    else if (type == PortType.Position) {
                        float3 position = SystemAPI.GetComponent<PositionPort>(inputPort);
                        if (node.Type == NodeType.Mesh) {
                            anchorRef.Value.HeartPosition = position;
                        }
                        else {
                            anchorRef.Value.SetPosition(position);
                        }
                    }
                    else if (type == PortType.Roll) {
                        float roll = SystemAPI.GetComponent<RollPort>(inputPort);
                        anchorRef.Value.SetRoll(roll);
                    }
                    else if (type == PortType.Pitch) {
                        float pitch = SystemAPI.GetComponent<PitchPort>(inputPort);
                        anchorRef.Value.SetPitch(pitch);
                    }
                    else if (type == PortType.Yaw) {
                        float yaw = SystemAPI.GetComponent<YawPort>(inputPort);
                        anchorRef.Value.SetYaw(yaw);
                    }
                    else if (type == PortType.Velocity) {
                        float velocity = SystemAPI.GetComponent<VelocityPort>(inputPort);
                        anchorRef.Value.SetVelocity(velocity);
                    }
                    else if (type == PortType.Heart) {
                        float heart = SystemAPI.GetComponent<HeartPort>(inputPort);
                        anchorRef.Value.SetHeart(heart);
                    }
                    else if (type == PortType.Friction) {
                        float friction = SystemAPI.GetComponent<FrictionPort>(inputPort);
                        anchorRef.Value.SetFriction(friction);
                    }
                    else if (type == PortType.Resistance) {
                        float resistance = SystemAPI.GetComponent<ResistancePort>(inputPort);
                        anchorRef.Value.SetResistance(resistance);
                    }
                    else if (type == PortType.Radius) {
                        float radius = SystemAPI.GetComponent<RadiusPort>(inputPort);
                        ref var curveDataRef = ref SystemAPI.GetComponentRW<CurveData>(nodeEntity).ValueRW;
                        curveDataRef.Radius = radius;
                    }
                    else if (type == PortType.Arc) {
                        float arc = SystemAPI.GetComponent<ArcPort>(inputPort);
                        ref var curveDataRef = ref SystemAPI.GetComponentRW<CurveData>(nodeEntity).ValueRW;
                        curveDataRef.Arc = arc;
                    }
                    else if (type == PortType.Axis) {
                        float axis = SystemAPI.GetComponent<AxisPort>(inputPort);
                        ref var curveDataRef = ref SystemAPI.GetComponentRW<CurveData>(nodeEntity).ValueRW;
                        curveDataRef.Axis = axis;
                    }
                    else if (type == PortType.LeadIn) {
                        float leadIn = SystemAPI.GetComponent<LeadInPort>(inputPort);
                        ref var curveDataRef = ref SystemAPI.GetComponentRW<CurveData>(nodeEntity).ValueRW;
                        curveDataRef.LeadIn = leadIn;
                    }
                    else if (type == PortType.LeadOut) {
                        float leadOut = SystemAPI.GetComponent<LeadOutPort>(inputPort);
                        ref var curveDataRef = ref SystemAPI.GetComponentRW<CurveData>(nodeEntity).ValueRW;
                        curveDataRef.LeadOut = leadOut;
                    }
                    else if (type == PortType.InWeight || type == PortType.OutWeight) {
                        // Handle in job
                    }
                    else if (type == PortType.Rotation) {
                        // Encode rotation into anchor
                        float3 rotation = SystemAPI.GetComponent<RotationPort>(inputPort);
                        anchorRef.Value.Roll = rotation.x;
                        anchorRef.Value.Velocity = rotation.y;
                        anchorRef.Value.Energy = rotation.z;
                    }
                    else if (type == PortType.Scale) {
                        // Encode scale into anchor
                        float scale = SystemAPI.GetComponent<ScalePort>(inputPort);
                        anchorRef.Value.NormalForce = scale;
                    }
                    else if (type == PortType.Start || type == PortType.End) {
                        // Start and End ports are read directly by BuildCopyPathSectionSystem
                    }
                    else {
                        throw new System.NotImplementedException($"Unknown input port type: {type}");
                    }

                    SystemAPI.SetComponentEnabled<Dirty>(inputPort, false);
                    SystemAPI.SetComponentEnabled<Dirty>(nodeEntity, true);
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
                var outputPorts = SystemAPI.GetBuffer<OutputPortReference>(nodeEntity);
                foreach (var sourcePort in outputPorts) {
                    if (!SystemAPI.IsComponentEnabled<Dirty>(sourcePort) || !map.ContainsKey(sourcePort)) continue;

                    foreach (var targetPort in map.GetValuesForKey(sourcePort)) {
                        if (propagated.Contains(targetPort)) continue;
                        PropagateConnection(ref state, nodeEntity, sourcePort, targetPort);
                        propagated.Add(targetPort);
                    }

                    SystemAPI.SetComponentEnabled<Dirty>(sourcePort, false);
                }
            }

            propagated.Dispose();
            map.Dispose();
            nodes.Dispose();
        }

        private void PropagateConnection(ref SystemState state, Entity nodeEntity, Entity sourcePort, Entity targetPort) {
            if (SystemAPI.HasComponent<AnchorPort>(sourcePort) && SystemAPI.HasComponent<AnchorPort>(targetPort)) {
                AnchorPort sourcePointPort = SystemAPI.GetComponent<AnchorPort>(sourcePort);
                ref var targetPointPortRef = ref SystemAPI.GetComponentRW<AnchorPort>(targetPort).ValueRW;
                targetPointPortRef.Value = sourcePointPort.Value;
            }
            else if (SystemAPI.HasBuffer<PathPort>(sourcePort) && SystemAPI.HasBuffer<PathPort>(targetPort)) {
                var sourceBuffer = SystemAPI.GetBuffer<CorePointBuffer>(nodeEntity);
                var portBuffer = SystemAPI.GetBuffer<PathPort>(targetPort);
                portBuffer.Clear();
                foreach (var point in sourceBuffer) {
                    portBuffer.Add((PathPort)point.ToPointData());
                }
            }
            else {
                UnityEngine.Debug.LogWarning("Unknown propagation");
            }

            SystemAPI.SetComponentEnabled<Dirty>(targetPort, true);
        }
    }
}
