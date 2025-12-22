using System.Collections.Generic;
using KexEdit.Core;
using KexEdit.Nodes.Storage;
using KexGraph;
using Unity.Collections;
using Unity.Mathematics;
using CoasterData = KexEdit.Coaster.Coaster;
using Duration = KexEdit.Coaster.Duration;
using DurationType = KexEdit.Coaster.DurationType;

namespace KexEdit.Persistence {
    public static class CoasterSerializer {
        const uint FileVersion = 1;
        const uint CoreVersion = 1;
        const uint GraphVersion = 1;
        const uint DataVersion = 1;

        public static void Write(ChunkWriter writer, in CoasterData coaster) {
            Write(writer, in coaster, null);
        }

        public static void Write(ChunkWriter writer, in CoasterData coaster, IReadOnlyList<IChunkExtension> extensions) {
            WriteFileHeader(ref writer);
            WriteCoreChunk(ref writer, in coaster);

            if (extensions != null) {
                foreach (var ext in extensions) {
                    ext.Write(ref writer);
                }
            }
        }

        public static CoasterData Read(ChunkReader reader, Allocator allocator) {
            return Read(ref reader, allocator, null);
        }

        public static CoasterData Read(ref ChunkReader reader, Allocator allocator, IReadOnlyList<IChunkExtension> extensions) {
            ReadFileHeader(ref reader);
            var coaster = CoasterData.Create(allocator);

            while (reader.HasData) {
                if (!reader.TryReadHeader(out var header)) break;

                if (header.TypeString == "CORE") {
                    ReadCoreChunk(ref reader, ref coaster, allocator, header);
                } else if (extensions != null) {
                    bool handled = false;
                    foreach (var ext in extensions) {
                        if (ext.ChunkType == header.TypeString) {
                            ext.Read(ref reader, header.Version);
                            handled = true;
                            break;
                        }
                    }
                    if (!handled) reader.SkipChunk(header);
                } else {
                    reader.SkipChunk(header);
                }
            }

            return coaster;
        }

        static void WriteFileHeader(ref ChunkWriter writer) {
            writer.WriteByte((byte)'K');
            writer.WriteByte((byte)'E');
            writer.WriteByte((byte)'X');
            writer.WriteByte((byte)'D');
            writer.WriteUInt(FileVersion);
        }

        static void ReadFileHeader(ref ChunkReader reader) {
            byte k = reader.ReadByte();
            byte e = reader.ReadByte();
            byte x = reader.ReadByte();
            byte d = reader.ReadByte();

            if (k != 'K' || e != 'E' || x != 'X' || d != 'D') {
                throw new System.InvalidOperationException("Invalid file magic");
            }

            uint version = reader.ReadUInt();
        }

        static void WriteCoreChunk(ref ChunkWriter writer, in CoasterData coaster) {
            writer.BeginChunk("CORE", CoreVersion);
            WriteGraphSubChunk(ref writer, coaster.Graph);
            WriteDataSubChunk(ref writer, in coaster);
            writer.EndChunk();
        }

        static void ReadCoreChunk(ref ChunkReader reader, ref CoasterData coaster, Allocator allocator, ChunkHeader coreHeader) {
            int endPos = reader.Position + (int)coreHeader.Length;

            while (reader.Position < endPos) {
                if (!reader.TryReadHeader(out var subHeader)) break;

                if (subHeader.TypeString == "GRPH") {
                    ReadGraphSubChunk(ref reader, ref coaster.Graph, allocator, subHeader);
                } else if (subHeader.TypeString == "DATA") {
                    ReadDataSubChunk(ref reader, ref coaster, allocator, subHeader);
                } else {
                    reader.SkipChunk(subHeader);
                }
            }
        }

        static void WriteGraphSubChunk(ref ChunkWriter writer, in Graph graph) {
            writer.BeginChunk("GRPH", GraphVersion);

            writer.WriteInt(graph.NodeIds.Length);
            writer.WriteInt(graph.PortIds.Length);
            writer.WriteInt(graph.EdgeIds.Length);

            for (int i = 0; i < graph.NodeIds.Length; i++) {
                writer.WriteUInt(graph.NodeIds[i]);
                writer.WriteUInt(graph.NodeTypes[i]);
                writer.WriteFloat2(graph.NodePositions[i]);
                writer.WriteInt(graph.NodeInputCount[i]);
                writer.WriteInt(graph.NodeOutputCount[i]);
            }

            for (int i = 0; i < graph.PortIds.Length; i++) {
                writer.WriteUInt(graph.PortIds[i]);
                writer.WriteUInt(graph.PortTypes[i]);
                writer.WriteUInt(graph.PortOwners[i]);
                writer.WriteBool(graph.PortIsInput[i]);
            }

            for (int i = 0; i < graph.EdgeIds.Length; i++) {
                writer.WriteUInt(graph.EdgeIds[i]);
                writer.WriteUInt(graph.EdgeSources[i]);
                writer.WriteUInt(graph.EdgeTargets[i]);
            }

            writer.WriteUInt(graph.NextNodeId);
            writer.WriteUInt(graph.NextPortId);
            writer.WriteUInt(graph.NextEdgeId);

            writer.EndChunk();
        }

