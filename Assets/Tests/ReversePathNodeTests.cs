using KexEdit.Core;
using KexEdit.Nodes.ReversePath;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests {
    [TestFixture]
    public class ReversePathNodeTests {
        [Test]
        public void Build_EmptyPath_ReturnsEmpty() {
            var path = new NativeArray<Point>(0, Allocator.Temp);
            var result = new NativeList<Point>(Allocator.Temp);

            try {
                ReversePathNode.Build(in path, ref result);
                Assert.AreEqual(0, result.Length);
            }
            finally {
                path.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void Build_SinglePoint_ReversesOrientation() {
            var path = new NativeArray<Point>(1, Allocator.Temp);
            path[0] = CreateTestPoint(0);

            var result = new NativeList<Point>(Allocator.Temp);

            try {
                ReversePathNode.Build(in path, ref result);

                Assert.AreEqual(1, result.Length);
                Assert.AreEqual(-path[0].Direction.z, result[0].Direction.z, 1e-5f);
                Assert.AreEqual(-path[0].Lateral.x, result[0].Lateral.x, 1e-5f);
            }
            finally {
                path.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void Build_MultiplePpoints_ReversesOrder() {
            var path = new NativeArray<Point>(3, Allocator.Temp);
            for (int i = 0; i < 3; i++) {
                path[i] = CreateTestPoint(i);
            }

            var result = new NativeList<Point>(Allocator.Temp);

            try {
                ReversePathNode.Build(in path, ref result);

                Assert.AreEqual(3, result.Length);
                Assert.AreEqual(path[2].SpinePosition.z, result[0].SpinePosition.z, 1e-5f);
                Assert.AreEqual(path[1].SpinePosition.z, result[1].SpinePosition.z, 1e-5f);
                Assert.AreEqual(path[0].SpinePosition.z, result[2].SpinePosition.z, 1e-5f);
            }
            finally {
                path.Dispose();
                result.Dispose();
            }
        }

        [Test]
        public void Build_PreservesNormal() {
            var path = new NativeArray<Point>(1, Allocator.Temp);
            path[0] = CreateTestPoint(0);

            var result = new NativeList<Point>(Allocator.Temp);

            try {
                ReversePathNode.Build(in path, ref result);

                Assert.AreEqual(path[0].Normal.x, result[0].Normal.x, 1e-5f);
                Assert.AreEqual(path[0].Normal.y, result[0].Normal.y, 1e-5f);
                Assert.AreEqual(path[0].Normal.z, result[0].Normal.z, 1e-5f);
            }
            finally {
                path.Dispose();
                result.Dispose();
            }
        }

        private static Point CreateTestPoint(int index) {
            return new Point(
                direction: new float3(0f, 0f, 1f),
                lateral: new float3(1f, 0f, 0f),
                normal: new float3(0f, 1f, 0f),
                spinePosition: new float3(0f, 0f, index * 10f),
                velocity: 10f,
                energy: 100f,
                normalForce: 1f,
                lateralForce: 0.1f,
                heartArc: index * 10f,
                spineArc: index * 11f,
                spineAdvance: 10f,
                frictionOrigin: 0f,
                rollSpeed: 0f,
                heartOffset: 1.1f,
                friction: 0f,
                resistance: 0f
            );
        }
    }
}
