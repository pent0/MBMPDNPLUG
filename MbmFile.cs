using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;

namespace PaintDotNet.Data.MbmFileType
{
    /// <summary>
    /// Header of an MBM file. Contains basic information of bitmap and its section.
    /// </summary>
    public struct MbmHeader
    {
        public const UInt32 directFileStoreUIDNum = 0x10000037;
        public const UInt32 multiBitmapUIDNum = 0x10000042;

        public UID directFileStoreUID;
        public UID multiBitmapUID;
        public UID customUID;

        public UInt32 uidCheckSum;
        public UInt32 trailerOff;

        unsafe public MbmHeader(UID uid3, UInt32 trailerOffset)
        {
            directFileStoreUID = new UID(directFileStoreUIDNum);
            multiBitmapUID = new UID(multiBitmapUIDNum);
            customUID = uid3;

            byte[] uidsBytes = new byte[3 * sizeof(UInt32)];

            if (BitConverter.IsLittleEndian)
            {
                uidsBytes[0] = 0x10;
                uidsBytes[3] = 0x37;
                uidsBytes[4] = 0x10;
                uidsBytes[7] = 0x42;
            }
            else
            {
                uidsBytes[0] = 0x37;
                uidsBytes[3] = 0x10;
                uidsBytes[4] = 0x42;
                uidsBytes[7] = 0x10;
            }

            byte[] uid3Bytes = BitConverter.GetBytes(uid3.value);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(uid3Bytes);

            Buffer.BlockCopy(uid3Bytes, 0, uidsBytes, 8, sizeof(UInt32));

            // TODO: This is not correct, maybe we just need another CRC table...
            uidCheckSum = CRC32.Calculate(uidsBytes);
            trailerOff = trailerOffset;
        }
    }

    /// <summary>
    /// Trailer section of MBM file, contains a list of offsets to each bitmap header,
    /// inside the Multi-bitmap file.
    /// </summary>
    public class MbmTrailer: List<UInt32>
    {
        public MbmTrailer(UInt32 count)
            : base((int)count)
        {

        }
    }

    /// <summary>
    /// Header of a bitmap inside the multi-bitmap file.
    /// </summary>
    struct SBMHeader
    {
        /// <summary>
        /// Size of bitmap, including bitmap data and bitmap header.
        /// </summary>
        public UInt32 bitmapSize;

        /// <summary>
        /// Length of the header.
        /// </summary>
        public UInt32 headerLength;

        /// <summary>
        /// Size of the bitmap, in pixels.
        /// </summary>
        public Size sizeInPixel;

        /// <summary>
        /// Size of the bitmap, in twips.
        /// </summary>
        public Size sizeInTwips;

        /// <summary>
        /// Size of a pixel, in bits.
        /// </summary>
        public UInt32 bitsPerPixel;

        /// <summary>
        /// Quick summary of color in bitmap.
        /// </summary>
        public BitmapColor colorMode;

        /// <summary>
        /// Size of the palette, if available.
        /// </summary>
        public UInt32 paletteSize;

        /// <summary>
        /// The compression mode that bitmap uses.
        /// </summary>
        public BitmapCompression compression;
    }

    /// <summary>
    /// Represent a Multi-bitmap file.
    /// </summary>
    class MbmFile
    {
        MbmHeader header;
        MbmTrailer trailer;
        List<SBMHeader> bitmapHeaders;

        public static readonly UInt32 MaxBitmapCount = 150;

        public MbmHeader Header
        {
            get
            {
                return header;
            }
        }

        public int BitmapCount
        {
            get
            {
                return bitmapHeaders.Count;
            }
        }

        public SBMHeader GetBitmapHeader(int index)
        {
            return bitmapHeaders[index];
        }

        public UInt32 GetBitmapHeaderOffset(int index)
        {
            return trailer[index];
        }
        public static Int32 GetStride(Int32 width, Byte bpp)
        {
            Int32 wordWidth = 0;

            switch (bpp)
            {
                case 1:
                    wordWidth = (width + 31) / 32;
                    break;

                case 2:
                    wordWidth = (width + 15) / 16;
                    break;

                case 4:
                    wordWidth = (width + 7) / 8;
                    break;

                case 8:
                    wordWidth = (width + 3) / 4;
                    break;

                case 16:
                    wordWidth = (width + 1) / 2;
                    break;

                case 24:
                    wordWidth = (((width * 3) + 11) / 12) * 3;
                    break;

                case 32:
                    wordWidth = width;
                    break;

                default:
                    throw new MbmException(String.Format("Unsupported bpp for getting stride ({0})", bpp));
            }

            return wordWidth * 4;
        }

        public MbmFile(System.IO.Stream input)
        {
            Load(input);
        }

        void Load(System.IO.Stream input)
        {
            MbmBinaryReader bin = new MbmBinaryReader(input);
            ReadHeader(bin);
            ReadTrailer(bin);

            bitmapHeaders = new List<SBMHeader>(trailer.Count);

            for (int i = 0; i < trailer.Count; i++)
            {
                bitmapHeaders.Add(ReadSingleBitmapHeader(bin, trailer[i]));
            }
        }

        void ReadHeader(MbmBinaryReader input)
        {
            UInt32 uid1 = input.ReadUInt32();
            UInt32 uid2 = input.ReadUInt32();
            UInt32 uid3 = input.ReadUInt32();

            if (uid1 != MbmHeader.directFileStoreUIDNum || uid2 != MbmHeader.multiBitmapUIDNum)
            {
                throw new MbmException("UID1 and UID2 of MBM header has invalid value!");
            }

            // No need for the uid1 and uid2 anymore
            UInt32 uidChecksum = input.ReadUInt32();
            UInt32 trailerOffset = input.ReadUInt32();

            header = new MbmHeader(new UID(uid3), trailerOffset);
        }

        void ReadTrailer(MbmBinaryReader input)
        {
            // Seek to trailer
            UInt32 lastPos = input.Tell();
            input.Seek(header.trailerOff);

            UInt32 count = input.ReadUInt32();

            if (count > MaxBitmapCount || count == 0)
            {
                String message = String.Format("Bitmap count in MBM file doesn't make sense ({0}). " +
                    "It should not be equal to 0 and must not be larger than {1}", count, MaxBitmapCount);

                throw new MbmException(message);
            }

            trailer = new MbmTrailer(count);

            for (UInt32 i = 0; i < count; i++)
            {
                trailer.Add(input.ReadUInt32());
            }

            input.Seek(lastPos);
        }

        SBMHeader ReadSingleBitmapHeader(MbmBinaryReader input, UInt32 offset)
        {
            SBMHeader bitmapHeader = new SBMHeader();

            // Seek to trailer
            UInt32 lastPos = input.Tell();
            input.Seek(offset);

            bitmapHeader.bitmapSize = input.ReadUInt32();
            bitmapHeader.headerLength = input.ReadUInt32();
            bitmapHeader.sizeInPixel = input.ReadSize();
            bitmapHeader.sizeInTwips = input.ReadSize();
            bitmapHeader.bitsPerPixel = input.ReadUInt32();
            bitmapHeader.colorMode = (BitmapColor)input.ReadUInt32();
            bitmapHeader.paletteSize = input.ReadUInt32();
            bitmapHeader.compression = (BitmapCompression)input.ReadUInt32();

            input.Seek(lastPos);

            return bitmapHeader;
        }
    }
}