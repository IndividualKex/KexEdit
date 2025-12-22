using KexEdit.Sim;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Sim.Nodes.Curved {
    public static class CurvedPorts {
        public const int Anchor = 0;
        public const int Radius = 1;
        public const int Arc = 2;
        public const int Axis = 3;
        public const int LeadIn = 4;
        public const int LeadOut = 5;
    }

    [BurstCompile]
    public static class CurvedNode {
        private const int MAX_ITERATIONS = 1_000_000;

        [BurstCompile]
        public static void Build(
            in Point anchor,
            float radius,
            float arc,
            float axis,
            float leadIn,
            float leadOut,
            bool driven,
            in NativeArray<Keyframe> rollSpeed,
            in NativeArray<Keyframe> drivenVelocity,
            in NativeArray<Keyframe> heartOffset,
            in NativeArray<Keyframe> friction,
            in NativeArray<Keyframe> resistance,
            float anchorHeart,
            float anchorFriction,
            float anchorResistance,
            ref NativeList<Point> result
        ) {
            result.Clear();
            result.Add(anchor);

            Point state = anchor;
            float angle = 0f;
            float leadOutStartAngle = arc - leadOut;
            bool leadOutStarted = false;
            Point leadOutStartState = default;
            float actualLeadOut = 0f;

            float prevHeartOffset = anchorHeart;
            float prevFriction = anchorFriction;

            int iterations = 0;
            int index = 0;

            while (angle < arc - Sim.EPSILON) {
                if (iterations++ > MAX_ITERATIONS) break;

                Point prev = state;
                float t = index / Sim.HZ;

                float heartOffsetVal = KeyframeEvaluator.Evaluate(in heartOffset, t, anchorHeart);
                float frictionVal = KeyframeEvaluator.Evaluate(in friction, t, anchorFriction);
                float resistanceVal = KeyframeEvaluator.Evaluate(in resistance, t, anchorResistance);

                if (driven) {
                    float velocity = KeyframeEvaluator.Evaluate(in drivenVelocity, t, prev.Velocity);
                    if (velocity < Sim.MIN_VELOCITY) break;
                    prev = prev.WithVelocity(velocity, heartOffsetVal, frictionVal, true);
                }
                else if (prev.Velocity < Sim.MIN_VELOCITY) {
                    if (prev.Frame.Pitch < 0f) {
                        prev = prev.WithVelocity(Sim.MIN_VELOCITY, heartOffsetVal, frictionVal, true);
                    }
                    else break;
                }

                float deltaAngle = prev.Velocity / radius / Sim.HZ * math.degrees(1f);

                if (leadIn > 0f) {
                    float distanceFromStart = prev.HeartArc - anchor.HeartArc;
                    float expectedLeadInDistance = 1.997f / Sim.HZ * prev.Velocity / deltaAngle * leadIn;
                    float fTrans = distanceFromStart / expectedLeadInDistance;
                    if (fTrans <= 1f) {
                        float dampening = fTrans * fTrans * (3f + fTrans * (-2f));
                        deltaAngle *= dampening;
                    }
                }

                if (!leadOutStarted && angle > leadOutStartAngle) {
                    leadOutStarted = true;
                    leadOutStartState = prev;
                    actualLeadOut = arc - angle;
                }

                if (leadOutStarted && leadOut > 0f) {
                    float distanceFromLeadOutStart = prev.HeartArc - leadOutStartState.HeartArc;
                    float expectedLeadOutDistance = 1.997f / Sim.HZ * prev.Velocity / deltaAngle * actualLeadOut;
                    float fTrans = 1f - distanceFromLeadOutStart / expectedLeadOutDistance;
                    if (fTrans >= 0f) {
                        float dampening = fTrans * fTrans * (3f + fTrans * (-2f));
                        deltaAngle *= dampening;
                    }
                    else break;
                }

                angle += deltaAngle;
                float rollSpeedVal = KeyframeEvaluator.Evaluate(in rollSpeed, angle, 0f);

                StepCurved(
                    in prev, axis, deltaAngle, rollSpeedVal,
                    heartOffsetVal, frictionVal, resistanceVal, driven,
                    out Point curr
                );

                if (curr.Velocity > Sim.MAX_VELOCITY) break;
                float forceMag = math.sqrt(curr.NormalForce * curr.NormalForce + curr.LateralForce * curr.LateralForce);
                if (forceMag > Sim.MAX_FORCE) break;

                result.Add(curr);
                state = curr;
                prevHeartOffset = heartOffsetVal;
                prevFriction = frictionVal;
                index++;
            }
        }

        [BurstCompile]
        private static void StepCurved(
            in Point prev,
            float axis,
            float deltaAngle,
            float rollSpeedVal,
            float heartOffsetVal,
            float frictionVal,
            float resistanceVal,
            bool driven,
            out Point result
        ) {
            Frame prevFrame = prev.Frame;

            float axisRad = math.radians(axis);
            float3 curveAxis = -math.cos(axisRad) * prev.Normal + math.sin(axisRad) * prev.Lateral;
            quaternion curveQuat = quaternion.AxisAngle(curveAxis, math.radians(deltaAngle));
            float3 currDirection = math.normalize(math.mul(curveQuat, prev.Direction));

            float originalRoll = math.degrees(math.atan2(prev.Lateral.y, -prev.Normal.y));
            originalRoll = (originalRoll + 540f) % 360f - 180f;

            float3 currLateral = math.normalize(math.cross(currDirection, math.up()));
            if (math.length(currLateral) < Sim.EPSILON) {
                currLateral = math.right();
            }
            float3 currNormal = math.normalize(math.cross(currDirection, currLateral));

            if (math.abs(originalRoll) > Sim.EPSILON) {
                quaternion preservedRollQuat = quaternion.AxisAngle(currDirection, math.radians(-originalRoll));
                currLateral = math.normalize(math.mul(preservedRollQuat, currLateral));
                currNormal = math.normalize(math.cross(currDirection, currLateral));
            }

            float halfStepDistance = prev.Velocity / (2f * Sim.HZ);
            float3 prevSpinePos = prev.SpinePosition(heartOffsetVal);
            float3 currSpinePosIfHeartStatic = prev.HeartPosition + currNormal * heartOffsetVal;

            float3 currHeartPosition = prev.HeartPosition
                + currDirection * halfStepDistance
                + prev.Direction * halfStepDistance
                + (prevSpinePos - currSpinePosIfHeartStatic);

            float deltaRoll = rollSpeedVal / Sim.HZ;
            quaternion rollQuat = quaternion.AxisAngle(currDirection, -deltaRoll);
            currLateral = math.normalize(math.mul(rollQuat, currLateral));
            currNormal = math.normalize(math.cross(currDirection, currLateral));

            float spineAdvance = math.distance(
                currHeartPosition + currNormal * heartOffsetVal,
                prev.SpinePosition(heartOffsetVal)
            );
            float heartAdvance = math.distance(currHeartPosition, prev.HeartPosition);
            float newHeartArc = prev.HeartArc + heartAdvance;
            float newSpineArc = prev.SpineArc + spineAdvance;

            // Use delta-based velocity update for numerical stability
            float newVelocity = prev.Velocity;
            if (!driven) {
                float prevCenterY = (prev.HeartPosition + 0.9f * heartOffsetVal * prev.Normal).y;
                float currCenterY = (currHeartPosition + 0.9f * heartOffsetVal * currNormal).y;
                float deltaY = currCenterY - prevCenterY;
                newVelocity = Sim.UpdateVelocity(
                    prev.Velocity,
                    deltaY,
                    spineAdvance,
                    frictionVal,
                    resistanceVal
                );
            }

            Frame currFrame = new(currDirection, currNormal, currLateral);
            Curvature curvature = Curvature.FromFrames(in currFrame, in prevFrame);
            Forces forces = Forces.Compute(in curvature, in currFrame, newVelocity, heartAdvance);

            result = new Point(
                heartPosition: currHeartPosition,
                direction: currDirection,
                normal: currNormal,
                lateral: currLateral,
                velocity: newVelocity,
                normalForce: forces.Normal,
                lateralForce: forces.Lateral,
                heartArc: newHeartArc,
                spineArc: newSpineArc,
                heartAdvance: heartAdvance,
                frictionOrigin: prev.FrictionOrigin,
                rollSpeed: rollSpeedVal,
                heartOffset: heartOffsetVal,
                friction: frictionVal,
                resistance: resistanceVal
            );
        }
    }
}
