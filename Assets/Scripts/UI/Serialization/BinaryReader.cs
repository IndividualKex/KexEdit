using System;
using Unity.Burst;
using Unity.Collections;

namespace KexEdit.UI.Serialization {
    [BurstCompile]
    public struct BinaryReader {
        private readonly NativeArray<byte> _buffer;
        private int _position;

        public int Position => _position;
        public int RemainingBytes => _buffer.Length - _position;

        public BinaryReader(NativeArray<byte> buffer) {
            _buffer = buffer;
            _position = 0;
        }

        [BurstCompile]
        public T Read<T>() where T : unmanaged {
            var tempArray = new NativeArray<T>(1, Allocator.Temp);
            var bytes = new NativeSlice<T>(tempArray).SliceConvert<byte>();
            CheckCapacity(bytes.Length);
            var sourceSlice = new NativeSlice<byte>(_buffer, _position, bytes.Length);
            bytes.CopyFrom(sourceSlice);
            _position += bytes.Length;
            T result = tempArray[0];
            tempArray.Dispose();
            return result;
        }

        [BurstCompile]
        public void ReadArray<T>(out NativeArray<T> output, Allocator allocator) where T : unmanaged {
            int length = Read<int>();
            output = new(length, allocator);
            if (length > 0) {
                var bytes = new NativeSlice<T>(output).SliceConvert<byte>();
                CheckCapacity(bytes.Length);
                var sourceSlice = new NativeSlice<byte>(_buffer, _position, bytes.Length);
                bytes.CopyFrom(sourceSlice);
                _position += bytes.Length;
            }
        }

        [BurstCompile]
        private void CheckCapacity(int size) {
            if (_position + size > _buffer.Length) {
                throw new InvalidOperationException($"Buffer overflow: need {size} bytes, have {_buffer.Length - _position}");
            }
        }
    }
}
