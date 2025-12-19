using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace KexEdit.Persistence {
    [BurstCompile]
    public struct ChunkReader : IDisposable {
        private NativeArray<byte> _data;
        private int _position;

        public ChunkReader(NativeArray<byte> data) {
            _data = data;
            _position = 0;
        }

        public int Position => _position;
        public int Length => _data.Length;
        public bool HasData => _position < _data.Length;

        public bool TryReadHeader(out ChunkHeader header) {
            if (_position + ChunkHeader.Size > _data.Length) {
                header = default;
                return false;
            }

            var type = ReadFixedString4();
            uint version = ReadUInt();
            uint length = ReadUInt();

            header = new ChunkHeader(type, version, length);
            return true;
        }

        private FixedString32Bytes ReadFixedString4() {
            var result = new FixedString32Bytes();
            for (int i = 0; i < 4; i++) {
                byte b = _data[_position++];
                if (b != 0) result.Append((char)b);
            }
            return result;
        }

        public void SkipChunk(ChunkHeader header) {
            _position += (int)header.Length;
        }

        public byte ReadByte() {
            return _data[_position++];
        }

        public uint ReadUInt() {
            unsafe {
                byte* ptr = (byte*)_data.GetUnsafeReadOnlyPtr() + _position;
                uint value = *(uint*)ptr;
                _position += 4;
                return value;
            }
        }

        public int ReadInt() {
            unsafe {
                byte* ptr = (byte*)_data.GetUnsafeReadOnlyPtr() + _position;
                int value = *(int*)ptr;
                _position += 4;
                return value;
            }
        }

        public ulong ReadULong() {
            unsafe {
                byte* ptr = (byte*)_data.GetUnsafeReadOnlyPtr() + _position;
                ulong value = *(ulong*)ptr;
                _position += 8;
                return value;
            }
        }

        public float ReadFloat() {
            unsafe {
                byte* ptr = (byte*)_data.GetUnsafeReadOnlyPtr() + _position;
                float value = *(float*)ptr;
                _position += 4;
                return value;
            }
        }

        public float2 ReadFloat2() {
            return new float2(ReadFloat(), ReadFloat());
        }

        public float3 ReadFloat3() {
            return new float3(ReadFloat(), ReadFloat(), ReadFloat());
        }

        public bool ReadBool() {
            return _data[_position++] != 0;
        }

        public NativeArray<T> ReadArray<T>(int count, Allocator allocator) where T : unmanaged {
            var result = new NativeArray<T>(count, allocator);
            unsafe {
                int byteSize = count * UnsafeUtility.SizeOf<T>();
                UnsafeUtility.MemCpy(result.GetUnsafePtr(), (byte*)_data.GetUnsafeReadOnlyPtr() + _position, byteSize);
                _position += byteSize;
            }
            return result;
        }

        public NativeArray<T> ReadArrayWithLength<T>(Allocator allocator) where T : unmanaged {
            int count = ReadInt();
            return ReadArray<T>(count, allocator);
        }

        public void Dispose() {
            // Reader doesn't own the data, so nothing to dispose
        }
    }
}
