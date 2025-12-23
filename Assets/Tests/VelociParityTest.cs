using KexEdit.Legacy;
using KexEdit.Legacy.Serialization;
using NUnit.Framework;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    public class VelociParityTest {
        private World _world;
        private SerializationSystem _serializationSystem;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp() {
            _world = new World("Veloci Test World");
            _serializationSystem = _world.CreateSystemManaged<SerializationSystem>();
            _entityManager = _world.EntityManager;
        }

        [TearDown]
        public void TearDown() {
            if (_world != null && _world.IsCreated) {
                _world.Dispose();
            }
        }

        [Test]
        public void Veloci_UndoRedo_PreservesNodePositions() {
            // This test reproduces the exact user scenario:
            // 1. Load veloci.kex (legacy)
            // 2. Simulate a change (which triggers Record for undo)
            // 3. Undo (deserialize from internal KEXD state)
            // 4. Verify no position collisions

            var legacyPath = Path.Combine("Assets", "Tests", "Assets", "veloci.kex");
            var legacyData = File.ReadAllBytes(legacyPath);

            // Step 1: Load legacy file
            var coasterEntity = _serializationSystem.DeserializeLegacyGraph(legacyData, false);

            var nodeQuery = _entityManager.CreateEntityQuery(typeof(Node));
            using var originalNodes = nodeQuery.ToEntityArray(Allocator.Temp);

            // Capture original positions
            var originalPositions = new NativeHashMap<uint, float2>(originalNodes.Length, Allocator.Temp);
            foreach (var entity in originalNodes) {
                var node = _entityManager.GetComponentData<Node>(entity);
                originalPositions[node.Id] = node.Position;
                UnityEngine.Debug.Log($"Original: Node {node.Id} at ({node.Position.x}, {node.Position.y})");
            }

            int originalCount = originalNodes.Length;
            UnityEngine.Debug.Log($"After legacy load: {originalCount} ECS nodes");

            // Step 2: Simulate user making a change (move a node)
            var firstNode = originalNodes[0];
            var nodeComp = _entityManager.GetComponentData<Node>(firstNode);
            var originalFirstNodePos = nodeComp.Position;
            nodeComp.Position += new float2(10, 10);
            _entityManager.SetComponentData(firstNode, nodeComp);

            // Record the changed state (this captures the moved position for undo)
            _serializationSystem.Record(coasterEntity);

            // Move the node again to create a second state
            nodeComp.Position += new float2(10, 10);
            _entityManager.SetComponentData(firstNode, nodeComp);

            // Step 3: Undo (should restore to the first moved position)
            // Note: Undo automatically destroys the old coaster and all its entities
            UnityEngine.Debug.Log("Performing undo...");
            var restoredEntity = _serializationSystem.Undo(coasterEntity);

            // Verify the first node is back to the first moved position (not original)
            // This confirms undo is working
            using var afterFirstUndo = nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in afterFirstUndo) {
                var node = _entityManager.GetComponentData<Node>(entity);
                if (node.Id == nodeComp.Id) {
                    var expectedPos = originalFirstNodePos + new float2(10, 10);
                    Assert.AreEqual(expectedPos.x, node.Position.x, 0.1f, "First undo should restore to first moved position");
                    Assert.AreEqual(expectedPos.y, node.Position.y, 0.1f, "First undo should restore to first moved position");
                    break;
                }
            }

            // Step 4: Verify all node positions after undo
            UnityEngine.Debug.Log($"After undo: {afterFirstUndo.Length} ECS nodes");
            Assert.AreEqual(originalCount, afterFirstUndo.Length, "Node count should be preserved");

            // Check each node's position (except the one we moved, which we already verified)
            var restoredPositions = new NativeHashMap<uint, float2>(afterFirstUndo.Length, Allocator.Temp);
            foreach (var entity in afterFirstUndo) {
                var node = _entityManager.GetComponentData<Node>(entity);
                restoredPositions[node.Id] = node.Position;
                UnityEngine.Debug.Log($"Restored: Node {node.Id} at ({node.Position.x}, {node.Position.y})");

                // Skip the node we intentionally moved
                if (node.Id == nodeComp.Id) continue;

                if (originalPositions.TryGetValue(node.Id, out float2 originalPos)) {
                    float dx = math.abs(originalPos.x - node.Position.x);
                    float dy = math.abs(originalPos.y - node.Position.y);
                    Assert.Less(dx, 0.1f, $"Node {node.Id} X position changed after undo (expected unchanged)");
                    Assert.Less(dy, 0.1f, $"Node {node.Id} Y position changed after undo (expected unchanged)");
                } else {
                    Assert.Fail($"Node {node.Id} not found in original positions");
                }
            }

            // Check for position collisions
            var positionGroups = afterFirstUndo
                .Select(e => _entityManager.GetComponentData<Node>(e))
                .GroupBy(n => (math.round(n.Position.x * 10), math.round(n.Position.y * 10)))
                .Where(g => g.Count() > 1)
                .ToList();

            if (positionGroups.Any()) {
                UnityEngine.Debug.LogError($"Found {positionGroups.Count()} position collisions after undo:");
                foreach (var group in positionGroups) {
                    var firstInGroup = group.First();
                    var nodeIds = string.Join(", ", group.Select(n => n.Id));
                    UnityEngine.Debug.LogError($"  Position ({firstInGroup.Position.x}, {firstInGroup.Position.y}): nodes {nodeIds}");
                }
                Assert.Fail($"Position collisions detected after undo");
            }

            originalPositions.Dispose();
            restoredPositions.Dispose();
            _entityManager.DestroyEntity(restoredEntity);
        }

        [Test]
        public void Veloci_LegacyToKexd_NoCollisions() {
            // Load veloci.kex (legacy format)
            var legacyPath = Path.Combine("Assets", "Tests", "Assets", "veloci.kex");
            var legacyData = File.ReadAllBytes(legacyPath);

            // Deserialize legacy
            var coasterEntity1 = _serializationSystem.DeserializeLegacyGraph(legacyData, false);
            var coaster1 = _entityManager.GetComponentData<CoasterData>(coasterEntity1).Value;

            UnityEngine.Debug.Log($"After legacy import: {coaster1.Graph.NodeIds.Length} nodes");

            // Get all node positions after legacy import
            var positions1 = new NativeHashMap<uint, float2>(coaster1.Graph.NodeIds.Length, Allocator.Temp);
            for (int i = 0; i < coaster1.Graph.NodeIds.Length; i++) {
                positions1[coaster1.Graph.NodeIds[i]] = coaster1.Graph.NodePositions[i];
            }

            // Also get ECS entity positions
            var nodeQuery = _entityManager.CreateEntityQuery(typeof(Node));
            using var nodeEntities = nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in nodeEntities) {
                var node = _entityManager.GetComponentData<Node>(entity);
                if (positions1.ContainsKey(node.Id)) {
                    // Override with ECS position if different
                    positions1[node.Id] = node.Position;
                }
            }

            UnityEngine.Debug.Log($"Collected {positions1.Count} positions after legacy load");

            // Serialize to KEXD
            var kexdData1 = _serializationSystem.SerializeToKEXD(coasterEntity1);

            // Clean up first coaster
            _entityManager.DestroyEntity(coasterEntity1);

            // Deserialize KEXD
            var coasterEntity2 = _serializationSystem.DeserializeGraph(kexdData1, false);
            var coaster2 = _entityManager.GetComponentData<CoasterData>(coasterEntity2).Value;

            UnityEngine.Debug.Log($"After KEXD round-trip: {coaster2.Graph.NodeIds.Length} nodes");

            // Get all node positions after KEXD round-trip
            var positions2 = new NativeHashMap<uint, float2>(coaster2.Graph.NodeIds.Length, Allocator.Temp);
            for (int i = 0; i < coaster2.Graph.NodeIds.Length; i++) {
                positions2[coaster2.Graph.NodeIds[i]] = coaster2.Graph.NodePositions[i];
            }

            // Also get ECS entity positions
            using var nodeEntities2 = nodeQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in nodeEntities2) {
                var node = _entityManager.GetComponentData<Node>(entity);
                if (positions2.ContainsKey(node.Id)) {
                    positions2[node.Id] = node.Position;
                }
            }

            UnityEngine.Debug.Log($"Collected {positions2.Count} positions after KEXD round-trip");

            // Compare node positions
            foreach (var kvp in positions1) {
                uint nodeId = kvp.Key;
                float2 pos1 = kvp.Value;

                Assert.IsTrue(positions2.TryGetValue(nodeId, out float2 pos2),
                    $"Node {nodeId} missing after KEXD round-trip");

                float dx = math.abs(pos1.x - pos2.x);
                float dy = math.abs(pos1.y - pos2.y);

                Assert.Less(dx, 0.1f, $"Node {nodeId} X position changed: {pos1.x} -> {pos2.x}");
                Assert.Less(dy, 0.1f, $"Node {nodeId} Y position changed: {pos1.y} -> {pos2.y}");
            }

            // Check for collisions (nodes at same position)
            var positionMap = new NativeHashMap<int, NativeList<uint>>(coaster2.Graph.NodeIds.Length, Allocator.Temp);
            for (int i = 0; i < coaster2.Graph.NodeIds.Length; i++) {
                uint nodeId = coaster2.Graph.NodeIds[i];
                float2 pos = positions2[nodeId];

                // Round to avoid floating point precision issues
                int posKey = (int)(pos.x * 10) + (int)(pos.y * 10) * 10000;

                if (!positionMap.ContainsKey(posKey)) {
                    positionMap[posKey] = new NativeList<uint>(Allocator.Temp);
                }
                positionMap[posKey].Add(nodeId);
            }

            // Report collisions
            var collisionKeys = new NativeList<int>(Allocator.Temp);
            foreach (var kvp in positionMap) {
                if (kvp.Value.Length > 1) {
                    collisionKeys.Add(kvp.Key);
                }
            }

            if (collisionKeys.Length > 0) {
                UnityEngine.Debug.LogError($"Found {collisionKeys.Length} position collisions:");
                foreach (var key in collisionKeys) {
                    var nodeIds = positionMap[key];
                    var firstPos = positions2[nodeIds[0]];
                    UnityEngine.Debug.LogError($"  Position ({firstPos.x}, {firstPos.y}): nodes {string.Join(", ", nodeIds.AsArray())}");
                }
                Assert.Fail($"Found {collisionKeys.Length} position collisions after KEXD round-trip");
            }

            // Cleanup
            positions1.Dispose();
            positions2.Dispose();
            foreach (var kvp in positionMap) {
                kvp.Value.Dispose();
            }
            positionMap.Dispose();
            collisionKeys.Dispose();
            _entityManager.DestroyEntity(coasterEntity2);
        }
    }
}
