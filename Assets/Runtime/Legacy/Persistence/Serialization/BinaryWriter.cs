using System;
using Unity.Burst;
using Unity.Collections;

namespace KexEdit.Legacy.Serialization {
    [BurstCompile]
    public struct BinaryWriter {
        private readonly NativeArray<byte> _buffer;
        private int _position;

        public int Position => _position;
        public int RemainingCapacity => _buffer.Length - _position;

        public BinaryWriter(NativeArray<byte> buffer) {
            _buffer = buffer;
            _position = 0;
        }

        public void Write<T>(T value) where T : unmanaged {
            var tempArray = new NativeArray<T>(1, Allocator.Temp);
            tempArray[0] = value;
            var bytes = new NativeSlice<T>(tempArray).SliceConvert<byte>();
            WriteBytes(bytes);
            tempArray.Dispose();
        }

        public void WriteArray<T>(NativeArray<T> array) where T : unmanaged {
            Write(array.Length);
            if (array.Length > 0) {
                var bytes = new NativeSlice<T>(array).SliceConvert<byte>();
                WriteBytes(bytes);
            }
        }

        private void WriteBytes(NativeSlice<byte> bytes) {
            CheckCapacity(bytes.Length);
            bytes.CopyTo(_buffer.GetSubArray(_position, bytes.Length));
            _position += bytes.Length;
        }

        private void CheckCapacity(int size) {
            if (_position + size > _buffer.Length) {
                throw new InvalidOperationException($"Buffer overflow: need {size} bytes, have {_buffer.Length - _position}");
            }
        }
    }
}
