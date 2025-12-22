using KexEdit.Persistence;
using Unity.Mathematics;

namespace KexEdit.Legacy.Serialization {
    public static class ViewStateAdapter {
        public static void Capture(
            ref UIStateChunk chunk,
            in TimelineState timeline,
            in NodeGraphState nodeGraph,
            in CameraState camera
        ) {
            chunk.TimelineOffset = timeline.Offset;
            chunk.TimelineZoom = timeline.Zoom;
            chunk.PlayheadTime = timeline.PlayheadTime;
            chunk.GraphPanX = nodeGraph.Pan.x;
            chunk.GraphPanY = nodeGraph.Pan.y;
            chunk.GraphZoom = nodeGraph.Zoom;
            chunk.CameraPosition = camera.Position;
            chunk.CameraTargetPosition = camera.TargetPosition;
            chunk.CameraDistance = camera.Distance;
            chunk.CameraTargetDistance = camera.TargetDistance;
            chunk.CameraPitch = camera.Pitch;
            chunk.CameraTargetPitch = camera.TargetPitch;
            chunk.CameraYaw = camera.Yaw;
            chunk.CameraTargetYaw = camera.TargetYaw;
            chunk.CameraSpeedMultiplier = camera.SpeedMultiplier;
        }

        public static void Apply(
            in UIStateChunk chunk,
            ref TimelineState timeline,
            ref NodeGraphState nodeGraph,
            ref CameraState camera
        ) {
            timeline = new TimelineState {
                Offset = chunk.TimelineOffset,
                Zoom = chunk.TimelineZoom,
                PlayheadTime = chunk.PlayheadTime
            };

            nodeGraph = new NodeGraphState {
                Pan = new float2(chunk.GraphPanX, chunk.GraphPanY),
                Zoom = chunk.GraphZoom
            };

            camera = new CameraState {
                Position = chunk.CameraPosition,
                TargetPosition = chunk.CameraTargetPosition,
                Distance = chunk.CameraDistance,
                TargetDistance = chunk.CameraTargetDistance,
                Pitch = chunk.CameraPitch,
                TargetPitch = chunk.CameraTargetPitch,
                Yaw = chunk.CameraYaw,
                TargetYaw = chunk.CameraTargetYaw,
                SpeedMultiplier = chunk.CameraSpeedMultiplier,
                OrthographicSize = camera.OrthographicSize,
                TargetOrthographicSize = camera.TargetOrthographicSize,
                Orthographic = camera.Orthographic,
                TargetOrthographic = camera.TargetOrthographic
            };
        }
    }
}
