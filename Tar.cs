using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Dicom2Volume
{
    public enum TarFiletype
    {
        File = 0, Directory = 5
    }

    public struct TarFileInfo
    {
        public string Filename;
        public long Position;
        public DateTime Modified;
        public TarFiletype Type;
        public long Size;
    }

    public class Tar
    {
        public struct Header
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
            public char[] Name;                 /*   0 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public char[] Mode;                 /* 100 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public char[] Uid;                  /* 108 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public char[] Gid;                  /* 116 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public char[] Size;                 /* 124 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public char[] MTime;                /* 136 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public char[] Checksum;               /* 148 */
            public char TypeFlag;               /* 156 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
            public char[] LinkName;             /* 157 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public char[] Magic;                /* 257 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public char[] Version;              /* 263 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public char[] UName;                /* 265 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public char[] GName;                /* 297 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public char[] DevMajor;             /* 329 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public char[] DevMinor;             /* 337 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 155)]
            public char[] Prefix;               /* 345 */
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public char[] Reserved;             /* 500 */
        }

        private static DateTime _utcTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static Dictionary<string, TarFileInfo> ListFileInfos(Stream inputStream)
        {
            long position = 0;
            var buffer = new byte[512 * 512];
            var output = new Dictionary<string, TarFileInfo>();

            while (true)
            {
                var header = Utils.ReadStruct<Header>(inputStream);
                var magic = ToString(header.Magic);
                position += 512;

                if (magic != "ustar ") break;

                var filename = ToString(header.Prefix) + ToString(header.Name);
                var size = Convert.ToInt64(ToString(header.Size), 8);

                switch (header.TypeFlag)
                {
                    case '0': // Regular file
                    case '5': // Directory
                        output[filename] = new TarFileInfo
                        {
                            Filename = filename,
                            Position = position,
                            Modified = _utcTime.AddSeconds((Convert.ToInt64(ToString(header.MTime), 8))),
                            Type = (header.TypeFlag == '0') ? TarFiletype.File : TarFiletype.Directory,
                            Size = size
                        };
                        break;
                }

                // Skip contents of file and align to 512 byte chunk boundary.
                var skipBytes = size + ((512 - ((position + size) % 512)) % 512);
                Skip(inputStream, skipBytes);
                position += skipBytes;

                Logger.Debug("Listing file from tar(" + size + "): \"" + filename + "\"");
            }

            inputStream.Close();

            return output;
        }

        public static void Untar(Stream inputStream, string outputDirectory)
        {
            long position = 0;
            var buffer = new byte[512 * 512];

            while (true)
            {
                var header = Utils.ReadStruct<Header>(inputStream);
                var magic = ToString(header.Magic);
                position += 512;

                if (magic != "ustar ") break;

                var filename = ToString(header.Prefix) + ToString(header.Name);
                var size = Convert.ToInt64(ToString(header.Size), 8);

                if (header.TypeFlag == '0') // Regular file.
                {
                    var directory = Path.Combine(outputDirectory, Path.GetDirectoryName(filename) ?? ".");
                    Directory.CreateDirectory(directory);

                    var outputFilename = Path.Combine(outputDirectory, filename);
                    var outputStream = File.Create(outputFilename);

                    var bytesLeft = size;
                    while (bytesLeft > 0)
                    {
                        var byteReadCount = inputStream.Read(buffer, 0, (int)Math.Min(bytesLeft, buffer.Length));
                        outputStream.Write(buffer, 0, byteReadCount);
                        bytesLeft -= byteReadCount;
                    }
                    outputStream.Close();
                    position += size;
                }
                else if (header.TypeFlag == '5') // Directory.
                {
                    Directory.CreateDirectory(filename);
                }

                // Align to 512 byte chunk boundary.
                var skipBytes = (int) ((512 - (position % 512)) % 512);
                Skip(inputStream, skipBytes);
                position += skipBytes;
                
                Logger.Debug("Reading file from tar(" + size + "): \"" + filename + "\"");
            }
            inputStream.Close();
        }

        public static string Create(string outputFilename, params string[] sourceFilenames)
        {
            var outputStream = File.Create(outputFilename);
            var mtime = (long)(DateTime.Now - _utcTime - TimeSpan.FromHours(1)).TotalSeconds;
            
            foreach (var sourceFilename in sourceFilenames)
            {
                var fileInfo = new FileInfo(sourceFilename);
                var name = Path.GetFileName(sourceFilename);
                var header = new Header
                {
                    Name = ToCharArray(name, 100),
                    Mode = ToCharArray("0000644", 8),
                    Uid = ToCharArray("0000764", 8),
                    Gid = ToCharArray("0000764", 8),
                    Size = ToCharArray(String.Format("{0:00000000000}", long.Parse(Convert.ToString(fileInfo.Length, 8))), 12),
                    MTime = ToCharArray(Convert.ToString(mtime, 8), 12),
                    Checksum = ToCharArray(new string(' ', 8), 8),
                    LinkName = ToCharArray("", 100),
                    Magic = ToCharArray("ustar ", 6),
                    Version = ToCharArray(" ", 2),
                    UName = ToCharArray("dcm2vol", 32),
                    GName = ToCharArray("dcm2vol", 32),
                    DevMajor = ToCharArray("", 8),
                    DevMinor = ToCharArray("", 8),
                    Prefix = ToCharArray("", 155),
                    Reserved = ToCharArray("", 12),
                    TypeFlag = '0',
                };

                var headerBytes = Utils.RawSerialize(header);
                var checksum = headerBytes.Aggregate(0, (current, t) => current + t);
                header.Checksum = ToCharArray(String.Format("{0:000000}\0 ", int.Parse(Convert.ToString(checksum, 8))), 8);
                headerBytes = Utils.RawSerialize(header);

                var sourceBytes = File.ReadAllBytes(sourceFilename); // Todo:
                outputStream.Write(headerBytes, 0, headerBytes.Length);
                outputStream.Write(sourceBytes, 0, sourceBytes.Length);

                var blockAlignBytes = (int)((512 - (outputStream.Position % 512)) % 512);
                outputStream.Write(new byte[blockAlignBytes], 0, blockAlignBytes);
            }

            outputStream.Write(new byte[1024], 0, 1024);
            outputStream.Close();

            return outputFilename;
        }

        public static Stream SkipToData(Stream stream, TarFileInfo fileInfo)
        {
            Skip(stream, fileInfo.Position);
            return stream;
        }

        private static readonly byte[] SkipBuffer = new byte[512 * 512];

        private static void Skip(Stream stream, long skipBytes)
        {
            var bytesRemaining = skipBytes;
            while (bytesRemaining > 0)
            {
                bytesRemaining -= stream.Read(SkipBuffer, 0, (int)Math.Min(bytesRemaining, SkipBuffer.Length));
            }
        }

        private static string ToString(char[] chars)
        {
            return TrimNulls(new string(chars));
        }

        private static string TrimNulls(string text)
        {
            var length = 0;
            for (var i = (text.Length - 1); i >= 0; i--)
            {
                if (text[i] == '\0') continue;
                length = (i + 1);
                break;
            }

            return text.Substring(0, length);
        }

        private static char[] ToCharArray(string text, int length)
        {
            var nullCount = length - text.Length;
            var output = text + new string('\0', nullCount);
            return output.ToCharArray();
        }
    }
}
