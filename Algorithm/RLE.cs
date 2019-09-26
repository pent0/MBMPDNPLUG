using System;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace PaintDotNet.Data.MbmFileType.Algorithm
{
    class RLEDecompressor
    {
        /// <summary>
        /// Decompress twenty-four bits RLE stream.
        /// </summary>
        /// <param name="dest">Destination stream</param>
        /// <param name="source">Source stream</param>
        public static void DecompressTwentyFourBitsRLE(Stream dest, Stream source, int sourceSize)
        {
            long sourceMax = source.Position + sourceSize;
            UInt32 decompressedSize = 0;

            while (source.Position != sourceMax && dest.Position != dest.Length)
            {
                SByte count = (SByte)source.ReadByte();

                if (count >= 0)
                {
                    // This is real count subtract 1
                    count = (SByte)Math.Min((Int32)(count), (Int32)((dest.Length - dest.Position) / 3));
                    Byte compr = (Byte)source.ReadByte();
                    Byte compg = (Byte)source.ReadByte();
                    Byte compb = (Byte)source.ReadByte();

                    decompressedSize += (UInt32)(3 * (count + 1));

                    for (Byte i = 0; i <= count; i++)
                    {
                        dest.WriteByte(compr);
                        dest.WriteByte(compg);
                        dest.WriteByte(compb);
                    }
                } else
                {
                    Int32 totalBytesTocopy = Math.Min((Int32)(count) * -3, (Int32)(dest.Length - dest.Position));
                    decompressedSize += (UInt32)totalBytesTocopy;

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
        public static void Decompress(Stream dest, Stream source, int sourceSize, BitmapCompression compression)
        {
            switch (compression)
            {
                case BitmapCompression.TwentyFourBitsRLE:
                    DecompressTwentyFourBitsRLE(dest, source, sourceSize);

                    using (var stream = new FileStream("test.bin", FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        var last = dest.Position;
                        dest.Position = 0;
                        dest.CopyTo(stream);
                        dest.Position = last;
                    }

                    break;

                default:
                    throw new MbmException(String.Format("Unsupported decompression type: {0}", compression.ToString()));
            }
        }
    }

    class RLECompressor
    {
        static bool IsXBytesFowardEqual(Stream source, Byte []comp)
        {
            for (int i = 0; i < comp.Length; i++)
            {
                if ((Byte)source.ReadByte() != comp[i])
                {
                    source.Seek(-i - 1, SeekOrigin.Current);
                    return false;
                }
            }

            return true;
        }

#if DEBUG
        /// <summary>
        /// Use this function to debug compressed data when the original doesn't match to decompressed source.
        /// Comparing the lastest compressed data with lastest target compressed source.
        /// 
        /// The function will throw an exception when the data doesn't match.
        /// </summary>
        /// <param name="dest">The compressed stream to check.</param>
        /// <param name="original">The original data stream that was compressed.</param>
        /// <param name="bpp">Bit per pixels.</param>
        /// <param name="originalSize">The size of original data.</param>
        /// <param name="compressedSize">The compressed data size.</param>
        static void DebugCompressedWithOriginal(Stream dest, Stream original, int bpp, UInt32 originalSize, UInt32 compressedSize)
        {
            long currentPosDest = dest.Position;
            long currentPosSource = original.Position;

            dest.Seek(-compressedSize, SeekOrigin.Current);

            Byte[] expectedData = new Byte[originalSize];
            MemoryStream temporaryStream = new MemoryStream(expectedData);

            switch (bpp)
            {
                case 24:
                    RLEDecompressor.DecompressTwentyFourBitsRLE(temporaryStream, dest, (int)compressedSize);
                    break;

                default:
                    break;
            }

            temporaryStream.Position = 0;
            original.Seek(-originalSize, SeekOrigin.Current);

            Byte[] originalData = new Byte[originalSize];
            original.Read(originalData, 0, (int)originalSize);

            for (int i = 0; i < expectedData.Length; i++)
            {
                if (originalData[i] != expectedData[i])
                {
                    throw new InvalidDataException(String.Format("Compressed data when decompress doesn't match to what " +
                        "expected. Offset source start go wrong is {0}", i + currentPosSource - originalSize));
                }
            }

            if (!originalData.SequenceEqual(expectedData))
            {
            }

            dest.Seek(currentPosDest, SeekOrigin.Begin);
        }
#endif

        static void CompressTwentyFourBitsRLE(Stream dest, Stream source, int size)
        {
            using (var stream = new FileStream("source.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                source.CopyTo(stream);
            }

            source.Position = 0;

            long sourceEnd = source.Position + size - 3;
            BinaryWriter destWriter = new BinaryWriter(dest);
            UInt32 totalDecompress = 0;
            UInt32 totalCompressThisIteration = 0;

            while (source.Position != sourceEnd)
            {
                long lastPos = source.Position;
                totalCompressThisIteration = 0;

                Byte comp1 = (Byte)source.ReadByte();
                Byte comp2 = (Byte)source.ReadByte();
                Byte comp3 = (Byte)source.ReadByte();

                uint packCount = 0;

                while (source.Position != sourceEnd && IsXBytesFowardEqual(source, new byte[] { comp1, comp2, comp3 }))
                    packCount++;

                if (packCount != 0)
                {
                    while (packCount >= 128)
                    {
                        destWriter.Write((Byte)127);
                        destWriter.Write(comp1);
                        destWriter.Write(comp2);
                        destWriter.Write(comp3);

                        packCount -= 128;
                        totalDecompress += 128 * 3;
#if DEBUG
                        totalCompressThisIteration += 4;
#endif
                    }

                    destWriter.Write((Byte)packCount);
                    destWriter.Write(comp1);
                    destWriter.Write(comp2);
                    destWriter.Write(comp3);
                    totalDecompress += (packCount + 1) * 3;

#if DEBUG
                    totalCompressThisIteration += 4;
                    DebugCompressedWithOriginal(dest, source, 24, (uint)(source.Position - lastPos), totalCompressThisIteration);
#endif
                }
                else
                {
                    while (source.Position != sourceEnd)
                    {
                        if (IsXBytesFowardEqual(source, new byte[] { comp1, comp2, comp3 }))
                        {
                            // Duplicate pair at the end. Should leave it later
                            source.Seek(-6, SeekOrigin.Current);
                            break;
                        }

                        comp1 = (Byte)source.ReadByte();
                        comp2 = (Byte)source.ReadByte();
                        comp3 = (Byte)source.ReadByte();
                    }

                    // Total bytes to get
                    packCount = (UInt32)((source.Position - lastPos) / 3);
                    long currentSource = source.Position;
                    source.Seek(lastPos, SeekOrigin.Begin);

                    byte[] dataToCopy = new byte[128 * 3];

                    while (packCount > 128)
                    {
                        destWriter.Write((SByte)(-128));
                        source.Read(dataToCopy, 0, 128 * 3);
                        destWriter.Write(dataToCopy, 0, 128 * 3);

                        packCount -= 128;
                        totalDecompress += 128 * 3;
                    }

                    destWriter.Write((SByte)(-packCount));
                    source.Read(dataToCopy, 0, (int)(packCount * 3));
                    destWriter.Write(dataToCopy, 0, (int)(packCount * 3));

                    Debug.Assert(source.Position == currentSource);

                    totalDecompress += packCount * 3;
                }
            }
        }

        /// <summary>
        /// Try to compress RLE stream, with given bitmap compression type.
        /// </summary>
        /// <param name="dest">The destination stream that will contains compressed pixel data.</param>
        /// <param name="source">The source stream we want to compress from.</param>
        /// <param name="sourceSize">The size of the source.</param>
        /// <param name="compression">The compression of destination stream.</param>
        public static void Compress(Stream dest, Stream source, int sourceSize, BitmapCompression compression)
        {
            switch (compression)
            {
                case BitmapCompression.TwentyFourBitsRLE:
                    CompressTwentyFourBitsRLE(dest, source, sourceSize);
                    break;

                default:
                    throw new MbmException(String.Format("Unsupported decompression type: {0}", compression.ToString()));
            }
        }
    }
}
