using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Dicom2dds
{
    public enum FormatEnum : uint
    {
        R16Uint = 57,
    }

    [Flags]
    public enum PixelFormatFlags : uint
    {
        Luminance = 0x20000
    }

    [Flags]
    public enum SurfaceFlags : uint
    {
        CapsTexture = 0x1000
    }

    [Flags]
    public enum HeaderFlags : uint
    {
        Caps = 0x1,
        Height = 0x2,
        Width = 0x4,
        Pixelformat = 0x1000,
        Depth = 0x800000
    }

    [Flags]
    public enum CubeMapFlags : uint
    {
        Caps2Volume = 0x200000
    }

    // Ref: http://msdn.microsoft.com/en-us/library/bb943991(v=VS.85).aspx
    public class Dds
    {
        public const uint MagicWord = 0x20534444;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Header
        {
            public uint Size;
            public HeaderFlags HeaderFlag;
            public uint Height;
            public uint Width;
            public uint PitchOrLinearSize;
            public uint Depth;
            public uint MipMapCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
            public uint[] Reserved1;
            public PixelFormat Format;
            public SurfaceFlags SurfaceFlag;
            public CubeMapFlags CubemapFlag;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public uint[] Reserved2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PixelFormat
        {
            public uint Size;
            public PixelFormatFlags Flag;
            public uint FourCc;
            public uint RgbBitCount;
            public uint RBitMask;
            public uint GBitMask;
            public uint BBitMask;
            public uint ABitMask;
        }

        public static void ConvertRawToDds(string sourceFilename, int width, int height, int depth, string outputFilename)
        {
            var header = new Header()
            {
                Size = 124,
                HeaderFlag = HeaderFlags.Caps | HeaderFlags.Height | HeaderFlags.Width | HeaderFlags.Depth | HeaderFlags.Pixelformat,
                Height = (uint)height,
                Width =  (uint)width,
                PitchOrLinearSize = 0,
                Depth = (uint)depth,
                MipMapCount = 0,
                Format = new PixelFormat()
                {
                    Size = 32,
                    Flag = PixelFormatFlags.Luminance,
                    FourCc = 0,
                    RgbBitCount = 16,
                    RBitMask = 0xffff,
                    GBitMask = 0x0,
                    BBitMask = 0x0,
                    ABitMask = 0x0
                },
                SurfaceFlag = SurfaceFlags.CapsTexture,
                CubemapFlag = CubeMapFlags.Caps2Volume,
            };

            var outputStream = File.Create(outputFilename);
            var writer = new BinaryWriter(outputStream);

            writer.Write(MagicWord);
            writer.Write(Utils.RawSerialize(header));
            var sourceStream = File.OpenRead(sourceFilename);
            var readerStream = new BinaryReader(sourceStream);

            // Read/write one slice at a time.
            for (var i = 0; i < depth; i++)
            {
                var pixelData = readerStream.ReadBytes(width * height * 2);
                writer.Write(pixelData);
            }

            readerStream.Close();
            sourceStream.Close();
            writer.Close();
            outputStream.Close();
        }

        public struct Surface
        {
            public Header Info;
            public Stream PixelDataStream;
        }

        public static Surface OpenRead(string filename)
        {
            var fileStream = File.OpenRead(filename);
            var reader = new BinaryReader(fileStream);

            var magic = reader.ReadUInt32();
            if (magic != MagicWord)
            {
                reader.Close();
                fileStream.Close();
                throw new IOException("Unable to find DDS magic word in beginning of file!");
            }

            var output = new Surface 
            {
                Info = Utils.ReadStruct<Header>(fileStream), 
                PixelDataStream = fileStream
            };

            reader.Close();
            fileStream.Close();

            if (output.Info.Format.FourCc != 0)
            {
                output.PixelDataStream.Close();
                throw new IOException("DX10 Header is not supported!");
            }

            return output;
        }
    }
}
