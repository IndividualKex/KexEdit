using KexEdit.Legacy;
using NUnit.Framework;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using KexEdit.Legacy.Serialization;

public class SerializationTests {
    [Test]
    public void TestBasicBinaryReadWrite() {
        // Test the basic binary read/write operations
        using var buffer = new NativeArray<byte>(64, Allocator.Temp);

        var writer = new BinaryWriter(buffer);

        // Write test data
        writer.Write(42);           // int
        writer.Write(3.14f);        // float

        Debug.Log($"Written {writer.Position} bytes");

        // Read back
        var reader = new BinaryReader(buffer);
        int intVal = reader.Read<int>();
        float floatVal = reader.Read<float>();

        Debug.Log($"Read {reader.Position} bytes. Int: {intVal}, Float: {floatVal}");

        // Verify
        Assert.AreEqual(42, intVal);
        Assert.AreEqual(3.14f, floatVal, 0.001f);
    }

    [Test]
    public void TestEmptyArraySerialization() {
        // Test serializing empty arrays
        using var buffer = new NativeArray<byte>(64, Allocator.Temp);

        var writer = new BinaryWriter(buffer);

        // Write empty array
        using var emptyArray = new NativeArray<int>(0, Allocator.Temp);
        writer.WriteArray(emptyArray);

        Debug.Log($"Written empty array, position: {writer.Position}");

        // Read back
        var reader = new BinaryReader(buffer);
        reader.ReadArray(out NativeArray<int> result, Allocator.Temp);

        Debug.Log($"Read empty array, length: {result.Length}, position: {reader.Position}");

        Assert.AreEqual(0, result.Length);
        Assert.AreEqual(writer.Position, reader.Position);

        if (result.IsCreated) result.Dispose();
    }

    [Test]
    public void TestArrayWithDataSerialization() {
        // Test serializing arrays with actual data
        using var buffer = new NativeArray<byte>(256, Allocator.Temp);

        var writer = new BinaryWriter(buffer);

        // Create test data
        var testData = new NativeArray<float>(3, Allocator.Temp);
        try {
            testData[0] = 1.5f;
            testData[1] = 2.7f;
            testData[2] = 3.9f;

            writer.WriteArray(testData);

            Debug.Log($"Written array with {testData.Length} elements, position: {writer.Position}");

            // Read back
            var reader = new BinaryReader(buffer);
            reader.ReadArray(out NativeArray<float> result, Allocator.Temp);

            try {
                Debug.Log($"Read array with {result.Length} elements, position: {reader.Position}");

                Assert.AreEqual(testData.Length, result.Length);
                Assert.AreEqual(writer.Position, reader.Position);

                for (int i = 0; i < testData.Length; i++) {
                    Assert.AreEqual(testData[i], result[i], 0.001f, $"Element {i} mismatch");
                }
            }
            finally {
                if (result.IsCreated) result.Dispose();
            }
        }
        finally {
            testData.Dispose();
        }
    }

    [Test]
    public void TestFloat2Serialization() {
        // Test serializing Unity math types
        using var buffer = new NativeArray<byte>(64, Allocator.Temp);

        var writer = new BinaryWriter(buffer);

        float2 testValue = new(1.23f, 4.56f);
        writer.Write(testValue);

        Debug.Log($"Written float2, position: {writer.Position}");

        // Read back
        var reader = new BinaryReader(buffer);
        float2 result = reader.Read<float2>();

        Debug.Log($"Read float2: ({result.x}, {result.y}), position: {reader.Position}");

        Assert.AreEqual(testValue.x, result.x, 0.001f);
        Assert.AreEqual(testValue.y, result.y, 0.001f);
        Assert.AreEqual(writer.Position, reader.Position);
    }

