using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace KexEdit.Spline.Rendering {
    [BurstCompile]
    public static class Deform {
        [BurstCompile]
        public static float Arc(float localZ, float startArc, float segmentLength, float nominalLength) {
            float t = localZ / nominalLength;
            return startArc + t * segmentLength;
        }

        [BurstCompile]
        public static void Position(
            in float3 localVertex,
            in NativeArray<SplinePoint> spline,
            float startArc,
            float segmentLength,
            float nominalLength,
            out float3 worldPosition
        ) {
            float arc = Arc(localVertex.z, startArc, segmentLength, nominalLength);
            SplineInterpolation.Interpolate(spline, arc, out SplinePoint frame);

            worldPosition = frame.Position
                + frame.Lateral * localVertex.x
                - frame.Normal * localVertex.y;
        }

        [BurstCompile]
        public static void Normal(
            in float3 localNormal,
            in NativeArray<SplinePoint> spline,
            float arc,
            out float3 worldNormal
        ) {
            SplineInterpolation.Interpolate(spline, arc, out SplinePoint frame);

            worldNormal = math.normalize(
                frame.Lateral * localNormal.x -
                frame.Normal * localNormal.y +
                frame.Direction * localNormal.z
            );
        }

        [BurstCompile]
        public static void Vertex(
            in float3 localPosition,
            in float3 localNormal,
            in NativeArray<SplinePoint> spline,
            float startArc,
            float segmentLength,
            float nominalLength,
            out float3 worldPosition,
            out float3 worldNormal
        ) {
            float arc = Arc(localPosition.z, startArc, segmentLength, nominalLength);
            SplineInterpolation.Interpolate(spline, arc, out SplinePoint frame);

            worldPosition = frame.Position
                + frame.Lateral * localPosition.x
                - frame.Normal * localPosition.y;

            worldNormal = math.normalize(
                frame.Lateral * localNormal.x -
                frame.Normal * localNormal.y +
                frame.Direction * localNormal.z
            );
        }

        [BurstCompile]
        public static void Mesh(
            in NativeArray<float3> vertices,
            in NativeArray<float3> normals,
            in NativeArray<SplinePoint> spline,
            float startArc,
            float segmentLength,
            float nominalLength,
            ref NativeArray<float3> outputPositions,
            ref NativeArray<float3> outputNormals
        ) {
            for (int i = 0; i < vertices.Length; i++) {
                Vertex(
                    vertices[i],
                    normals[i],
                    spline,
                    startArc,
                    segmentLength,
                    nominalLength,
                    out float3 worldPos,
                    out float3 worldNormal
                );
                outputPositions[i] = worldPos;
                outputNormals[i] = worldNormal;
            }
        }
    }
}
