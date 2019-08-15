using System;
using System.IO;

namespace PaintDotNet.Data.MbmFileType.Algorithm
{
    class RLEDecompressor
    {
        /// <summary>
        /// Decompress twenty-four bits RLE stream.
        /// </summary>
        /// <param name="dest">Destination stream</param>
        /// <param name="source">Source stream</param>
        public static void DecompressTwentyFourBitsRLE(Stream dest, Stream source)
        {
            while (source.Position != source.Length && dest.Position != dest.Length)
            {
                SByte count = (SByte)source.ReadByte();

                if (count >= 0)
                {
                    // This is real count subtract 1
                    count = (SByte)Math.Min((Int32)(count), (Int32)((dest.Length - dest.Position) / 3));
                    Byte compr = (Byte)source.ReadByte();
                    Byte compg = (Byte)source.ReadByte();
                    Byte compb = (Byte)source.ReadByte();

                    for (Byte i = 0; i <= count; i++)
                    {
                        dest.WriteByte(compr);
                        dest.WriteByte(compg);
                        dest.WriteByte(compb);
                    }
                } else
                {
                    Int32 totalBytesTocopy = Math.Min((Int32)(count) * -3, (Int32)(dest.Length - dest.Position));
                    byte[] bytesToCopy = new byte[totalBytesTocopy];

                    source.Read(bytesToCopy, 0, totalBytesTocopy);
                    dest.Write(bytesToCopy, 0, totalBytesTocopy);
                }
            }
        }

        /// <summary>
        /// Try to decompress RLE stream, with given bitmap compression type.
        /// </summary>
        /// <param name="dest">The destination stream that will contains decompressed pixel data.</param>
        /// <param name="source">The source stream we want to decompress from.</param>
        /// <param name="compression">The compression of source stream.</param>
        public static void Decompress(Stream dest, Stream source, BitmapCompression compression)
        {
            switch (compression)
            {
                case BitmapCompression.TwentyFourBitsRLE:
                    DecompressTwentyFourBitsRLE(dest, source);
                    break;

                default:
                    throw new MbmException(String.Format("Unsupported decompression type: {0}", compression.ToString()));
            }
        }
    }
}