    [Test]
    public void TestEmptyGraphSerialization() {
        // Test full empty graph serialization
        var graph = new SerializedGraph {
            Version = SerializationVersion.CURRENT,
            UIState = new SerializedUIState {
                TimelineOffset = 100f,
                TimelineZoom = 1.5f,
                NodeGraphPanX = 50f,
                NodeGraphPanY = -25f,
                NodeGraphZoom = 2f,
                CameraTargetPositionX = 10f,
                CameraTargetPositionY = 5f,
                CameraTargetPositionZ = -15f,
                CameraTargetDistance = 20f,
                CameraTargetPitch = 30f,
                CameraTargetYaw = 45f,
                CameraSpeedMultiplier = 2.5f
            },
            Nodes = new NativeArray<SerializedNode>(0, Allocator.Temp),
            Edges = new NativeArray<SerializedEdge>(0, Allocator.Temp)
        };

        try {
            // Calculate size and create buffer
            int size = SizeCalculator.CalculateSize(ref graph);
            Debug.Log($"Empty graph calculated size: {size}");

            var buffer = new NativeArray<byte>(size, Allocator.Temp);

            try {
                // Serialize
                int serializedSize = GraphSerializer.Serialize(ref graph, ref buffer);
                Debug.Log($"Serialized empty graph: {serializedSize} bytes");

                // Create new graph for deserialization
                var deserializedGraph = new SerializedGraph();

                try {
                    // Deserialize
                    int deserializedSize = GraphSerializer.Deserialize(ref deserializedGraph, ref buffer);
                    Debug.Log($"Deserialized empty graph: {deserializedSize} bytes");

                    // Verify
                    Assert.AreEqual(graph.Version, deserializedGraph.Version);
                    Assert.AreEqual(graph.Nodes.Length, deserializedGraph.Nodes.Length);
                    Assert.AreEqual(graph.Edges.Length, deserializedGraph.Edges.Length);
                    Assert.AreEqual(serializedSize, deserializedSize);

                    // Verify UI state
                    Assert.AreEqual(graph.UIState.TimelineOffset, deserializedGraph.UIState.TimelineOffset, 0.001f);
                    Assert.AreEqual(graph.UIState.TimelineZoom, deserializedGraph.UIState.TimelineZoom, 0.001f);
                    Assert.AreEqual(graph.UIState.NodeGraphPanX, deserializedGraph.UIState.NodeGraphPanX, 0.001f);
                    Assert.AreEqual(graph.UIState.NodeGraphPanY, deserializedGraph.UIState.NodeGraphPanY, 0.001f);
                    Assert.AreEqual(graph.UIState.NodeGraphZoom, deserializedGraph.UIState.NodeGraphZoom, 0.001f);
                    Assert.AreEqual(graph.UIState.CameraTargetPositionX, deserializedGraph.UIState.CameraTargetPositionX, 0.001f);
                    Assert.AreEqual(graph.UIState.CameraTargetPositionY, deserializedGraph.UIState.CameraTargetPositionY, 0.001f);
                    Assert.AreEqual(graph.UIState.CameraTargetPositionZ, deserializedGraph.UIState.CameraTargetPositionZ, 0.001f);
                    Assert.AreEqual(graph.UIState.CameraTargetDistance, deserializedGraph.UIState.CameraTargetDistance, 0.001f);
                    Assert.AreEqual(graph.UIState.CameraTargetPitch, deserializedGraph.UIState.CameraTargetPitch, 0.001f);
                    Assert.AreEqual(graph.UIState.CameraTargetYaw, deserializedGraph.UIState.CameraTargetYaw, 0.001f);
                    Assert.AreEqual(graph.UIState.CameraSpeedMultiplier, deserializedGraph.UIState.CameraSpeedMultiplier, 0.001f);
                }
                finally {
                    deserializedGraph.Dispose();
                }
            }
            finally {
                buffer.Dispose();
            }
        }
        finally {
            graph.Dispose();
        }
    }

    [Test]
    public void TestVersionAndCountSerialization() {
        // Test just the version and counts to isolate potential issues
        using var buffer = new NativeArray<byte>(64, Allocator.Temp);

        var writer = new BinaryWriter(buffer);

        // Write version and counts like SerializeBinary does
        int version = 1;
        int nodeCount = 0;
        int edgeCount = 0;

        writer.Write(version);
        writer.Write(nodeCount);
        writer.Write(edgeCount);

        Debug.Log($"Written version: {version}, nodeCount: {nodeCount}, edgeCount: {edgeCount}, position: {writer.Position}");

        // Read back like DeserializeBinary does
        var reader = new BinaryReader(buffer);
        int readVersion = reader.Read<int>();
        int readNodeCount = reader.Read<int>();
        int readEdgeCount = reader.Read<int>();

        Debug.Log($"Read version: {readVersion}, nodeCount: {readNodeCount}, edgeCount: {readEdgeCount}, position: {reader.Position}");

        Assert.AreEqual(version, readVersion);
        Assert.AreEqual(nodeCount, readNodeCount);
        Assert.AreEqual(edgeCount, readEdgeCount);
        Assert.AreEqual(writer.Position, reader.Position);
    }

