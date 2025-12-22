using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Core.Articulation {
    [BurstCompile]
    public static class BodyPositioning {
        [BurstCompile]
        public static void FromAnchor(in Anchor anchor, out BodyTransform result) {
            result = new BodyTransform(anchor.Position, anchor.Rotation);
        }

        [BurstCompile]
        public static void FromAnchors(in Anchor leading, in Anchor trailing, float pivot, out BodyTransform result) {
            float3 toTrailing = trailing.Position - leading.Position;

            if (math.lengthsq(toTrailing) < 1e-6f) {
                FromAnchor(leading, out result);
                return;
            }

            float3 forward = math.normalize(toTrailing);

            float3 upLeading = -leading.Normal;
            float3 upTrailing = -trailing.Normal;
            float3 up = math.normalize(upLeading + upTrailing);

            float3 right = math.normalize(math.cross(up, forward));
            up = math.cross(forward, right);

            quaternion rotation = quaternion.LookRotation(forward, up);
            float3 position = math.lerp(leading.Position, trailing.Position, pivot);

            result = new BodyTransform(position, rotation);
        }

        [BurstCompile]
        public static void FromAnchors(in NativeArray<Anchor> anchors, float pivot, out BodyTransform result) {
            if (anchors.Length == 0) {
                result = BodyTransform.Identity;
                return;
            }
            if (anchors.Length == 1) {
                FromAnchor(anchors[0], out result);
                return;
            }
            if (anchors.Length == 2) {
                FromAnchors(anchors[0], anchors[1], pivot, out result);
                return;
            }

            Anchor first = anchors[0];
            Anchor last = anchors[^1];

            float3 toEnd = last.Position - first.Position;
            if (math.lengthsq(toEnd) < 1e-6f) {
                FromAnchor(first, out result);
                return;
            }

            float3 forward = math.normalize(toEnd);

            float3 upSum = float3.zero;
            for (int i = 0; i < anchors.Length; i++) {
                upSum -= anchors[i].Normal;
            }
            float3 up = math.normalize(upSum);

            float3 right = math.normalize(math.cross(up, forward));
            up = math.cross(forward, right);

            quaternion rotation = quaternion.LookRotation(forward, up);

            float totalLength = 0f;
            for (int i = 1; i < anchors.Length; i++) {
                totalLength += math.length(anchors[i].Position - anchors[i - 1].Position);
            }

            float targetLength = totalLength * pivot;
            float accumulated = 0f;
            float3 position = first.Position;

            for (int i = 1; i < anchors.Length; i++) {
                float3 segment = anchors[i].Position - anchors[i - 1].Position;
                float segmentLength = math.length(segment);

                if (accumulated + segmentLength >= targetLength) {
                    float t = segmentLength > 0f ? (targetLength - accumulated) / segmentLength : 0f;
                    position = math.lerp(anchors[i - 1].Position, anchors[i].Position, t);
                    break;
                }

                accumulated += segmentLength;
            }

            result = new BodyTransform(position, rotation);
        }

        [BurstCompile]
        public static void FromAnchors(in NativeArray<Anchor> anchors, int pivotIndex, out BodyTransform result) {
            if (anchors.Length == 0) {
                result = BodyTransform.Identity;
                return;
            }
            if (anchors.Length == 1) {
                FromAnchor(anchors[0], out result);
                return;
            }

            pivotIndex = math.clamp(pivotIndex, 0, anchors.Length - 1);
            Anchor pivotAnchor = anchors[pivotIndex];

            Anchor first = anchors[0];
            Anchor last = anchors[^1];

            float3 toEnd = last.Position - first.Position;
            if (math.lengthsq(toEnd) < 1e-6f) {
                FromAnchor(pivotAnchor, out result);
                return;
            }

            float3 forward = math.normalize(toEnd);

            float3 upSum = float3.zero;
            for (int i = 0; i < anchors.Length; i++) {
                upSum -= anchors[i].Normal;
            }
            float3 up = math.normalize(upSum);

            float3 right = math.normalize(math.cross(up, forward));
            up = math.cross(forward, right);

            quaternion rotation = quaternion.LookRotation(forward, up);

            result = new BodyTransform(pivotAnchor.Position, rotation);
        }
    }
}
