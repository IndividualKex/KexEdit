using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using KexEdit.Spline;
using CoreKeyframe = KexEdit.Sim.Keyframe;
using CorePoint = KexEdit.Sim.Point;

namespace KexEdit.Track {
    public static class RustTrack {
        private const string DLL_NAME = "kexengine";
        private const int INITIAL_POINTS_CAPACITY = 4096;
        private const int INITIAL_SECTIONS_CAPACITY = 256;
        private const int INITIAL_SPLINE_CAPACITY = 8192;

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct DocumentData {
            // Graph arrays
            public uint* NodeIds;
            public nuint NodeCount;
            public uint* NodeTypes;
            public int* NodeInputCounts;
            public int* NodeOutputCounts;

            public uint* PortIds;
            public nuint PortCount;
            public uint* PortTypes;
            public uint* PortOwners;
            public byte* PortIsInput;

            public uint* EdgeIds;
            public nuint EdgeCount;
            public uint* EdgeSources;
            public uint* EdgeTargets;

            // Property maps
            public ulong* ScalarKeys;
            public float* ScalarValues;
            public nuint ScalarCount;

            public ulong* VectorKeys;
            public float3* VectorValues;
            public nuint VectorCount;

            public ulong* FlagKeys;
            public int* FlagValues;
            public nuint FlagCount;

            // Keyframes
            public CoreKeyframe* Keyframes;
            public nuint KeyframeCount;
            public ulong* KeyframeRangeKeys;
            public int* KeyframeRangeStarts;
            public int* KeyframeRangeLengths;
            public nuint KeyframeRangeCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct TrackOutput {
            public CorePoint* Points;
            public nuint PointsCapacity;

            public Section* Sections;
            public nuint SectionsCapacity;

            public uint* SectionNodeIds;

            public int* TraversalOrder;
            public nuint TraversalCapacity;

            public SplinePoint* SplinePoints;
            public nuint SplineCapacity;
            public float* SplineVelocities;
            public float* SplineNormalForces;
            public float* SplineLateralForces;
            public float* SplineRollSpeeds;

            public nuint* PointsCount;
            public nuint* SectionsCount;
            public nuint* TraversalCount;
            public nuint* SplineCount;
        }

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kex_build(
            DocumentData* doc,
            float resolution,
            int defaultStyleIndex,
            TrackOutput* output
        );

        public static unsafe int Build(
            in Document.Document doc,
            Allocator allocator,
            float resolution,
            int defaultStyleIndex,
            out Track track
        ) {
            // Extract graph arrays
            var nodeIds = doc.Graph.NodeIds;
            var nodeTypes = doc.Graph.NodeTypes;
            var nodeInputCounts = doc.Graph.NodeInputCount;
            var nodeOutputCounts = doc.Graph.NodeOutputCount;
            var portIds = doc.Graph.PortIds;
            var portTypes = doc.Graph.PortTypes;
            var portOwners = doc.Graph.PortOwners;
            var portIsInput = doc.Graph.PortIsInput;
            var edgeIds = doc.Graph.EdgeIds;
            var edgeSources = doc.Graph.EdgeSources;
            var edgeTargets = doc.Graph.EdgeTargets;

            // Convert bool array to byte array for FFI
            var portIsInputBytes = new NativeArray<byte>(portIsInput.Length, Allocator.Temp);
            for (int i = 0; i < portIsInput.Length; i++) {
                portIsInputBytes[i] = portIsInput[i] ? (byte)1 : (byte)0;
            }

            // Extract scalar map to parallel arrays
            var scalarKeys = doc.Scalars.GetKeyArray(Allocator.Temp);
            var scalarValues = doc.Scalars.GetValueArray(Allocator.Temp);

            // Extract vector map to parallel arrays
            var vectorKeys = doc.Vectors.GetKeyArray(Allocator.Temp);
            var vectorValues = doc.Vectors.GetValueArray(Allocator.Temp);

            // Extract flag map to parallel arrays
            var flagKeys = doc.Flags.GetKeyArray(Allocator.Temp);
            var flagValues = doc.Flags.GetValueArray(Allocator.Temp);

            // Extract keyframe ranges to parallel arrays
            var keyframeRangeKeys = doc.Keyframes.Ranges.GetKeyArray(Allocator.Temp);
            var keyframeRangeValues = doc.Keyframes.Ranges.GetValueArray(Allocator.Temp);
            var keyframeRangeStarts = new NativeArray<int>(keyframeRangeKeys.Length, Allocator.Temp);
            var keyframeRangeLengths = new NativeArray<int>(keyframeRangeKeys.Length, Allocator.Temp);
            for (int i = 0; i < keyframeRangeKeys.Length; i++) {
                keyframeRangeStarts[i] = keyframeRangeValues[i].x;
                keyframeRangeLengths[i] = keyframeRangeValues[i].y;
            }

            // Allocate output buffers
            var points = new NativeList<CorePoint>(INITIAL_POINTS_CAPACITY, allocator);
            var sections = new NativeArray<Section>(INITIAL_SECTIONS_CAPACITY, allocator);
            var sectionNodeIds = new NativeArray<uint>(INITIAL_SECTIONS_CAPACITY, Allocator.Temp);
            var traversalOrder = new NativeArray<int>(INITIAL_SECTIONS_CAPACITY, allocator);
            var splinePoints = new NativeList<SplinePoint>(INITIAL_SPLINE_CAPACITY, allocator);
            var splineVelocities = new NativeList<float>(INITIAL_SPLINE_CAPACITY, allocator);
            var splineNormalForces = new NativeList<float>(INITIAL_SPLINE_CAPACITY, allocator);
            var splineLateralForces = new NativeList<float>(INITIAL_SPLINE_CAPACITY, allocator);
            var splineRollSpeeds = new NativeList<float>(INITIAL_SPLINE_CAPACITY, allocator);

            // Ensure capacity
            points.Capacity = INITIAL_POINTS_CAPACITY;
            splinePoints.Capacity = INITIAL_SPLINE_CAPACITY;
            splineVelocities.Capacity = INITIAL_SPLINE_CAPACITY;
            splineNormalForces.Capacity = INITIAL_SPLINE_CAPACITY;
            splineLateralForces.Capacity = INITIAL_SPLINE_CAPACITY;
            splineRollSpeeds.Capacity = INITIAL_SPLINE_CAPACITY;

            nuint pointsCount = 0;
            nuint sectionsCount = 0;
            nuint traversalCount = 0;
            nuint splineCount = 0;

            int returnCode;
            int retryCount = 0;
            const int MAX_RETRIES = 5;

            do {
                // Build DocumentData
                var docData = new DocumentData {
                    NodeIds = nodeIds.Length > 0 ? (uint*)nodeIds.GetUnsafeReadOnlyPtr() : null,
                    NodeCount = (nuint)nodeIds.Length,
                    NodeTypes = nodeTypes.Length > 0 ? (uint*)nodeTypes.GetUnsafeReadOnlyPtr() : null,
                    NodeInputCounts = nodeInputCounts.Length > 0 ? (int*)nodeInputCounts.GetUnsafeReadOnlyPtr() : null,
                    NodeOutputCounts = nodeOutputCounts.Length > 0 ? (int*)nodeOutputCounts.GetUnsafeReadOnlyPtr() : null,
                    PortIds = portIds.Length > 0 ? (uint*)portIds.GetUnsafeReadOnlyPtr() : null,
                    PortCount = (nuint)portIds.Length,
                    PortTypes = portTypes.Length > 0 ? (uint*)portTypes.GetUnsafeReadOnlyPtr() : null,
                    PortOwners = portOwners.Length > 0 ? (uint*)portOwners.GetUnsafeReadOnlyPtr() : null,
                    PortIsInput = portIsInputBytes.Length > 0 ? (byte*)portIsInputBytes.GetUnsafeReadOnlyPtr() : null,
                    EdgeIds = edgeIds.Length > 0 ? (uint*)edgeIds.GetUnsafeReadOnlyPtr() : null,
                    EdgeCount = (nuint)edgeIds.Length,
                    EdgeSources = edgeSources.Length > 0 ? (uint*)edgeSources.GetUnsafeReadOnlyPtr() : null,
                    EdgeTargets = edgeTargets.Length > 0 ? (uint*)edgeTargets.GetUnsafeReadOnlyPtr() : null,
                    ScalarKeys = scalarKeys.Length > 0 ? (ulong*)scalarKeys.GetUnsafeReadOnlyPtr() : null,
                    ScalarValues = scalarValues.Length > 0 ? (float*)scalarValues.GetUnsafeReadOnlyPtr() : null,
                    ScalarCount = (nuint)scalarKeys.Length,
                    VectorKeys = vectorKeys.Length > 0 ? (ulong*)vectorKeys.GetUnsafeReadOnlyPtr() : null,
                    VectorValues = vectorValues.Length > 0 ? (float3*)vectorValues.GetUnsafeReadOnlyPtr() : null,
                    VectorCount = (nuint)vectorKeys.Length,
                    FlagKeys = flagKeys.Length > 0 ? (ulong*)flagKeys.GetUnsafeReadOnlyPtr() : null,
                    FlagValues = flagValues.Length > 0 ? (int*)flagValues.GetUnsafeReadOnlyPtr() : null,
                    FlagCount = (nuint)flagKeys.Length,
                    Keyframes = doc.Keyframes.Keyframes.Length > 0 ? (CoreKeyframe*)doc.Keyframes.Keyframes.GetUnsafeReadOnlyPtr() : null,
                    KeyframeCount = (nuint)doc.Keyframes.Keyframes.Length,
                    KeyframeRangeKeys = keyframeRangeKeys.Length > 0 ? (ulong*)keyframeRangeKeys.GetUnsafeReadOnlyPtr() : null,
                    KeyframeRangeStarts = keyframeRangeStarts.Length > 0 ? (int*)keyframeRangeStarts.GetUnsafeReadOnlyPtr() : null,
                    KeyframeRangeLengths = keyframeRangeLengths.Length > 0 ? (int*)keyframeRangeLengths.GetUnsafeReadOnlyPtr() : null,
                    KeyframeRangeCount = (nuint)keyframeRangeKeys.Length,
                };

                // Build TrackOutput
                var output = new TrackOutput {
                    Points = (CorePoint*)points.GetUnsafePtr(),
                    PointsCapacity = (nuint)points.Capacity,
                    Sections = (Section*)sections.GetUnsafePtr(),
                    SectionsCapacity = (nuint)sections.Length,
                    SectionNodeIds = (uint*)sectionNodeIds.GetUnsafePtr(),
                    TraversalOrder = (int*)traversalOrder.GetUnsafePtr(),
                    TraversalCapacity = (nuint)traversalOrder.Length,
                    SplinePoints = (SplinePoint*)splinePoints.GetUnsafePtr(),
                    SplineCapacity = (nuint)splinePoints.Capacity,
                    SplineVelocities = (float*)splineVelocities.GetUnsafePtr(),
                    SplineNormalForces = (float*)splineNormalForces.GetUnsafePtr(),
                    SplineLateralForces = (float*)splineLateralForces.GetUnsafePtr(),
                    SplineRollSpeeds = (float*)splineRollSpeeds.GetUnsafePtr(),
                    PointsCount = &pointsCount,
                    SectionsCount = &sectionsCount,
                    TraversalCount = &traversalCount,
                    SplineCount = &splineCount,
                };

                returnCode = kex_build(&docData, resolution, defaultStyleIndex, &output);

                if (returnCode == -3) {
                    // Buffer overflow - grow buffers and retry
                    points.Capacity *= 2;

                    var newSections = new NativeArray<Section>(sections.Length * 2, allocator);
                    sections.Dispose();
                    sections = newSections;

                    var newSectionNodeIds = new NativeArray<uint>(sectionNodeIds.Length * 2, Allocator.Temp);
                    sectionNodeIds.Dispose();
                    sectionNodeIds = newSectionNodeIds;

                    var newTraversalOrder = new NativeArray<int>(traversalOrder.Length * 2, allocator);
                    traversalOrder.Dispose();
                    traversalOrder = newTraversalOrder;

                    splinePoints.Capacity *= 2;
                    splineVelocities.Capacity *= 2;
                    splineNormalForces.Capacity *= 2;
                    splineLateralForces.Capacity *= 2;
                    splineRollSpeeds.Capacity *= 2;

                    retryCount++;
                }
            } while (returnCode == -3 && retryCount < MAX_RETRIES);

            // Cleanup temp arrays
            portIsInputBytes.Dispose();
            scalarKeys.Dispose();
            scalarValues.Dispose();
            vectorKeys.Dispose();
            vectorValues.Dispose();
            flagKeys.Dispose();
            flagValues.Dispose();
            keyframeRangeKeys.Dispose();
            keyframeRangeValues.Dispose();
            keyframeRangeStarts.Dispose();
            keyframeRangeLengths.Dispose();

            if (returnCode != 0) {
                // Cleanup on error
                points.Dispose();
                sections.Dispose();
                sectionNodeIds.Dispose();
                traversalOrder.Dispose();
                splinePoints.Dispose();
                splineVelocities.Dispose();
                splineNormalForces.Dispose();
                splineLateralForces.Dispose();
                splineRollSpeeds.Dispose();

                track = default;
                return returnCode;
            }

            // Resize to actual counts
            points.ResizeUninitialized((int)pointsCount);
            splinePoints.ResizeUninitialized((int)splineCount);
            splineVelocities.ResizeUninitialized((int)splineCount);
            splineNormalForces.ResizeUninitialized((int)splineCount);
            splineLateralForces.ResizeUninitialized((int)splineCount);
            splineRollSpeeds.ResizeUninitialized((int)splineCount);

            // Create final sections array with correct size
            var finalSections = new NativeArray<Section>((int)sectionsCount, allocator);
            for (int i = 0; i < (int)sectionsCount; i++) {
                finalSections[i] = sections[i];
            }
            sections.Dispose();

            // Create final traversal order array with correct size
            var finalTraversalOrder = new NativeArray<int>((int)traversalCount, allocator);
            for (int i = 0; i < (int)traversalCount; i++) {
                finalTraversalOrder[i] = traversalOrder[i];
            }
            traversalOrder.Dispose();

            // Build NodeToSection map
            var nodeToSection = new NativeHashMap<uint, int>((int)sectionsCount, allocator);
            for (int i = 0; i < (int)sectionsCount; i++) {
                nodeToSection[sectionNodeIds[i]] = i;
            }
            sectionNodeIds.Dispose();

            track = new Track {
                Points = points,
                Sections = finalSections,
                NodeToSection = nodeToSection,
                TraversalOrder = finalTraversalOrder,
                SplinePoints = splinePoints,
                SplineVelocities = splineVelocities,
                SplineNormalForces = splineNormalForces,
                SplineLateralForces = splineLateralForces,
                SplineRollSpeeds = splineRollSpeeds,
            };

            return 0;
        }
    }
}