        static void ReadGraphSubChunk(ref ChunkReader reader, ref Graph graph, Allocator allocator, ChunkHeader header) {
            int nodeCount = reader.ReadInt();
            int portCount = reader.ReadInt();
            int edgeCount = reader.ReadInt();

            for (int i = 0; i < nodeCount; i++) {
                uint id = reader.ReadUInt();
                uint type = reader.ReadUInt();
                float2 position = reader.ReadFloat2();
                int inputCount = reader.ReadInt();
                int outputCount = reader.ReadInt();

                graph.NodeIds.Add(id);
                graph.NodeTypes.Add(type);
                graph.NodePositions.Add(position);
                graph.NodeInputCount.Add(inputCount);
                graph.NodeOutputCount.Add(outputCount);
            }

            for (int i = 0; i < portCount; i++) {
                uint id = reader.ReadUInt();
                uint type = reader.ReadUInt();
                uint owner = reader.ReadUInt();
                bool isInput = reader.ReadBool();

                graph.PortIds.Add(id);
                graph.PortTypes.Add(type);
                graph.PortOwners.Add(owner);
                graph.PortIsInput.Add(isInput);
            }

            for (int i = 0; i < edgeCount; i++) {
                uint id = reader.ReadUInt();
                uint source = reader.ReadUInt();
                uint target = reader.ReadUInt();

                graph.EdgeIds.Add(id);
                graph.EdgeSources.Add(source);
                graph.EdgeTargets.Add(target);
            }

            graph.NextNodeId = reader.ReadUInt();
            graph.NextPortId = reader.ReadUInt();
            graph.NextEdgeId = reader.ReadUInt();

            graph.RebuildIndexMaps();
        }

        static void WriteDataSubChunk(ref ChunkWriter writer, in CoasterData coaster) {
            writer.BeginChunk("DATA", DataVersion);

            WriteKeyframes(ref writer, coaster.Keyframes);
            WriteScalars(ref writer, coaster.Scalars);
            WriteVectors(ref writer, coaster.Vectors);
            WriteRotations(ref writer, coaster.Rotations);
            WriteDurations(ref writer, coaster.Durations);
            WriteSteering(ref writer, coaster.Steering);
            WriteAnchors(ref writer, coaster.Anchors);

            writer.EndChunk();
        }

        static void ReadDataSubChunk(ref ChunkReader reader, ref CoasterData coaster, Allocator allocator, ChunkHeader header) {
            ReadKeyframes(ref reader, ref coaster.Keyframes, allocator);
            ReadScalars(ref reader, ref coaster.Scalars);
            ReadVectors(ref reader, ref coaster.Vectors);
            ReadRotations(ref reader, ref coaster.Rotations);
            ReadDurations(ref reader, ref coaster.Durations);
            ReadSteering(ref reader, ref coaster.Steering);
            ReadAnchors(ref reader, ref coaster.Anchors);
        }

        static void WriteKeyframes(ref ChunkWriter writer, in KeyframeStore store) {
            writer.WriteInt(store.Keyframes.Length);
            for (int i = 0; i < store.Keyframes.Length; i++) {
                WriteKeyframe(ref writer, store.Keyframes[i]);
            }

            int rangeCount = store.Ranges.Count;
            writer.WriteInt(rangeCount);
            foreach (var kv in store.Ranges) {
                writer.WriteULong(kv.Key);
                writer.WriteInt(kv.Value.x);
                writer.WriteInt(kv.Value.y);
            }
        }

        static void WriteKeyframe(ref ChunkWriter writer, in Keyframe kf) {
            writer.WriteFloat(kf.Time);
            writer.WriteFloat(kf.Value);
            writer.WriteByte((byte)kf.InInterpolation);
            writer.WriteByte((byte)kf.OutInterpolation);
            writer.WriteFloat(kf.InTangent);
            writer.WriteFloat(kf.OutTangent);
            writer.WriteFloat(kf.InWeight);
            writer.WriteFloat(kf.OutWeight);
        }

        static void ReadKeyframes(ref ChunkReader reader, ref KeyframeStore store, Allocator allocator) {
            int keyframeCount = reader.ReadInt();
            for (int i = 0; i < keyframeCount; i++) {
                store.Keyframes.Add(ReadKeyframe(ref reader));
            }

            int rangeCount = reader.ReadInt();
            for (int i = 0; i < rangeCount; i++) {
                ulong key = reader.ReadULong();
                int start = reader.ReadInt();
                int length = reader.ReadInt();
                store.Ranges[key] = new int2(start, length);
            }
        }

        static Keyframe ReadKeyframe(ref ChunkReader reader) {
            float time = reader.ReadFloat();
            float value = reader.ReadFloat();
            var inInterp = (InterpolationType)reader.ReadByte();
            var outInterp = (InterpolationType)reader.ReadByte();
            float inTangent = reader.ReadFloat();
            float outTangent = reader.ReadFloat();
            float inWeight = reader.ReadFloat();
            float outWeight = reader.ReadFloat();
            return new Keyframe(time, value, inInterp, outInterp, inTangent, outTangent, inWeight, outWeight);
        }

