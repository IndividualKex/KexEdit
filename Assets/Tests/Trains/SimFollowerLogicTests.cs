using KexEdit.Document;
using KexEdit.Sim;
using KexEdit.Track;
using KexEdit.Trains.Sim;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

public class SimFollowerLogicTests {
    [Test]
    public void GetCurrentPoint_SingleSegment_ReturnsFirstPoint() {
        var track = CreateSingleSegmentTrack(out var expectedFirst);
        try {
            var follower = SimFollower.Default;
            SimFollowerLogic.GetCurrentPoint(in follower, in track, out Point point);
            Assert.AreEqual(expectedFirst.x, point.HeartPosition.x, 0.001f);
            Assert.AreEqual(expectedFirst.y, point.HeartPosition.y, 0.001f);
            Assert.AreEqual(expectedFirst.z, point.HeartPosition.z, 0.001f);
        } finally {
            track.Dispose();
        }
    }

    [Test]
    public void Advance_SingleSegment_AdvancesThroughPoints() {
        var track = CreateSingleSegmentTrack(out _);
        try {
            var follower = SimFollower.Default;
            SimFollowerLogic.Advance(ref follower, in track, 0.01f, 100f, false, out _);
            Assert.AreEqual(0, follower.TraversalIndex);
            Assert.Greater(follower.PointIndex, 0f);
        } finally {
            track.Dispose();
        }
    }

    [Test]
    public void Advance_SingleSegment_Interpolates() {
        var track = CreateSingleSegmentTrack(out _);
        try {
            var follower = new SimFollower { TraversalIndex = 0, PointIndex = 0.5f };
            SimFollowerLogic.GetCurrentPoint(in follower, in track, out Point point);
            Assert.Greater(point.HeartPosition.z, 0f);
            Assert.Less(point.HeartPosition.z, 10f);
        } finally {
            track.Dispose();
        }
    }

    [Test]
    public void Advance_MultipleSegments_TransitionsCorrectly() {
        var track = CreateMultiSegmentTrack();
        try {
            var follower = SimFollower.Default;
            SimFollowerLogic.Advance(ref follower, in track, 1.0f, 100f, false, out _);
            Assert.Greater(follower.TraversalIndex, 0);
        } finally {
            track.Dispose();
        }
    }

    [Test]
    public void Advance_ForwardSegment_FacingIsOne() {
        var track = CreateSingleSegmentTrack(out _);
        try {
            var follower = SimFollower.Default;
            SimFollowerLogic.Advance(ref follower, in track, 0.01f, 100f, false, out _);
            Assert.AreEqual(1, follower.Facing);
        } finally {
            track.Dispose();
        }
    }

    [Test]
    public void Advance_ReversedSegment_FacingIsNegativeOne() {
        var track = CreateReversedSegmentTrack();
        try {
            var follower = SimFollower.Default;
            SimFollowerLogic.Advance(ref follower, in track, 0.01f, 100f, false, out _);
            Assert.AreEqual(-1, follower.Facing);
        } finally {
            track.Dispose();
        }
    }

    private static Track CreateSingleSegmentTrack(out float3 firstPosition) {
        var points = new NativeList<Point>(10, Allocator.Temp);
        for (int i = 0; i < 10; i++) {
            points.Add(new Point(
                heartPosition: new float3(0f, 0f, i * 10f),
                direction: new float3(0f, 0f, 1f),
                normal: new float3(0f, 1f, 0f),
                lateral: new float3(1f, 0f, 0f),
                velocity: 10f, normalForce: 0f, lateralForce: 0f,
                heartArc: i, spineArc: i, heartAdvance: i, frictionOrigin: 0f
            ));
        }

        var sections = new NativeArray<Section>(1, Allocator.Temp);
        sections[0] = new Section {
            StartIndex = 0,
            EndIndex = 9,
            ArcStart = 0f,
            ArcEnd = 9f,
            Flags = 0
        };

        var traversalOrder = new NativeArray<int>(1, Allocator.Temp);
        traversalOrder[0] = 0;

        firstPosition = new float3(0f, 0f, 0f);

        return new Track {
            Points = points,
            Sections = sections,
            TraversalOrder = traversalOrder
        };
    }

    private static Track CreateMultiSegmentTrack() {
        var points = new NativeList<Point>(10, Allocator.Temp);

        for (int seg = 0; seg < 2; seg++) {
            for (int i = 0; i < 5; i++) {
                points.Add(new Point(
                    heartPosition: new float3(0f, 0f, seg * 50f + i * 10f),
                    direction: new float3(0f, 0f, 1f),
                    normal: new float3(0f, 1f, 0f),
                    lateral: new float3(1f, 0f, 0f),
                    velocity: 10f, normalForce: 0f, lateralForce: 0f,
                    heartArc: i, spineArc: i, heartAdvance: i, frictionOrigin: 0f
                ));
            }
        }

        var sections = new NativeArray<Section>(2, Allocator.Temp);
        sections[0] = new Section {
            StartIndex = 0,
            EndIndex = 4,
            ArcStart = 0f,
            ArcEnd = 4f,
            Flags = 0
        };
        sections[1] = new Section {
            StartIndex = 5,
            EndIndex = 9,
            ArcStart = 0f,
            ArcEnd = 4f,
            Flags = 0
        };

        var traversalOrder = new NativeArray<int>(2, Allocator.Temp);
        traversalOrder[0] = 0;
        traversalOrder[1] = 1;

        return new Track {
            Points = points,
            Sections = sections,
            TraversalOrder = traversalOrder
        };
    }

    private static Track CreateReversedSegmentTrack() {
        var points = new NativeList<Point>(10, Allocator.Temp);
        for (int i = 0; i < 10; i++) {
            points.Add(new Point(
                heartPosition: new float3(0f, 0f, i * 10f),
                direction: new float3(0f, 0f, -1f),
                normal: new float3(0f, 1f, 0f),
                lateral: new float3(-1f, 0f, 0f),
                velocity: 10f, normalForce: 0f, lateralForce: 0f,
                heartArc: i, spineArc: i, heartAdvance: i, frictionOrigin: 0f
            ));
        }

        var sections = new NativeArray<Section>(1, Allocator.Temp);
        sections[0] = new Section {
            StartIndex = 0,
            EndIndex = 9,
            ArcStart = 0f,
            ArcEnd = 9f,
            Flags = NodeFlag.Reversed
        };

        var traversalOrder = new NativeArray<int>(1, Allocator.Temp);
        traversalOrder[0] = 0;

        return new Track {
            Points = points,
            Sections = sections,
            TraversalOrder = traversalOrder
        };
    }
}
