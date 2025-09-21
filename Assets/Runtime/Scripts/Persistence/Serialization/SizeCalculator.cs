using System.Runtime.InteropServices;
using Unity.Collections;

namespace KexEdit.Serialization {
    public static class SizeCalculator {
        public static int CalculateSize(ref SerializedGraph graph) {
            int size = sizeof(int); // Version
            
            // UI State (12 floats)
            size += sizeof(float); // TimelineOffset
            size += sizeof(float); // TimelineZoom
            size += sizeof(float); // NodeGraphPanX
            size += sizeof(float); // NodeGraphPanY
            size += sizeof(float); // NodeGraphZoom
            size += sizeof(float); // CameraTargetPositionX
            size += sizeof(float); // CameraTargetPositionY
            size += sizeof(float); // CameraTargetPositionZ
            size += sizeof(float); // CameraTargetDistance
            size += sizeof(float); // CameraTargetPitch
            size += sizeof(float); // CameraTargetYaw
            size += sizeof(float); // CameraSpeedMultiplier
            
            size += sizeof(int); // Node count

            for (int i = 0; i < graph.Nodes.Length; i++) {
                size += CalculateNodeSize(ref graph, i);
            }

            size += sizeof(int); // Edge count
            size += graph.Edges.Length * Marshal.SizeOf<SerializedEdge>();

            return size;
        }

        private static int CalculateNodeSize(ref SerializedGraph graph, int index) {
            int size = Marshal.SizeOf<Node>();
            size += Marshal.SizeOf<PointData>();
            size += sizeof(uint); // Field flags

            var node = graph.Nodes[index];

            if ((node.FieldFlags & (NodeFieldFlags.HasRender | NodeFieldFlags.HasSelected | NodeFieldFlags.HasSteering)) != 0) {
                size += sizeof(byte);
            }
            if ((node.FieldFlags & NodeFieldFlags.HasPropertyOverrides) != 0) size += Marshal.SizeOf<PropertyOverrides>();
            if ((node.FieldFlags & NodeFieldFlags.HasSelectedProperties) != 0) size += Marshal.SizeOf<SelectedProperties>();
            if ((node.FieldFlags & NodeFieldFlags.HasCurveData) != 0) size += Marshal.SizeOf<CurveData>();
            if ((node.FieldFlags & NodeFieldFlags.HasDuration) != 0) size += Marshal.SizeOf<Duration>();
            if ((node.FieldFlags & NodeFieldFlags.HasMeshFilePath) != 0) size += Marshal.SizeOf<FixedString512Bytes>();

            // Arrays
            size += sizeof(int) + node.InputPorts.Length * Marshal.SizeOf<SerializedPort>();
            size += sizeof(int) + node.OutputPorts.Length * Marshal.SizeOf<SerializedPort>();
            size += sizeof(int) + node.RollSpeedKeyframes.Length * Marshal.SizeOf<RollSpeedKeyframe>();
            size += sizeof(int) + node.NormalForceKeyframes.Length * Marshal.SizeOf<NormalForceKeyframe>();
            size += sizeof(int) + node.LateralForceKeyframes.Length * Marshal.SizeOf<LateralForceKeyframe>();
            size += sizeof(int) + node.PitchSpeedKeyframes.Length * Marshal.SizeOf<PitchSpeedKeyframe>();
            size += sizeof(int) + node.YawSpeedKeyframes.Length * Marshal.SizeOf<YawSpeedKeyframe>();
            size += sizeof(int) + node.FixedVelocityKeyframes.Length * Marshal.SizeOf<FixedVelocityKeyframe>();
            size += sizeof(int) + node.HeartKeyframes.Length * Marshal.SizeOf<HeartKeyframe>();
            size += sizeof(int) + node.FrictionKeyframes.Length * Marshal.SizeOf<FrictionKeyframe>();
            size += sizeof(int) + node.ResistanceKeyframes.Length * Marshal.SizeOf<ResistanceKeyframe>();
            size += sizeof(int) + node.TrackStyleKeyframes.Length * Marshal.SizeOf<TrackStyleKeyframe>();

            return size;
        }
    }
}