    [Test]
    public void TestFieldFlagsSerialization() {
        // Test the robust field flags system
        using var buffer = new NativeArray<byte>(256, Allocator.Temp);

        var writer = new BinaryWriter(buffer);

        // Test different flag combinations
        var flags1 = NodeFieldFlags.HasRender | NodeFieldFlags.HasSelected;
        var flags2 = NodeFieldFlags.HasCurveData | NodeFieldFlags.HasDuration;
        var flags3 = NodeFieldFlags.None;

        writer.Write((uint)flags1);
        writer.Write((uint)flags2);
        writer.Write((uint)flags3);

        Debug.Log($"Written flags, position: {writer.Position}");

        // Read back
        var reader = new BinaryReader(buffer);
        var readFlags1 = (NodeFieldFlags)reader.Read<uint>();
        var readFlags2 = (NodeFieldFlags)reader.Read<uint>();
        var readFlags3 = (NodeFieldFlags)reader.Read<uint>();

        Debug.Log($"Read flags, position: {reader.Position}");

        Assert.AreEqual(flags1, readFlags1);
        Assert.AreEqual(flags2, readFlags2);
        Assert.AreEqual(flags3, readFlags3);
        Assert.AreEqual(writer.Position, reader.Position);

        // Test flag operations
        Assert.IsTrue((readFlags1 & NodeFieldFlags.HasRender) != 0);
        Assert.IsTrue((readFlags1 & NodeFieldFlags.HasSelected) != 0);
        Assert.IsFalse((readFlags1 & NodeFieldFlags.HasCurveData) != 0);

        Assert.IsTrue((readFlags2 & NodeFieldFlags.HasCurveData) != 0);
        Assert.IsTrue((readFlags2 & NodeFieldFlags.HasDuration) != 0);
        Assert.IsFalse((readFlags2 & NodeFieldFlags.HasRender) != 0);

        Assert.AreEqual(NodeFieldFlags.None, readFlags3);
    }

    [Test]
    public void TestMigrationFromVersion1() {
        // Test migration of a pre-HandleType file
        string testFilePath = "Assets/Tests/Assets/shuttle_v1.kex";

        Assert.IsTrue(System.IO.File.Exists(testFilePath), "Test file shuttle_v1.kex not found");

        byte[] fileData = System.IO.File.ReadAllBytes(testFilePath);
        Assert.IsTrue(fileData.Length > 0, "Test file is empty");

        Debug.Log($"Loaded test file: {fileData.Length} bytes");

        try {
            // Create buffer and deserialize
            var buffer = new NativeArray<byte>(fileData.Length, Allocator.Temp);
            buffer.CopyFrom(fileData);

            var graph = new SerializedGraph();

            try {
                int bytesRead = GraphSerializer.Deserialize(ref graph, ref buffer);
                Debug.Log($"Deserialized {bytesRead} bytes, version: {graph.Version}");

                // Should have migrated to current version
                Assert.AreEqual(SerializationVersion.CURRENT, graph.Version);

                // Verify UI state has default values for migrated files
                Assert.AreEqual(0f, graph.UIState.TimelineOffset, 0.001f);
                Assert.AreEqual(1f, graph.UIState.TimelineZoom, 0.001f);
                Assert.AreEqual(0f, graph.UIState.NodeGraphPanX, 0.001f);
                Assert.AreEqual(0f, graph.UIState.NodeGraphPanY, 0.001f);
                Assert.AreEqual(1f, graph.UIState.NodeGraphZoom, 0.001f);
                Assert.AreEqual(6f, graph.UIState.CameraTargetPositionX, 0.001f);
                Assert.AreEqual(6f, graph.UIState.CameraTargetPositionY, 0.001f);
                Assert.AreEqual(6f, graph.UIState.CameraTargetPositionZ, 0.001f);
                Assert.AreEqual(10.39230484541326f, graph.UIState.CameraTargetDistance, 0.001f);
                Assert.AreEqual(30f, graph.UIState.CameraTargetPitch, 0.001f);
                Assert.AreEqual(-135f, graph.UIState.CameraTargetYaw, 0.001f);
                Assert.AreEqual(1f, graph.UIState.CameraSpeedMultiplier, 0.001f);

                // Check that we have some nodes (shuttle.kex should contain data)
                Assert.IsTrue(graph.Nodes.Length > 0, "Expected nodes in shuttle.kex");

                Debug.Log($"Successfully migrated {graph.Nodes.Length} nodes");

                // Check that keyframes were migrated properly
                bool foundKeyframes = false;
                for (int i = 0; i < graph.Nodes.Length; i++) {
                    var node = graph.Nodes[i];
                    if (node.RollSpeedKeyframes.Length > 0) {
                        foundKeyframes = true;
                        var keyframe = node.RollSpeedKeyframes[0].Value;

                        // Should have valid interpolation types (no value 3)
                        Assert.IsTrue((int)keyframe.InInterpolation <= 2, $"Invalid InInterpolation: {(int)keyframe.InInterpolation}");
                        Assert.IsTrue((int)keyframe.OutInterpolation <= 2, $"Invalid OutInterpolation: {(int)keyframe.OutInterpolation}");

                        Debug.Log($"Keyframe interpolation: In={keyframe.InInterpolation}, Out={keyframe.OutInterpolation}, Handle={keyframe.HandleType}");
                        break;
                    }
                }

                if (foundKeyframes) {
                    Debug.Log("Successfully found and validated migrated keyframes");
                }
                else {
                    Debug.Log("No keyframes found to validate");
                }
            }
            finally {
                graph.Dispose();
                buffer.Dispose();
            }
        }
        catch (System.Exception ex) {
            Assert.Fail($"Migration failed: {ex.Message}");
        }
    }

