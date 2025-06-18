using NUnit.Framework;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using KexEdit.UI.Serialization;

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
            Version = 1,
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
}
