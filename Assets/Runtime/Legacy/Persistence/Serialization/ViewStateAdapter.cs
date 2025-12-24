using KexEdit.Persistence;
using Unity.Mathematics;

namespace KexEdit.Legacy.Serialization {
    public static class ViewStateAdapter {
        public static ViewStateChunk Capture(
            in TimelineState timeline,
            in NodeGraphState nodeGraph,
            in CameraState camera
        ) {
            return new ViewStateChunk {
                TimelineOffset = timeline.Offset,
                TimelineZoom = timeline.Zoom,
                GraphPanX = nodeGraph.Pan.x,
                GraphPanY = nodeGraph.Pan.y,
                GraphZoom = nodeGraph.Zoom,
                CameraPosition = camera.Position,
                CameraTargetPosition = camera.TargetPosition,
                CameraDistance = camera.Distance,
                CameraTargetDistance = camera.TargetDistance,
                CameraPitch = camera.Pitch,
                CameraTargetPitch = camera.TargetPitch,
                CameraYaw = camera.Yaw,
                CameraTargetYaw = camera.TargetYaw,
                CameraSpeedMultiplier = camera.SpeedMultiplier
            };
        }

        public static void Apply(
            in ViewStateChunk chunk,
            ref TimelineState timeline,
            ref NodeGraphState nodeGraph,
            ref CameraState camera
        ) {
            timeline = new TimelineState {
                Offset = chunk.TimelineOffset,
                Zoom = chunk.TimelineZoom
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
