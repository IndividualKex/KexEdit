using KexEdit.Sim;
using KexEdit.Sim.Schema;
using Unity.Collections;
using Unity.Mathematics;
using DocumentData = KexEdit.Document.Document;

namespace KexEdit.Persistence {
    public static class CoasterSerializer {
        private const uint FileVersion = 1;
        private const uint CoreVersion = 1;
        private const uint DataVersion = 2;

        public static void Write(ChunkWriter writer, in DocumentData coaster) {
            WriteFileHeader(ref writer);
            WriteCoreChunk(ref writer, in coaster);
        }

        public static DocumentData Read(ref ChunkReader reader, Allocator allocator) {
            ReadFileHeader(ref reader);
            return ReadChunks(ref reader, allocator);
        }

        public static DocumentData ReadChunks(ref ChunkReader reader, Allocator allocator) {
            var coaster = DocumentData.Create(allocator);

            while (reader.HasData) {
                if (!reader.TryReadHeader(out var header)) break;

                if (header.TypeString == "CORE") {
                    ReadCoreChunk(ref reader, ref coaster, header);
                    break;
                }
                reader.SkipChunk(header);
            }

            return coaster;
        }

        private static void WriteFileHeader(ref ChunkWriter writer) {
            writer.WriteByte((byte)'K');
            writer.WriteByte((byte)'E');
            writer.WriteByte((byte)'X');
            writer.WriteByte((byte)'D');
            writer.WriteUInt(FileVersion);
        }

        private static void ReadFileHeader(ref ChunkReader reader) {
            byte k = reader.ReadByte();
            byte e = reader.ReadByte();
            byte x = reader.ReadByte();
            byte d = reader.ReadByte();

            if (k != 'K' || e != 'E' || x != 'X' || d != 'D') {
                throw new System.InvalidOperationException("Invalid file magic");
            }

            reader.ReadUInt();
        }

        private static void WriteCoreChunk(ref ChunkWriter writer, in DocumentData coaster) {
            writer.BeginChunk("CORE", CoreVersion);

            writer.BeginChunk("GRPH", GraphCodec.Version);
            GraphCodec.Write(ref writer, in coaster.Graph);
            writer.EndChunk();

            writer.BeginChunk("DATA", DataVersion);
            WriteKeyframes(ref writer, coaster.Keyframes);
            writer.WriteHashMap(in coaster.Scalars);
            writer.WriteHashMap(in coaster.Vectors);
            writer.WriteHashMap(in coaster.Flags);
            writer.EndChunk();

            writer.EndChunk();
        }

        private static void ReadCoreChunk(ref ChunkReader reader, ref DocumentData coaster, ChunkHeader coreHeader) {
            int endPos = reader.Position + (int)coreHeader.Length;

            while (reader.Position < endPos) {
                if (!reader.TryReadHeader(out var subHeader)) break;

                if (subHeader.TypeString == "GRPH") {
                    GraphCodec.Read(ref reader, ref coaster.Graph);
                }
                else if (subHeader.TypeString == "DATA") {
                    ReadKeyframes(ref reader, ref coaster.Keyframes);
                    reader.ReadHashMap(ref coaster.Scalars);
                    reader.ReadHashMap(ref coaster.Vectors);
                    reader.ReadHashMap(ref coaster.Flags);
                }
                else {
                    reader.SkipChunk(subHeader);
                }
            }
        }

        private static void WriteKeyframes(ref ChunkWriter writer, in KeyframeStore store) {
            writer.WriteInt(store.Keyframes.Length);
            for (int i = 0; i < store.Keyframes.Length; i++) {
                var kf = store.Keyframes[i];
                writer.WriteFloat(kf.Time);
                writer.WriteFloat(kf.Value);
                writer.WriteByte((byte)kf.InInterpolation);
                writer.WriteByte((byte)kf.OutInterpolation);
                writer.WriteFloat(kf.InTangent);
                writer.WriteFloat(kf.OutTangent);
                writer.WriteFloat(kf.InWeight);
                writer.WriteFloat(kf.OutWeight);
            }

            int rangeCount = store.Ranges.Count;
            writer.WriteInt(rangeCount);
            foreach (var kv in store.Ranges) {
                writer.WriteULong(kv.Key);
                writer.WriteInt(kv.Value.x);
                writer.WriteInt(kv.Value.y);
            }
        }

        private static void ReadKeyframes(ref ChunkReader reader, ref KeyframeStore store) {
            int keyframeCount = reader.ReadInt();
            for (int i = 0; i < keyframeCount; i++) {
                float time = reader.ReadFloat();
                float value = reader.ReadFloat();
                var inInterp = (InterpolationType)reader.ReadByte();
                var outInterp = (InterpolationType)reader.ReadByte();
                float inTangent = reader.ReadFloat();
                float outTangent = reader.ReadFloat();
                float inWeight = reader.ReadFloat();
                float outWeight = reader.ReadFloat();
                store.Keyframes.Add(new Keyframe(time, value, inInterp, outInterp, inTangent, outTangent, inWeight, outWeight));
            }

            int rangeCount = reader.ReadInt();
            for (int i = 0; i < rangeCount; i++) {
                ulong key = reader.ReadULong();
                int start = reader.ReadInt();
                int length = reader.ReadInt();
                store.Ranges[key] = new int2(start, length);
            }
        }
    }
}