    [Test]
    public void TestUIStateSerialization() {
        // Test UI state serialization specifically
        var buffer = new NativeArray<byte>(256, Allocator.Temp);

        var writer = new BinaryWriter(buffer);

        // Write version
        writer.Write(SerializationVersion.UI_STATE_SERIALIZATION);

        // Write UI state
        writer.Write(123.45f);  // TimelineOffset
        writer.Write(2.5f);     // TimelineZoom
        writer.Write(-50.25f);  // NodeGraphPanX
        writer.Write(75.75f);   // NodeGraphPanY
        writer.Write(1.25f);    // NodeGraphZoom
        writer.Write(10.5f);    // CameraTargetPositionX
        writer.Write(-5.25f);   // CameraTargetPositionY
        writer.Write(15.75f);   // CameraTargetPositionZ
        writer.Write(25.0f);    // CameraTargetDistance
        writer.Write(45.0f);    // CameraTargetPitch
        writer.Write(90.0f);    // CameraTargetYaw
        writer.Write(3.5f);     // CameraSpeedMultiplier

        // Write empty node/edge counts
        writer.Write(0);        // Node count
        writer.Write(0);        // Edge count (handled by WriteArray for empty)

        Debug.Log($"Written UI state data, position: {writer.Position}");

        // Read back
        var reader = new BinaryReader(buffer);
        var graph = new SerializedGraph();

        try {
            int bytesRead = GraphSerializer.Deserialize(ref graph, ref buffer);
            Debug.Log($"Read UI state data, bytes: {bytesRead}");

            // Verify all UI state values
            Assert.AreEqual(123.45f, graph.UIState.TimelineOffset, 0.001f);
            Assert.AreEqual(2.5f, graph.UIState.TimelineZoom, 0.001f);
            Assert.AreEqual(-50.25f, graph.UIState.NodeGraphPanX, 0.001f);
            Assert.AreEqual(75.75f, graph.UIState.NodeGraphPanY, 0.001f);
            Assert.AreEqual(1.25f, graph.UIState.NodeGraphZoom, 0.001f);
            Assert.AreEqual(10.5f, graph.UIState.CameraTargetPositionX, 0.001f);
            Assert.AreEqual(-5.25f, graph.UIState.CameraTargetPositionY, 0.001f);
            Assert.AreEqual(15.75f, graph.UIState.CameraTargetPositionZ, 0.001f);
            Assert.AreEqual(25.0f, graph.UIState.CameraTargetDistance, 0.001f);
            Assert.AreEqual(45.0f, graph.UIState.CameraTargetPitch, 0.001f);
            Assert.AreEqual(90.0f, graph.UIState.CameraTargetYaw, 0.001f);
            Assert.AreEqual(3.5f, graph.UIState.CameraSpeedMultiplier, 0.001f);

            Debug.Log("UI state serialization test passed");
        }
        finally {
            graph.Dispose();
            buffer.Dispose();
        }
    }

    [Test]
    public void TestKeyframeLockingFlags() {
        var keyframe = KexEdit.Legacy.Keyframe.Default;

        Assert.IsFalse(keyframe.IsTimeLocked);
        Assert.IsFalse(keyframe.IsValueLocked);

        var timeLockedKeyframe = keyframe.WithTimeLock(true);
        Assert.IsTrue(timeLockedKeyframe.IsTimeLocked);
        Assert.IsFalse(timeLockedKeyframe.IsValueLocked);

        var valueLockedKeyframe = keyframe.WithValueLock(true);
        Assert.IsFalse(valueLockedKeyframe.IsTimeLocked);
        Assert.IsTrue(valueLockedKeyframe.IsValueLocked);

        var bothLockedKeyframe = keyframe.WithTimeLock(true).WithValueLock(true);
        Assert.IsTrue(bothLockedKeyframe.IsTimeLocked);
        Assert.IsTrue(bothLockedKeyframe.IsValueLocked);

        var unlockedKeyframe = bothLockedKeyframe.WithTimeLock(false).WithValueLock(false);
        Assert.IsFalse(unlockedKeyframe.IsTimeLocked);
        Assert.IsFalse(unlockedKeyframe.IsValueLocked);
    }
}
