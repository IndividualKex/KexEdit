using KexEdit.Nodes.Reverse;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct BuildReverseSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new Job {
                Ecb = ecb.AsParallelWriter(),
                AnchorPortLookup = SystemAPI.GetComponentLookup<AnchorPort>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly]
            public ComponentLookup<AnchorPort> AnchorPortLookup;

            public void Execute(
                [ChunkIndexInQuery] int chunkIndex, 
                Entity entity, 
                EnabledRefRW<Dirty> dirty,
                ReverseAspect section
            ) {
                if (section.OutputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(section.OutputPorts[0], out var anchorPort)) {
                    PointData anchor = section.Anchor;
                    Core.Point anchorState = ToPoint(in anchor);
                    ReverseNode.Build(in anchorState, out Core.Point result);
                    anchorPort.Value = ToPointData(in result, in anchor);
                    anchorPort.Value.Facing *= -1;
                    Ecb.SetComponent(chunkIndex, section.OutputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildReverseSystem: No anchor port found");
                }

                foreach (var port in section.OutputPorts) {
                    Ecb.SetComponentEnabled<Dirty>(chunkIndex, port, true);
                }

                dirty.ValueRW = false;
            }

            private static Core.Point ToPoint(in PointData p) {
                return new Core.Point(
                    spinePosition: p.Position,
                    direction: p.Direction,
                    normal: p.Normal,
                    lateral: p.Lateral,
                    velocity: p.Velocity,
                    energy: p.Energy,
                    normalForce: p.NormalForce,
                    lateralForce: p.LateralForce,
                    heartArc: p.TotalLength,
                    spineArc: p.TotalHeartLength,
                    spineAdvance: p.HeartDistanceFromLast,
                    frictionOrigin: p.FrictionCompensation,
                    rollSpeed: p.RollSpeed,
                    heartOffset: p.Heart,
                    friction: p.Friction,
                    resistance: p.Resistance
                );
            }

            private static PointData ToPointData(in Core.Point s, in PointData prev) {
                float roll = math.degrees(math.atan2(s.Lateral.y, -s.Normal.y));
                roll = (roll + 540) % 360 - 180;

                return new PointData {
                    Position = s.SpinePosition,
                    Direction = s.Direction,
                    Lateral = s.Lateral,
                    Normal = s.Normal,
                    Roll = roll,
                    Velocity = s.Velocity,
                    Energy = s.Energy,
                    NormalForce = s.NormalForce,
                    LateralForce = s.LateralForce,
                    DistanceFromLast = prev.DistanceFromLast,
                    HeartDistanceFromLast = prev.HeartDistanceFromLast,
                    AngleFromLast = prev.AngleFromLast,
                    PitchFromLast = prev.PitchFromLast,
                    YawFromLast = prev.YawFromLast,
                    RollSpeed = s.RollSpeed,
                    TotalLength = s.HeartArc,
                    TotalHeartLength = s.SpineArc,
                    FrictionCompensation = s.FrictionOrigin,
                    Heart = s.HeartOffset,
                    Friction = s.Friction,
                    Resistance = s.Resistance,
                    Facing = prev.Facing,
                };
            }
        }
    }
}
