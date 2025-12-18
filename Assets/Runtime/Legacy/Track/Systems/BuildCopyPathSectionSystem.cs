using KexEdit.Nodes.CopyPath;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static KexEdit.Legacy.Constants;
using CoreKeyframe = KexEdit.Core.Keyframe;
using CorePoint = KexEdit.Core.Point;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BuildCopyPathSectionSystem : ISystem {
        public void OnUpdate(ref SystemState state) {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new Job {
                Ecb = ecb.AsParallelWriter(),
                AnchorPortLookup = SystemAPI.GetComponentLookup<AnchorPort>(true),
                StartPortLookup = SystemAPI.GetComponentLookup<StartPort>(true),
                EndPortLookup = SystemAPI.GetComponentLookup<EndPort>(true),
                PathPortLookup = SystemAPI.GetBufferLookup<PathPort>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(CopyPathSectionTag))]
        private partial struct Job : IJobEntity {
            public EntityCommandBuffer.ParallelWriter Ecb;

            [ReadOnly]
            public ComponentLookup<AnchorPort> AnchorPortLookup;

            [ReadOnly]
            public ComponentLookup<StartPort> StartPortLookup;

            [ReadOnly]
            public ComponentLookup<EndPort> EndPortLookup;

            [ReadOnly]
            public BufferLookup<PathPort> PathPortLookup;

            public void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                Entity entity,
                EnabledRefRW<Dirty> dirty,
                in Anchor anchor,
                in PropertyOverrides propertyOverrides,
                in DynamicBuffer<FixedVelocityKeyframe> fixedVelocityKeyframes,
                in DynamicBuffer<HeartKeyframe> heartKeyframes,
                in DynamicBuffer<FrictionKeyframe> frictionKeyframes,
                in DynamicBuffer<ResistanceKeyframe> resistanceKeyframes,
                in DynamicBuffer<InputPortReference> inputPorts,
                in DynamicBuffer<OutputPortReference> outputPorts,
                ref DynamicBuffer<Point> points
            ) {
                if (inputPorts.Length != 4) {
                    UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: Expected 4 input ports (anchor, path, start, end)");
                    return;
                }

                if (!PathPortLookup.TryGetBuffer(inputPorts[1], out var pathBuffer)) {
                    UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: No path port found");
                    return;
                }

                if (!StartPortLookup.TryGetComponent(inputPorts[2], out var startPort)) {
                    UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: No start port found");
                    return;
                }

                if (!EndPortLookup.TryGetComponent(inputPorts[3], out var endPort)) {
                    UnityEngine.Debug.LogError("BuildCopyPathSectionSystem: No end port found");
                    return;
                }

                if (pathBuffer.Length < 2) return;

                CorePoint anchorState = ToPoint(in anchor.Value);

                float startTime = startPort.Value;
                float endTime = endPort.Value;

                using var sourcePath = ConvertPathBuffer(ref pathBuffer, Allocator.Temp);
                using var drivenVelocityKf = ConvertKeyframes(fixedVelocityKeyframes, Allocator.Temp);
                using var heartOffsetKf = ConvertKeyframes(heartKeyframes, Allocator.Temp);
                using var frictionKf = ConvertKeyframes(frictionKeyframes, Allocator.Temp);
                using var resistanceKf = ConvertKeyframes(resistanceKeyframes, Allocator.Temp);

                var result = new NativeList<CorePoint>(Allocator.Temp);

                CopyPathNode.Build(
                    in anchorState,
                    sourcePath,
                    startTime,
                    endTime,
                    propertyOverrides.FixedVelocity,
                    drivenVelocityKf,
                    heartOffsetKf,
                    frictionKf,
                    resistanceKf,
                    anchor.Value.Heart,
                    anchor.Value.Friction,
                    anchor.Value.Resistance,
                    ref result
                );

                points.Clear();
                points.Add(anchor.Value);
                PointData prev = anchor;
                for (int i = 1; i < result.Length; i++) {
                    PointData curr = ToPointData(in result.ElementAt(i), in prev);
                    points.Add(curr);
                    prev = curr;
                }
                result.Dispose();

                if (outputPorts.Length > 0 && AnchorPortLookup.TryGetComponent(outputPorts[0], out var anchorPort)) {
                    anchorPort.Value = points[^1].Value;
                    Ecb.SetComponent(chunkIndex, outputPorts[0], anchorPort);
                }
                else {
                    UnityEngine.Debug.LogWarning("BuildCopyPathSectionSystem: No anchor port found");
                }

                foreach (var port in outputPorts) {
                    Ecb.SetComponentEnabled<Dirty>(chunkIndex, port, true);
                }

                dirty.ValueRW = false;
            }

            private static CorePoint ToPoint(in PointData p) {
                return new CorePoint(
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

            private static PointData ToPointData(in CorePoint p, in PointData prev) {
                float roll = math.degrees(math.atan2(p.Lateral.y, -p.Normal.y));
                roll = (roll + 540) % 360 - 180;

                float pitch = GetPitch(p.Direction);
                float yaw = GetYaw(p.Direction);
                float prevPitch = GetPitch(prev.Direction);
                float prevYaw = GetYaw(prev.Direction);

                float pitchFromLast = 0f;
                float yawFromLast = 0f;
                float3 diff = p.Direction - prev.Direction;
                if (math.length(diff) >= EPSILON) {
                    pitchFromLast = (pitch - prevPitch + 540) % 360 - 180;
                    yawFromLast = (yaw - prevYaw + 540) % 360 - 180;
                }

                float yawScaleFactor = math.cos(math.abs(math.radians(pitch)));
                float angleFromLast = math.sqrt(
                    yawScaleFactor * yawScaleFactor * yawFromLast * yawFromLast
                    + pitchFromLast * pitchFromLast
                );

                float heartDistanceFromLast = math.distance(p.SpinePosition, prev.Position);
                float distanceFromLast = math.distance(
                    p.SpinePosition + p.Normal * p.HeartOffset,
                    prev.Position + prev.Normal * prev.Heart
                );

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
                    DistanceFromLast = distanceFromLast,
                    HeartDistanceFromLast = heartDistanceFromLast,
                    AngleFromLast = angleFromLast,
                    PitchFromLast = pitchFromLast,
                    YawFromLast = yawFromLast,
                    RollSpeed = p.RollSpeed,
                    TotalLength = p.HeartArc,
                    TotalHeartLength = p.SpineArc,
                    FrictionCompensation = p.FrictionOrigin,
                    Heart = p.HeartOffset,
                    Friction = p.Friction,
                    Resistance = p.Resistance,
                    Facing = prev.Facing,
                };
            }

            private static float GetPitch(float3 direction) {
                float magnitude = math.sqrt(direction.x * direction.x + direction.z * direction.z);
                return math.degrees(math.atan2(direction.y, magnitude));
            }

            private static float GetYaw(float3 direction) {
                return math.degrees(math.atan2(-direction.x, -direction.z));
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

            private static NativeArray<CoreKeyframe> ConvertKeyframes<T>(DynamicBuffer<T> buffer, Allocator allocator)
                where T : unmanaged, IBufferElementData {
                var result = new NativeArray<CoreKeyframe>(buffer.Length, allocator);
                for (int i = 0; i < buffer.Length; i++) {
                    var legacy = buffer.Reinterpret<Keyframe>()[i];
                    result[i] = new CoreKeyframe(
                        legacy.Time,
                        legacy.Value,
                        (Core.InterpolationType)(int)legacy.InInterpolation,
                        (Core.InterpolationType)(int)legacy.OutInterpolation,
                        legacy.InTangent,
                        legacy.OutTangent,
                        legacy.InWeight,
                        legacy.OutWeight
                    );
                }
                return result;
            }
        }
    }
}
