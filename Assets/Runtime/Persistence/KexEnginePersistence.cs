using System;
using System.Runtime.InteropServices;
using KexEdit.Graph;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using CoreKeyframe = KexEdit.Sim.Keyframe;
using DocumentData = KexEdit.Document.Document;

namespace KexEdit.Persistence {
    public static class KexEnginePersistence {
        private const string DLL_NAME = "kexengine";

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct FfiDocument {
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

            public ulong* ScalarKeys;
            public float* ScalarValues;
            public nuint ScalarCount;

            public ulong* VectorKeys;
            public float3* VectorValues;
            public nuint VectorCount;

            public ulong* FlagKeys;
            public int* FlagValues;
            public nuint FlagCount;

            public CoreKeyframe* Keyframes;
            public nuint KeyframeCount;
            public ulong* KeyframeRangeKeys;
            public int* KeyframeRangeStarts;
            public int* KeyframeRangeLengths;
            public nuint KeyframeRangeCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DocumentCounts {
            public int NodeCount;
            public int PortCount;
            public int EdgeCount;
            public int ScalarCount;
            public int VectorCount;
            public int FlagCount;
            public int KeyframeCount;
            public int KeyframeRangeCount;
            public uint NextNodeId;
            public uint NextPortId;
            public uint NextEdgeId;
        }

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern long kex_save_size(FfiDocument* doc);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kex_save(
            FfiDocument* doc,
            byte* buffer,
            nuint bufferCapacity,
            nuint* bytesWritten
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern IntPtr kex_load(byte* data, nuint dataLen);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void kex_load_free(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kex_load_get_counts(IntPtr handle, DocumentCounts* counts);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kex_load_copy_data(
            IntPtr handle,
            uint* nodeIds, uint* nodeTypes, int* nodeInputCounts, int* nodeOutputCounts,
            uint* portIds, uint* portTypes, uint* portOwners, byte* portIsInput,
            uint* edgeIds, uint* edgeSources, uint* edgeTargets,
            ulong* scalarKeys, float* scalarValues,
            ulong* vectorKeys, float3* vectorValues,
            ulong* flagKeys, int* flagValues,
            CoreKeyframe* keyframes,
            ulong* keyframeRangeKeys, int* keyframeRangeStarts, int* keyframeRangeLengths
        );

        public static unsafe NativeArray<byte> Serialize(in DocumentData doc, Allocator allocator) {
            // Convert bool array to byte array for FFI
            var portIsInputBytes = new NativeArray<byte>(doc.Graph.PortIsInput.Length, Allocator.Temp);
            for (int i = 0; i < doc.Graph.PortIsInput.Length; i++) {
                portIsInputBytes[i] = doc.Graph.PortIsInput[i] ? (byte)1 : (byte)0;
            }

            // Extract maps to parallel arrays
            var scalarKeys = doc.Scalars.GetKeyArray(Allocator.Temp);
            var scalarValues = doc.Scalars.GetValueArray(Allocator.Temp);
            var vectorKeys = doc.Vectors.GetKeyArray(Allocator.Temp);
            var vectorValues = doc.Vectors.GetValueArray(Allocator.Temp);
            var flagKeys = doc.Flags.GetKeyArray(Allocator.Temp);
            var flagValues = doc.Flags.GetValueArray(Allocator.Temp);

            // Extract keyframe ranges
            var keyframeRangeKeys = doc.Keyframes.Ranges.GetKeyArray(Allocator.Temp);
            var keyframeRangeValues = doc.Keyframes.Ranges.GetValueArray(Allocator.Temp);
            var keyframeRangeStarts = new NativeArray<int>(keyframeRangeKeys.Length, Allocator.Temp);
            var keyframeRangeLengths = new NativeArray<int>(keyframeRangeKeys.Length, Allocator.Temp);
            for (int i = 0; i < keyframeRangeKeys.Length; i++) {
                keyframeRangeStarts[i] = keyframeRangeValues[i].x;
                keyframeRangeLengths[i] = keyframeRangeValues[i].y;
            }

            var ffiDoc = new FfiDocument {
                NodeIds = doc.Graph.NodeIds.Length > 0 ? (uint*)doc.Graph.NodeIds.GetUnsafeReadOnlyPtr() : null,
                NodeCount = (nuint)doc.Graph.NodeIds.Length,
                NodeTypes = doc.Graph.NodeTypes.Length > 0 ? (uint*)doc.Graph.NodeTypes.GetUnsafeReadOnlyPtr() : null,
                NodeInputCounts = doc.Graph.NodeInputCount.Length > 0 ? (int*)doc.Graph.NodeInputCount.GetUnsafeReadOnlyPtr() : null,
                NodeOutputCounts = doc.Graph.NodeOutputCount.Length > 0 ? (int*)doc.Graph.NodeOutputCount.GetUnsafeReadOnlyPtr() : null,
                PortIds = doc.Graph.PortIds.Length > 0 ? (uint*)doc.Graph.PortIds.GetUnsafeReadOnlyPtr() : null,
                PortCount = (nuint)doc.Graph.PortIds.Length,
                PortTypes = doc.Graph.PortTypes.Length > 0 ? (uint*)doc.Graph.PortTypes.GetUnsafeReadOnlyPtr() : null,
                PortOwners = doc.Graph.PortOwners.Length > 0 ? (uint*)doc.Graph.PortOwners.GetUnsafeReadOnlyPtr() : null,
                PortIsInput = portIsInputBytes.Length > 0 ? (byte*)portIsInputBytes.GetUnsafeReadOnlyPtr() : null,
                EdgeIds = doc.Graph.EdgeIds.Length > 0 ? (uint*)doc.Graph.EdgeIds.GetUnsafeReadOnlyPtr() : null,
                EdgeCount = (nuint)doc.Graph.EdgeIds.Length,
                EdgeSources = doc.Graph.EdgeSources.Length > 0 ? (uint*)doc.Graph.EdgeSources.GetUnsafeReadOnlyPtr() : null,
                EdgeTargets = doc.Graph.EdgeTargets.Length > 0 ? (uint*)doc.Graph.EdgeTargets.GetUnsafeReadOnlyPtr() : null,
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

            // Get required size
            long size = kex_save_size(&ffiDoc);
            if (size < 0) {
                throw new InvalidOperationException($"kex_save_size failed with code {size}");
            }

            // Allocate buffer and serialize
            var buffer = new NativeArray<byte>((int)size, allocator);
            nuint bytesWritten = 0;

            int result = kex_save(&ffiDoc, (byte*)buffer.GetUnsafePtr(), (nuint)size, &bytesWritten);

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

            if (result != 0) {
                buffer.Dispose();
                throw new InvalidOperationException($"kex_save failed with code {result}");
            }

            return buffer;
        }

        public static unsafe DocumentData Deserialize(NativeArray<byte> data, Allocator allocator) {
            // Load document
            IntPtr handle = kex_load((byte*)data.GetUnsafeReadOnlyPtr(), (nuint)data.Length);
            if (handle == IntPtr.Zero) {
                throw new InvalidOperationException("kex_load failed - invalid data format");
            }

            try {
                // Get counts
                DocumentCounts counts;
                int result = kex_load_get_counts(handle, &counts);
                if (result != 0) {
                    throw new InvalidOperationException($"kex_load_get_counts failed with code {result}");
                }

                // Allocate arrays
                var doc = DocumentData.Create(allocator);
                var nodeIds = new NativeList<uint>(counts.NodeCount, Allocator.Temp);
                var nodeTypes = new NativeList<uint>(counts.NodeCount, Allocator.Temp);
                var nodeInputCounts = new NativeList<int>(counts.NodeCount, Allocator.Temp);
                var nodeOutputCounts = new NativeList<int>(counts.NodeCount, Allocator.Temp);
                var nodePositions = new NativeList<float2>(counts.NodeCount, Allocator.Temp);
                var portIds = new NativeList<uint>(counts.PortCount, Allocator.Temp);
                var portTypes = new NativeList<uint>(counts.PortCount, Allocator.Temp);
                var portOwners = new NativeList<uint>(counts.PortCount, Allocator.Temp);
                var portIsInput = new NativeArray<byte>(counts.PortCount, Allocator.Temp);
                var edgeIds = new NativeList<uint>(counts.EdgeCount, Allocator.Temp);
                var edgeSources = new NativeList<uint>(counts.EdgeCount, Allocator.Temp);
                var edgeTargets = new NativeList<uint>(counts.EdgeCount, Allocator.Temp);
                var scalarKeys = new NativeArray<ulong>(counts.ScalarCount, Allocator.Temp);
                var scalarValues = new NativeArray<float>(counts.ScalarCount, Allocator.Temp);
                var vectorKeys = new NativeArray<ulong>(counts.VectorCount, Allocator.Temp);
                var vectorValues = new NativeArray<float3>(counts.VectorCount, Allocator.Temp);
                var flagKeys = new NativeArray<ulong>(counts.FlagCount, Allocator.Temp);
                var flagValues = new NativeArray<int>(counts.FlagCount, Allocator.Temp);
                var keyframes = new NativeArray<CoreKeyframe>(counts.KeyframeCount, Allocator.Temp);
                var keyframeRangeKeys = new NativeArray<ulong>(counts.KeyframeRangeCount, Allocator.Temp);
                var keyframeRangeStarts = new NativeArray<int>(counts.KeyframeRangeCount, Allocator.Temp);
                var keyframeRangeLengths = new NativeArray<int>(counts.KeyframeRangeCount, Allocator.Temp);

                // Resize NativeLists
                nodeIds.ResizeUninitialized(counts.NodeCount);
                nodeTypes.ResizeUninitialized(counts.NodeCount);
                nodeInputCounts.ResizeUninitialized(counts.NodeCount);
                nodeOutputCounts.ResizeUninitialized(counts.NodeCount);
                nodePositions.ResizeUninitialized(counts.NodeCount);
                portIds.ResizeUninitialized(counts.PortCount);
                portTypes.ResizeUninitialized(counts.PortCount);
                portOwners.ResizeUninitialized(counts.PortCount);
                edgeIds.ResizeUninitialized(counts.EdgeCount);
                edgeSources.ResizeUninitialized(counts.EdgeCount);
                edgeTargets.ResizeUninitialized(counts.EdgeCount);

                // Copy data
                result = kex_load_copy_data(
                    handle,
                    (uint*)nodeIds.GetUnsafePtr(),
                    (uint*)nodeTypes.GetUnsafePtr(),
                    (int*)nodeInputCounts.GetUnsafePtr(),
                    (int*)nodeOutputCounts.GetUnsafePtr(),
                    (uint*)portIds.GetUnsafePtr(),
                    (uint*)portTypes.GetUnsafePtr(),
                    (uint*)portOwners.GetUnsafePtr(),
                    (byte*)portIsInput.GetUnsafePtr(),
                    (uint*)edgeIds.GetUnsafePtr(),
                    (uint*)edgeSources.GetUnsafePtr(),
                    (uint*)edgeTargets.GetUnsafePtr(),
                    (ulong*)scalarKeys.GetUnsafePtr(),
                    (float*)scalarValues.GetUnsafePtr(),
                    (ulong*)vectorKeys.GetUnsafePtr(),
                    (float3*)vectorValues.GetUnsafePtr(),
                    (ulong*)flagKeys.GetUnsafePtr(),
                    (int*)flagValues.GetUnsafePtr(),
                    (CoreKeyframe*)keyframes.GetUnsafePtr(),
                    (ulong*)keyframeRangeKeys.GetUnsafePtr(),
                    (int*)keyframeRangeStarts.GetUnsafePtr(),
                    (int*)keyframeRangeLengths.GetUnsafePtr()
                );

                if (result != 0) {
                    throw new InvalidOperationException($"kex_load_copy_data failed with code {result}");
                }

                // Build graph
                for (int i = 0; i < counts.NodeCount; i++) {
                    doc.Graph.NodeIds.Add(nodeIds[i]);
                    doc.Graph.NodeTypes.Add(nodeTypes[i]);
                    doc.Graph.NodeInputCount.Add(nodeInputCounts[i]);
                    doc.Graph.NodeOutputCount.Add(nodeOutputCounts[i]);
                    doc.Graph.NodePositions.Add(float2.zero);
                }

                for (int i = 0; i < counts.PortCount; i++) {
                    doc.Graph.PortIds.Add(portIds[i]);
                    doc.Graph.PortTypes.Add(portTypes[i]);
                    doc.Graph.PortOwners.Add(portOwners[i]);
                    doc.Graph.PortIsInput.Add(portIsInput[i] != 0);
                }

                for (int i = 0; i < counts.EdgeCount; i++) {
                    doc.Graph.EdgeIds.Add(edgeIds[i]);
                    doc.Graph.EdgeSources.Add(edgeSources[i]);
                    doc.Graph.EdgeTargets.Add(edgeTargets[i]);
                }

                doc.Graph.NextNodeId = counts.NextNodeId;
                doc.Graph.NextPortId = counts.NextPortId;
                doc.Graph.NextEdgeId = counts.NextEdgeId;
                doc.Graph.RebuildIndexMaps();

                // Build scalars/vectors/flags
                for (int i = 0; i < counts.ScalarCount; i++) {
                    doc.Scalars[scalarKeys[i]] = scalarValues[i];
                }
                for (int i = 0; i < counts.VectorCount; i++) {
                    doc.Vectors[vectorKeys[i]] = vectorValues[i];
                }
                for (int i = 0; i < counts.FlagCount; i++) {
                    doc.Flags[flagKeys[i]] = flagValues[i];
                }

                // Build keyframes
                for (int i = 0; i < counts.KeyframeCount; i++) {
                    doc.Keyframes.Keyframes.Add(keyframes[i]);
                }
                for (int i = 0; i < counts.KeyframeRangeCount; i++) {
                    doc.Keyframes.Ranges[keyframeRangeKeys[i]] = new int2(keyframeRangeStarts[i], keyframeRangeLengths[i]);
                }

                // Cleanup temp arrays
                nodeIds.Dispose();
                nodeTypes.Dispose();
                nodeInputCounts.Dispose();
                nodeOutputCounts.Dispose();
                nodePositions.Dispose();
                portIds.Dispose();
                portTypes.Dispose();
                portOwners.Dispose();
                portIsInput.Dispose();
                edgeIds.Dispose();
                edgeSources.Dispose();
                edgeTargets.Dispose();
                scalarKeys.Dispose();
                scalarValues.Dispose();
                vectorKeys.Dispose();
                vectorValues.Dispose();
                flagKeys.Dispose();
                flagValues.Dispose();
                keyframes.Dispose();
                keyframeRangeKeys.Dispose();
                keyframeRangeStarts.Dispose();
                keyframeRangeLengths.Dispose();

                return doc;
            }
            finally {
                kex_load_free(handle);
            }
        }
    }
}
