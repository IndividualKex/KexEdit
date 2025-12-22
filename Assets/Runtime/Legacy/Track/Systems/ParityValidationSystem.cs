#if VALIDATE_COASTER_PARITY
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct ParityValidationSystem : ISystem {
        const float ToleranceTight = 1e-5f;
        const float ToleranceAccumulated = 1e-3f;
        const float ToleranceHighlyAccumulated = 1e-2f;

        private bool _validated;

        public void OnCreate(ref SystemState state) {
            _validated = false;
        }

        public void OnUpdate(ref SystemState state) {
            if (_validated) return;

            int totalEntities = 0;
            int passedEntities = 0;
            int failedEntities = 0;
            int pointCountMismatches = 0;
            int valueMismatches = 0;

            var failedNodes = new NativeList<uint>(Allocator.Temp);

            foreach (var (node, corePoints, coasterPoints, entity) in
                     SystemAPI.Query<RefRO<Node>, DynamicBuffer<CorePointBuffer>, DynamicBuffer<CoasterPointBuffer>>()
                         .WithEntityAccess()) {
                totalEntities++;

                if (corePoints.Length == 0 && coasterPoints.Length == 0) {
                    passedEntities++;
                    continue;
                }

                if (corePoints.Length != coasterPoints.Length) {
                    failedEntities++;
                    pointCountMismatches++;
                    failedNodes.Add(node.ValueRO.Id);
                    UnityEngine.Debug.LogWarning(
                        $"[Parity] Node {node.ValueRO.Id}: count mismatch ECS={corePoints.Length} Coaster={coasterPoints.Length}");
                    continue;
                }

                int nodeMismatches = CountMismatches(corePoints, coasterPoints);
                if (nodeMismatches > 0) {
                    failedEntities++;
                    valueMismatches += nodeMismatches;
                    failedNodes.Add(node.ValueRO.Id);
                } else {
                    passedEntities++;
                }
            }

            if (totalEntities > 0) {
                if (failedEntities > 0) {
                    var nodeList = string.Join(", ", failedNodes.AsArray());
                    UnityEngine.Debug.LogError(
                        $"[Parity] FAILED: {failedEntities}/{totalEntities} nodes " +
                        $"({pointCountMismatches} count, {valueMismatches} value mismatches) " +
                        $"nodes=[{nodeList}]");
                } else {
                    UnityEngine.Debug.Log($"[Parity] PASSED: {passedEntities}/{totalEntities} nodes validated");
                }
            }

            failedNodes.Dispose();
            _validated = true;
        }

        static int CountMismatches(
            DynamicBuffer<CorePointBuffer> corePoints,
            DynamicBuffer<CoasterPointBuffer> coasterPoints
        ) {
            int mismatches = 0;

            for (int i = 0; i < corePoints.Length; i++) {
                var ecs = corePoints[i].Point;
                var coa = coasterPoints[i].Point;

                // Tight tolerance for spatial values
                if (!Float3Near(ecs.HeartPosition, coa.HeartPosition, ToleranceTight)) mismatches++;
                if (!Float3Near(ecs.Direction, coa.Direction, ToleranceTight)) mismatches++;
                if (!Float3Near(ecs.Lateral, coa.Lateral, ToleranceTight)) mismatches++;
                if (!Float3Near(ecs.Normal, coa.Normal, ToleranceTight)) mismatches++;

                // Accumulated tolerance for physics values
                if (!FloatNear(ecs.Velocity, coa.Velocity, ToleranceAccumulated)) mismatches++;
                if (!FloatNear(ecs.Energy, coa.Energy, ToleranceAccumulated)) mismatches++;
                if (!FloatNear(ecs.NormalForce, coa.NormalForce, ToleranceAccumulated)) mismatches++;
                if (!FloatNear(ecs.LateralForce, coa.LateralForce, ToleranceAccumulated)) mismatches++;

                // Highly accumulated tolerance for arc values
                if (!FloatNear(ecs.HeartArc, coa.HeartArc, ToleranceHighlyAccumulated)) mismatches++;
                if (!FloatNear(ecs.SpineArc, coa.SpineArc, ToleranceHighlyAccumulated)) mismatches++;
                if (!FloatNear(ecs.HeartAdvance, coa.HeartAdvance, ToleranceTight)) mismatches++;
                if (!FloatNear(ecs.FrictionOrigin, coa.FrictionOrigin, ToleranceAccumulated)) mismatches++;

                // Tight for per-step values
                if (!FloatNear(ecs.RollSpeed, coa.RollSpeed, ToleranceTight)) mismatches++;
                if (!FloatNear(ecs.HeartOffset, coa.HeartOffset, ToleranceTight)) mismatches++;
                if (!FloatNear(ecs.Friction, coa.Friction, ToleranceTight)) mismatches++;
                if (!FloatNear(ecs.Resistance, coa.Resistance, ToleranceTight)) mismatches++;
            }

            return mismatches;
        }

        static bool FloatNear(float a, float b, float tolerance) {
            return math.abs(a - b) <= tolerance;
        }

        static bool Float3Near(float3 a, float3 b, float tolerance) {
            return FloatNear(a.x, b.x, tolerance) &&
                   FloatNear(a.y, b.y, tolerance) &&
                   FloatNear(a.z, b.z, tolerance);
        }
    }
}
#endif
