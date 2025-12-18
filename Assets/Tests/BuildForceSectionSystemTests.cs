using KexEdit.Legacy;
using KexEdit;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Tests;

namespace Tests {
    [TestFixture]
    [Category("Golden")]
    public class BuildForceSectionSystemTests : ECSTestsFixture {
        private SystemHandle _buildSystem;
        private SystemHandle _ecbSystem;

        [SetUp]
        public override void Setup() {
            base.Setup();
            _ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            _buildSystem = World.GetOrCreateSystem<BuildForceSectionSystem>();
        }

        [Test]
        public void Shuttle_ForceSection_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/shuttle.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 0);

            var entity = ForceSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<Point>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }

        [Test]
        public void Veloci_ForceSection1_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 0);

            var entity = ForceSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<Point>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }

        [Test]
        public void Veloci_ForceSection2_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/veloci.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 1);

            var entity = ForceSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<Point>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }

        [Test]
        public void AllTypes_ForceSection_MatchesGoldData() {
            var gold = GoldDataLoader.Load("Assets/Tests/TrackData/all_types.json");
            var section = GoldDataLoader.GetForceSectionByIndex(gold, 0);

            var entity = ForceSectionEntityBuilder.Create(m_Manager, section);

            _buildSystem.Update(World.Unmanaged);
            _ecbSystem.Update(World.Unmanaged);

            var points = m_Manager.GetBuffer<Point>(entity);
            PointComparer.AssertPointsMatch(points, section.outputs.points);
        }
    }
}
