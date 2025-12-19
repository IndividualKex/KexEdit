using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace KexEdit.Persistence {
    [BurstCompile]
    public struct ChunkWriter : IDisposable {
        private NativeList<byte> _buffer;
        private NativeList<int> _chunkStack;
        private Allocator _allocator;

        public ChunkWriter(Allocator allocator) {
            _buffer = new NativeList<byte>(1024, allocator);
            _chunkStack = new NativeList<int>(8, allocator);
            _allocator = allocator;
        }

        public void BeginChunk(string type, uint version) {
            BeginChunk(new FixedString32Bytes(type), version);
        }

        public void BeginChunk(FixedString32Bytes type, uint version) {
            int startPos = _buffer.Length;
            _chunkStack.Add(startPos);

            WriteFixedString4(type);
            WriteUInt(version);
            WriteUInt(0); // Placeholder for length
        }

        public void EndChunk() {
            int startPos = _chunkStack[_chunkStack.Length - 1];
            _chunkStack.RemoveAt(_chunkStack.Length - 1);

            int contentLength = _buffer.Length - startPos - ChunkHeader.Size;

            unsafe {
                byte* ptr = (byte*)_buffer.GetUnsafePtr() + startPos + 8;
                *(uint*)ptr = (uint)contentLength;
            }
        }

        private void WriteFixedString4(FixedString32Bytes type) {
            unsafe {
                byte* typeBytes = (byte*)&type + 2; // Skip length bytes
                for (int i = 0; i < 4; i++) {
                    _buffer.Add(i < type.Length ? typeBytes[i] : (byte)0);
                }
            }
        }

        public void WriteByte(byte value) {
            _buffer.Add(value);
        }

        public void WriteUInt(uint value) {
            unsafe {
                byte* bytes = (byte*)&value;
                _buffer.Add(bytes[0]);
                _buffer.Add(bytes[1]);
                _buffer.Add(bytes[2]);
                _buffer.Add(bytes[3]);
            }
        }

        public void WriteInt(int value) {
            unsafe {
                byte* bytes = (byte*)&value;
                _buffer.Add(bytes[0]);
                _buffer.Add(bytes[1]);
                _buffer.Add(bytes[2]);
                _buffer.Add(bytes[3]);
            }
        }

        public void WriteULong(ulong value) {
            unsafe {
                byte* bytes = (byte*)&value;
                for (int i = 0; i < 8; i++) {
                    _buffer.Add(bytes[i]);
                }
            }
        }

        public void WriteFloat(float value) {
            unsafe {
                byte* bytes = (byte*)&value;
                _buffer.Add(bytes[0]);
                _buffer.Add(bytes[1]);
                _buffer.Add(bytes[2]);
                _buffer.Add(bytes[3]);
            }
        }

        public void WriteFloat2(float2 value) {
            WriteFloat(value.x);
            WriteFloat(value.y);
        }

        public void WriteFloat3(float3 value) {
            WriteFloat(value.x);
            WriteFloat(value.y);
            WriteFloat(value.z);
        }

        public void WriteBool(bool value) {
            _buffer.Add(value ? (byte)1 : (byte)0);
        }

        public void WriteArray<T>(NativeArray<T> array) where T : unmanaged {
            WriteInt(array.Length);
            unsafe {
                int byteSize = array.Length * UnsafeUtility.SizeOf<T>();
                byte* ptr = (byte*)array.GetUnsafeReadOnlyPtr();
                for (int i = 0; i < byteSize; i++) {
                    _buffer.Add(ptr[i]);
                }
            }
        }

        public void WriteArrayRaw<T>(NativeArray<T> array) where T : unmanaged {
            unsafe {
                int byteSize = array.Length * UnsafeUtility.SizeOf<T>();
                byte* ptr = (byte*)array.GetUnsafeReadOnlyPtr();
                for (int i = 0; i < byteSize; i++) {
                    _buffer.Add(ptr[i]);
                }
            }
        }

        public NativeArray<byte> ToArray() {
            var result = new NativeArray<byte>(_buffer.Length, _allocator);
            NativeArray<byte>.Copy(_buffer.AsArray(), result);
            return result;
        }

        public void Dispose() {
            if (_buffer.IsCreated) _buffer.Dispose();
            if (_chunkStack.IsCreated) _chunkStack.Dispose();
        }
    }
}
