using KexEdit.Nodes.ReversePath;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using CorePoint = KexEdit.Core.Point;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct BuildReversePathSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new Job {
                Ecb = ecb.AsParallelWriter(),
                PathPortLookup = SystemAPI.GetBufferLookup<PathPort>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct Job : IJobEntity {
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly]
            public BufferLookup<PathPort> PathPortLookup;

            public void Execute(
                [ChunkIndexInQuery] int chunkIndex, 
                Entity entity, 
                EnabledRefRW<Dirty> dirty,
                ReversePathAspect section
            ) {
                if (section.InputPorts.Length < 1
                    || !PathPortLookup.TryGetBuffer(section.InputPorts[0], out var pathBuffer)) {
                    UnityEngine.Debug.LogError("BuildReversePathSystem: No path port found");
                    return;
                }

                if (pathBuffer.Length < 2) return;

                using var sourcePath = ConvertPathBuffer(ref pathBuffer, Allocator.Temp);
                var result = new NativeList<CorePoint>(Allocator.Temp);

                ReversePathNode.Build(sourcePath, ref result);

                section.Points.Clear();
                float totalLength = pathBuffer[^1].Value.TotalLength;
                PointData firstAnchor = pathBuffer[^1].Value;
                for (int i = 0; i < result.Length; i++) {
                    PointData curr = ToPointData(in result.ElementAt(i), in firstAnchor);
                    curr.TotalLength = totalLength - sourcePath[sourcePath.Length - 1 - i].HeartArc;
                    section.Points.Add(curr);
                }

                result.Dispose();

                foreach (var port in section.OutputPorts) {
                    Ecb.SetComponentEnabled<Dirty>(chunkIndex, port, true);
                }

                dirty.ValueRW = false;
            }

            private static NativeArray<CorePoint> ConvertPathBuffer(ref DynamicBuffer<PathPort> buffer, Allocator allocator) {
                var result = new NativeArray<CorePoint>(buffer.Length, allocator);
                for (int i = 0; i < buffer.Length; i++) {
                    PointData p = buffer[i].Value;
                    result[i] = new CorePoint(
                        direction: p.Direction,
                        lateral: p.Lateral,
                        normal: p.Normal,
                        spinePosition: p.Position,
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
                return result;
            }

            private static PointData ToPointData(in CorePoint p, in PointData anchor) {
                float roll = math.degrees(math.atan2(p.Lateral.y, -p.Normal.y));
                roll = (roll + 540) % 360 - 180;

                return new PointData {
                    Position = p.SpinePosition,
                    Direction = p.Direction,
                    Lateral = p.Lateral,
                    Normal = p.Normal,
                    Roll = roll,
                    Velocity = p.Velocity,
                    Energy = p.Energy,
                    NormalForce = p.NormalForce,
                    LateralForce = p.LateralForce,
                    DistanceFromLast = 0f,
                    HeartDistanceFromLast = 0f,
                    AngleFromLast = 0f,
                    PitchFromLast = 0f,
                    YawFromLast = 0f,
                    RollSpeed = p.RollSpeed,
                    TotalLength = p.HeartArc,
                    TotalHeartLength = p.SpineArc,
                    FrictionCompensation = 0f,
                    Heart = p.HeartOffset,
                    Friction = p.Friction,
                    Resistance = p.Resistance,
                    Facing = anchor.Facing,
                };
            }
        }
    }
}
