namespace KexEdit.Persistence {
    public interface IChunkExtension {
        string ChunkType { get; }
        uint CurrentVersion { get; }
        void Write(ref ChunkWriter writer);
        void Read(ref ChunkReader reader, uint version);
        void Clear();
    }
}