        static void WriteScalars(ref ChunkWriter writer, in NativeHashMap<uint, float> scalars) {
            writer.WriteInt(scalars.Count);
            foreach (var kv in scalars) {
                writer.WriteUInt(kv.Key);
                writer.WriteFloat(kv.Value);
            }
        }

        static void ReadScalars(ref ChunkReader reader, ref NativeHashMap<uint, float> scalars) {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++) {
                uint key = reader.ReadUInt();
                float value = reader.ReadFloat();
                scalars[key] = value;
            }
        }

        static void WriteVectors(ref ChunkWriter writer, in NativeHashMap<uint, float3> vectors) {
            writer.WriteInt(vectors.Count);
            foreach (var kv in vectors) {
                writer.WriteUInt(kv.Key);
                writer.WriteFloat3(kv.Value);
            }
        }

        static void ReadVectors(ref ChunkReader reader, ref NativeHashMap<uint, float3> vectors) {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++) {
                uint key = reader.ReadUInt();
                float3 value = reader.ReadFloat3();
                vectors[key] = value;
            }
        }

        static void WriteRotations(ref ChunkWriter writer, in NativeHashMap<uint, float3> rotations) {
            writer.WriteInt(rotations.Count);
            foreach (var kv in rotations) {
                writer.WriteUInt(kv.Key);
                writer.WriteFloat3(kv.Value);
            }
        }

        static void ReadRotations(ref ChunkReader reader, ref NativeHashMap<uint, float3> rotations) {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++) {
                uint key = reader.ReadUInt();
                float3 value = reader.ReadFloat3();
                rotations[key] = value;
            }
        }

        static void WriteDurations(ref ChunkWriter writer, in NativeHashMap<uint, Duration> durations) {
            writer.WriteInt(durations.Count);
            foreach (var kv in durations) {
                writer.WriteUInt(kv.Key);
                writer.WriteFloat(kv.Value.Value);
                writer.WriteByte((byte)kv.Value.Type);
            }
        }

        static void ReadDurations(ref ChunkReader reader, ref NativeHashMap<uint, Duration> durations) {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++) {
                uint key = reader.ReadUInt();
                float value = reader.ReadFloat();
                var type = (DurationType)reader.ReadByte();
                durations[key] = new Duration(value, type);
            }
        }

        static void WriteSteering(ref ChunkWriter writer, in NativeHashSet<uint> steering) {
            writer.WriteInt(steering.Count);
            foreach (var id in steering) {
                writer.WriteUInt(id);
            }
        }

        static void ReadSteering(ref ChunkReader reader, ref NativeHashSet<uint> steering) {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++) {
                steering.Add(reader.ReadUInt());
            }
        }

        static void WriteAnchors(ref ChunkWriter writer, in NativeHashMap<uint, Point> anchors) {
            writer.WriteInt(anchors.Count);
            foreach (var kv in anchors) {
                writer.WriteUInt(kv.Key);
                WritePoint(ref writer, kv.Value);
            }
        }

        static void WritePoint(ref ChunkWriter writer, in Point p) {
            writer.WriteFloat3(p.HeartPosition);
            writer.WriteFloat3(p.Direction);
            writer.WriteFloat3(p.Normal);
            writer.WriteFloat3(p.Lateral);
            writer.WriteFloat(p.Velocity);
            writer.WriteFloat(p.Energy);
            writer.WriteFloat(p.NormalForce);
            writer.WriteFloat(p.LateralForce);
            writer.WriteFloat(p.HeartArc);
            writer.WriteFloat(p.SpineArc);
            writer.WriteFloat(p.HeartAdvance);
            writer.WriteFloat(p.FrictionOrigin);
            writer.WriteFloat(p.RollSpeed);
            writer.WriteFloat(p.HeartOffset);
            writer.WriteFloat(p.Friction);
            writer.WriteFloat(p.Resistance);
        }

        static void ReadAnchors(ref ChunkReader reader, ref NativeHashMap<uint, Point> anchors) {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++) {
                uint key = reader.ReadUInt();
                anchors[key] = ReadPoint(ref reader);
            }
        }

        static Point ReadPoint(ref ChunkReader reader) {
            return new Point(
                heartPosition: reader.ReadFloat3(),
                direction: reader.ReadFloat3(),
                normal: reader.ReadFloat3(),
                lateral: reader.ReadFloat3(),
                velocity: reader.ReadFloat(),
                energy: reader.ReadFloat(),
                normalForce: reader.ReadFloat(),
                lateralForce: reader.ReadFloat(),
                heartArc: reader.ReadFloat(),
                spineArc: reader.ReadFloat(),
                heartAdvance: reader.ReadFloat(),
                frictionOrigin: reader.ReadFloat(),
                rollSpeed: reader.ReadFloat(),
                heartOffset: reader.ReadFloat(),
                friction: reader.ReadFloat(),
                resistance: reader.ReadFloat()
            );
        }
    }
}
