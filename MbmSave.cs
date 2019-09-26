using System;
using System.IO;

using PaintDotNet;
using PaintDotNet.Core;

namespace PaintDotNet.Data.MbmFileType
{
    class MbmSave
    {
        public static void Save(Document input, Stream output, Surface scratchSurface, UInt32 bpp, BitmapColor colorMode)
        {
            // Flatten the image first
            input.Flatten(scratchSurface);

            // Build up SBM Header
            SBMHeader header = new SBMHeader();
            header.headerLength = 40;
            header.sizeInPixel.Width = input.Width;
            header.sizeInPixel.Height = input.Height;
            header.sizeInTwips.Width = input.Width * 15;
            header.sizeInTwips.Height = input.Height * 15;
            header.colorMode = colorMode;
            header.bitsPerPixel = bpp;

            // Determine what compression to use
            BitmapCompression compressionToUse = ChooseBestCompressionMode(input.Width, input.Height, bpp, colorMode);
            header.compression = compressionToUse;

            Int32 stride = MbmFile.GetStride(input.Width, (Byte)bpp);

            byte[] rawBitmapData = new byte[stride * input.Height];
            byte[] compressedBitmapData = null;

            MemoryStream bitmapStream = new MemoryStream(rawBitmapData, 0, rawBitmapData.Length);
            ReadRawBitmapData(scratchSurface, bitmapStream, (Byte)bpp);

            bitmapStream.Seek(0, SeekOrigin.Begin);

            switch (compressionToUse)
            {
                case BitmapCompression comp when (comp >= BitmapCompression.ByteRLE && comp <= BitmapCompression.ThirtyTwoABitsRLE):
                    compressedBitmapData = new byte[stride * input.Height];
                    MemoryStream compressedDestStream = new MemoryStream(compressedBitmapData, 0, compressedBitmapData.Length);
                    compressedDestStream.Seek(0, SeekOrigin.Begin);
                    Algorithm.RLECompressor.Compress(compressedDestStream, bitmapStream, stride * input.Height, compressionToUse);
                    header.bitmapSize = (UInt32)(40 + compressedDestStream.Position);

                    break;

                case BitmapCompression.None:
                    header.bitmapSize = (UInt32)(40 + stride * input.Height);
                    break;

                default:
                    throw new MbmException(String.Format("Unsupported bitmap compression type {0}", compressionToUse.ToString()));
            }

            output.Seek(/*sizeof(MbmHeader)*/ 20, SeekOrigin.Begin);

            MbmBinaryWriter writer = new MbmBinaryWriter(output);

            // Try to write our header
            writer.WriteUInt32(header.bitmapSize);
            writer.WriteUInt32(header.headerLength);
            writer.WriteSize(header.sizeInPixel);
            writer.WriteSize(header.sizeInTwips);
            writer.WriteUInt32(header.bitsPerPixel);
            writer.WriteUInt32((UInt32)header.colorMode);
            writer.WriteUInt32(header.paletteSize);
            writer.WriteUInt32((UInt32)header.compression);

            // Write data
            switch (compressionToUse)
            {
                case BitmapCompression comp when (comp >= BitmapCompression.ByteRLE && comp <= BitmapCompression.ThirtyTwoABitsRLE):
                    output.Write(compressedBitmapData, 0, (int)(header.bitmapSize - header.headerLength));
                    break;

                case BitmapCompression.None:
                    output.Write(rawBitmapData, 0, rawBitmapData.Length);
                    break;

                default:
                    break;
            }

            // Write trailer
            UInt32 trailerOffset = writer.Tell();
            writer.WriteUInt32(1);
            writer.WriteUInt32(20);

            // Seek back and write our header
            writer.Seek(0);
            writer.WriteUInt32(MbmHeader.directFileStoreUIDNum);
            writer.WriteUInt32(MbmHeader.multiBitmapUIDNum);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);      // Checksum
            writer.WriteUInt32(trailerOffset);
        }

        unsafe static void ReadRawBitmapData(Surface surface, Stream destinationStream, Byte bpp)
        {
            Int32 stride = MbmFile.GetStride(surface.Width, (Byte)bpp);

            for (int y = 0; y < surface.Height; y++)
            {
                destinationStream.Position = stride * y;
                ColorBgra *row = surface.GetRowAddress(y);

                for (int x = 0; x < surface.Width; x++)
                {
                    destinationStream.WriteByte(row[x].R);

                    if (bpp >= 16)
                    {
                        destinationStream.WriteByte(row[x].G);
                    }

                    if (bpp >= 24)
                    {
                        destinationStream.WriteByte(row[x].B);
                    }

                    if (bpp >= 32)
                    {
                        destinationStream.WriteByte(row[x].A);
                    }
                }

                for (int x = surface.Width * (bpp / 8); x < stride; x++)
                {
                    destinationStream.WriteByte(255);
                }
            }
        }

        public static readonly UInt32 MaximumSizeNoCompress = 4800;

        static BitmapCompression ChooseBestCompressionMode(int width, int height, UInt32 bpp, BitmapColor colorMode)
        {
            Int32 stride = MbmFile.GetStride(width, (Byte)bpp);

            if (width * height <= MaximumSizeNoCompress)
            {
                // At this size we should not compress. Not worth at all
                return BitmapCompression.None;
            }

            switch (colorMode)
            {
                case BitmapColor.ColorWithAlpha:
                    return BitmapCompression.ThirtyTwoABitsRLE;

                case BitmapColor.ColorWithAlphaPM:
                    return BitmapCompression.ThirtyTwoABitsRLE;

                case BitmapColor.Color:
                    break;

                default:
                    throw new MbmException(String.Format("Unsupported bitmap color type to save: {0}", colorMode.ToString()));
            }

            switch (bpp)
            {
                case 32:
                    return BitmapCompression.ThirtyTwoUBitsRLE;

                case 24:
                    return BitmapCompression.TwentyFourBitsRLE;

                case 16:
                    return BitmapCompression.SixteenBitsRLE;

                case 12:
                    return BitmapCompression.TwelveBitsRLE;

                case 8:
                    return BitmapCompression.ByteRLE;

                default:
                    break;
            }

            throw new MbmException(String.Format("Unsupported bits per pixel to save: {0}", bpp));
        }
    }
}
