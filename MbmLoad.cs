using System;
using System.IO;

using PaintDotNet;

namespace PaintDotNet.Data.MbmFileType
{
    class MbmLoad
    {
        public static Document Load(MbmFile file, Stream source)
        {
            // TODO: Make this configurable
            int bitmapToLoad = 0;
            return LoadIndex(file, source, bitmapToLoad);
        }

        static Document LoadIndex(MbmFile file, Stream source, int index)
        {
            SBMHeader header = file.GetBitmapHeader(index);
            UInt32 headerOffset = file.GetBitmapHeaderOffset(index);
            Document doc = new Document(header.sizeInPixel);

            // Create background layer
            BitmapLayer layer = Layer.CreateBackgroundLayer(header.sizeInPixel.Width, header.sizeInPixel.Height, "Bitmap");
            Surface surface = layer.Surface;

            LoadBitmapToSurface(header, headerOffset, source, surface);

            doc.Layers.Add(layer);

            return doc;
        }

        /// <summary>
        /// Load bitmap data to Paint.NET surface.
        /// </summary>
        /// <param name="header">The bitmap header to convert.</param>
        /// <param name="offset">The offset of the header in the stream.</param>
        /// <param name="source">The source stream.</param>
        /// <param name="surface">Paint.NET surface.</param>
        unsafe static void LoadBitmapToSurface(SBMHeader header, UInt32 offset, Stream source, Surface surface)
        {
            UInt32 lastPos = (UInt32)source.Position;
            source.Seek(offset + header.headerLength, SeekOrigin.Begin);

            UInt32 compressedSize = header.bitmapSize - header.headerLength;
            UInt32 stride = (UInt32)MbmFile.GetStride(header.sizeInPixel.Width, (Byte)(header.bitsPerPixel));
            UInt32 realSize = (UInt32)header.sizeInPixel.Height * stride;

            byte[] decompressedData = new byte[realSize];
            MemoryStream destinationStream = new MemoryStream(decompressedData, 0, (int)realSize);

            destinationStream.Seek(0, SeekOrigin.Begin);

            switch (header.compression)
            {
                case BitmapCompression comp when (comp <= BitmapCompression.ThirtyTwoABitsRLE && comp > BitmapCompression.None):
                    Algorithm.RLEDecompressor.Decompress(destinationStream, source, (int)compressedSize, comp);
                    break;

                default:
                    source.Read(decompressedData, 0, (int)realSize);
                    break;
            }

            destinationStream.Seek(0, SeekOrigin.Begin);

            for (int y = 0; y < header.sizeInPixel.Height; y++)
            {
                destinationStream.Seek(stride * y, SeekOrigin.Begin);
                ColorBgra* rowData = surface.GetRowAddress(y);

                // Convert to PDN representation
                switch (header.bitsPerPixel)
                {
                    // These case are generally easy
                    // Each channel is represent by 8 bits
                    case UInt32 i when (i % 8 == 0):
                        for (int x = 0; x < header.sizeInPixel.Width; x++)
                        {
                            rowData[x].R = (i >= 8) ? (Byte)destinationStream.ReadByte() : (Byte)0;
                            rowData[x].G = (i >= 16) ? (Byte)destinationStream.ReadByte() : (Byte)0;
                            rowData[x].B = (i >= 24) ? (Byte)destinationStream.ReadByte() : (Byte)0;
                            rowData[x].A = (i >= 32) ? (Byte)destinationStream.ReadByte() : (Byte)255;
                        }

                        break;

                    default:
                        throw new MbmException(String.Format("Unhandled converting {0} bits per pixel to Paint.NET surface", header.bitsPerPixel));
                }
            }

            source.Seek(lastPos, SeekOrigin.Begin);
        }
    }
}
