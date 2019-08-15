using System;
using System.IO;
using System.Net;

namespace PaintDotNet.Data.MbmFileType
{
    class MbmBinaryReader
    {
        private BinaryReader reader;

        public MbmBinaryReader(System.IO.Stream stream)
        {
            reader = new BinaryReader(stream);
        }

        public UInt32 Seek(UInt32 offset)
        {
            reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            return (UInt32)reader.BaseStream.Position;
        }

        public UInt32 Tell()
        {
            return (UInt32)reader.BaseStream.Position;
        }

        public UInt32 ReadUInt32()
        {
            if (BitConverter.IsLittleEndian)
                return reader.ReadUInt32();

            return (UInt32)IPAddress.HostToNetworkOrder((Int32)reader.ReadUInt32());
        }

        public Int32 ReadInt32()
        {
            if (BitConverter.IsLittleEndian)
                return reader.ReadInt32();

            return IPAddress.HostToNetworkOrder(reader.ReadInt32());
        }

        public System.Drawing.Size ReadSize()
        {
            return new System.Drawing.Size(ReadInt32(), ReadInt32());
        }
    }
}