using System;
using System.IO;
using System.Net;

namespace PaintDotNet.Data.MbmFileType
{
    class MbmBinaryWriter
    {
        private BinaryWriter writer;

        public MbmBinaryWriter(System.IO.Stream stream)
        {
            writer = new BinaryWriter(stream);
        }

        public UInt32 Seek(UInt32 offset)
        {
            writer.BaseStream.Seek(offset, SeekOrigin.Begin);
            return (UInt32)writer.BaseStream.Position;
        }

        public UInt32 Tell()
        {
            return (UInt32)writer.BaseStream.Position;
        }

        public void WriteUInt32(UInt32 val)
        {
            if (BitConverter.IsLittleEndian)
                writer.Write(val);
            else
                writer.Write(IPAddress.NetworkToHostOrder(val));
        }

        public void WriteInt32(Int32 val)
        {
            if (BitConverter.IsLittleEndian)
                writer.Write(val);
            else
                writer.Write(IPAddress.NetworkToHostOrder(val));
        }

        public void WriteSize(System.Drawing.Size size)
        {
            WriteInt32(size.Width);
            WriteInt32(size.Height);
        }
    }
}