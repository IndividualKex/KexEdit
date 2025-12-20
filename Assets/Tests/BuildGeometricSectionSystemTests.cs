using KexEdit.Legacy;
using KexEdit;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Tests;

namespace Tests {
    [TestFixture]
    [Category("Golden")]
    public class BuildGeometricSectionSystemTests : ECSTestsFixture {
        private SystemHandle _buildSystem;
        private SystemHandle _ecbSystem;

        [SetUp]
        public override void Setup() {
            base.Setup();
            _ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _buildSystem = World.GetOrCreateSystem<BuildGeometricSectionSystem>();
        }

        [Test]
        public void Shuttle_GeometricSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/shuttle.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 0);

            var entity = GeometricSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }

        [Test]
        public void Shuttle_GeometricSection2_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/shuttle.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 1);

            var entity = GeometricSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }

        [Test]
        public void Veloci_GeometricSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 0);

            var entity = GeometricSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }

        [Test]
        public void Veloci_GeometricSection2_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 1);

            var entity = GeometricSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }

        [Test]
        public void Veloci_GeometricSection3_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 2);

            var entity = GeometricSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }

        [Test]
        public void Veloci_GeometricSection4_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 3);

            var entity = GeometricSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }

        [Test]
        public void AllTypes_GeometricSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 0);

            var entity = GeometricSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }

        [Test]
        public void AllTypes_GeometricSection2_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetGeometricSectionByIndex(gold, 1);

            var entity = GeometricSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<CorePointBuffer>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }
    }
}
