using System;
using System.IO;
using static System.BitConverter;
using static System.Text.Encoding;

namespace Clentaminator
{
    public class BigEndianReader
    {
        private static BinaryReader _reader = new BinaryReader(Stream.Null);

        public BigEndianReader(Stream input) => _reader = new BinaryReader(input);

        public byte[] ReadBytes(int count) => _reader.ReadBytes(count);

        public string ReadString(ushort lenght) => Default.GetString(_reader.ReadBytes(lenght));
        
        public ushort ReadUInt8() => ToUInt16(Flip(_reader.ReadBytes(1)),0);
        
        public ushort ReadUInt16() => ToUInt16(Flip(_reader.ReadBytes(2)),0);
        public uint ReadUInt32() => ToUInt32(Flip(_reader.ReadBytes(4)),0);

        public short ReadInt8() => _reader.ReadByte();
        
        public short ReadInt16() => ToInt16(Flip(_reader.ReadBytes(2)),0);

        public int ReadInt32() => ToInt32(Flip(_reader.ReadBytes(4)),0);

        public float ReadFloat() => ToSingle(Flip(_reader.ReadBytes(4)),0);

        public T ReadEnum<T>() where T : Enum => (T)(object)(int)_reader.ReadByte();

        public byte ReadByte() => _reader.ReadByte();

        private static byte[] Flip(byte[] value)
        {
            Array.Reverse(value);
            return value;
        }

        public long Length()
        {
            return _reader.BaseStream.Length;
        }

        public long Position()
        {
            return _reader.BaseStream.Position;
        }
    
    }
}
