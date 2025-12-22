using KexEdit.Persistence;
using NUnit.Framework;
using Unity.Collections;

namespace Tests {
    [TestFixture]
    public class ChunkSerializationTests {
        [Test]
        public void ChunkHeader_RoundTrip_PreservesData() {
            var header = new ChunkHeader("TEST", 42, 1024);

            Assert.AreEqual("TEST", header.TypeString);
            Assert.AreEqual(42u, header.Version);
            Assert.AreEqual(1024u, header.Length);
        }

        [Test]
        public void ChunkWriter_WriteHeader_CorrectBytes() {
            using var writer = new ChunkWriter(Allocator.Temp);
            writer.BeginChunk("TEST", 1);
            writer.WriteUInt(12345);
            writer.EndChunk();

            var data = writer.ToArray();
            Assert.AreEqual(ChunkHeader.Size + 4, data.Length);
        }

        [Test]
        public void ChunkWriter_MultipleChunks_CorrectLayout() {
            using var writer = new ChunkWriter(Allocator.Temp);

            writer.BeginChunk("AAA1", 1);
            writer.WriteUInt(100);
            writer.EndChunk();

            writer.BeginChunk("BBB2", 2);
            writer.WriteUInt(200);
            writer.WriteUInt(300);
            writer.EndChunk();

            var data = writer.ToArray();
            Assert.AreEqual((ChunkHeader.Size + 4) + (ChunkHeader.Size + 8), data.Length);
        }

        [Test]
        public void ChunkReader_ReadHeader_MatchesWritten() {
            using var writer = new ChunkWriter(Allocator.Temp);
            writer.BeginChunk("TEST", 5);
            writer.WriteUInt(42);
            writer.EndChunk();

            var data = writer.ToArray();
            var reader = new ChunkReader(data);

            Assert.IsTrue(reader.TryReadHeader(out var header));
            Assert.AreEqual("TEST", header.TypeString);
            Assert.AreEqual(5u, header.Version);
            Assert.AreEqual(4u, header.Length);
        }

        [Test]
        public void ChunkReader_ReadContent_MatchesWritten() {
            using var writer = new ChunkWriter(Allocator.Temp);
            writer.BeginChunk("DATA", 1);
            writer.WriteUInt(12345);
            writer.WriteFloat(3.14f);
            writer.EndChunk();

            var data = writer.ToArray();
            var reader = new ChunkReader(data);

            Assert.IsTrue(reader.TryReadHeader(out var header));
            Assert.AreEqual(12345u, reader.ReadUInt());
            Assert.AreEqual(3.14f, reader.ReadFloat(), 0.0001f);
        }

        [Test]
        public void ChunkReader_SkipChunk_AdvancesToNext() {
            using var writer = new ChunkWriter(Allocator.Temp);

            writer.BeginChunk("SKIP", 1);
            writer.WriteUInt(111);
            writer.WriteUInt(222);
            writer.EndChunk();

            writer.BeginChunk("READ", 2);
            writer.WriteUInt(333);
            writer.EndChunk();

            var data = writer.ToArray();
            var reader = new ChunkReader(data);

            Assert.IsTrue(reader.TryReadHeader(out var header1));
            Assert.AreEqual("SKIP", header1.TypeString);
            reader.SkipChunk(header1);

            Assert.IsTrue(reader.TryReadHeader(out var header2));
            Assert.AreEqual("READ", header2.TypeString);
            Assert.AreEqual(333u, reader.ReadUInt());
        }

        [Test]
        public void ChunkWriter_NestedChunks_CorrectLengths() {
            using var writer = new ChunkWriter(Allocator.Temp);

            writer.BeginChunk("CORE", 1);
            {
                writer.BeginChunk("GRPH", 1);
                writer.WriteUInt(10);
                writer.EndChunk();

                writer.BeginChunk("DATA", 1);
                writer.WriteUInt(20);
                writer.WriteUInt(30);
                writer.EndChunk();
            }
            writer.EndChunk();

            var data = writer.ToArray();
            var reader = new ChunkReader(data);

            Assert.IsTrue(reader.TryReadHeader(out var coreHeader));
            Assert.AreEqual("CORE", coreHeader.TypeString);

            int expectedCoreLength = (ChunkHeader.Size + 4) + (ChunkHeader.Size + 8);
            Assert.AreEqual(expectedCoreLength, (int)coreHeader.Length);
        }

        [Test]
        public void ChunkWriter_WriteArray_RoundTrip() {
            var values = new NativeArray<uint>(3, Allocator.Temp);
            values[0] = 100;
            values[1] = 200;
            values[2] = 300;

            using var writer = new ChunkWriter(Allocator.Temp);
            writer.BeginChunk("ARR1", 1);
            writer.WriteArray(values);
            writer.EndChunk();

            var data = writer.ToArray();
            var reader = new ChunkReader(data);

            Assert.IsTrue(reader.TryReadHeader(out _));
            var read = reader.ReadArrayWithLength<uint>(Allocator.Temp);

            Assert.AreEqual(100u, read[0]);
            Assert.AreEqual(200u, read[1]);
            Assert.AreEqual(300u, read[2]);

            read.Dispose();
            values.Dispose();
        }
    }
}
