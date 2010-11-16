using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Dicom2dds
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
        public int Size;

        public Stream OpenRead()
        {
            var stream = File.OpenRead(Filename);
            stream.Seek(Position, SeekOrigin.Begin);
            return stream;
        }
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
            public char[] Reserved;            /* 500 */
        }

        private static DateTime _utcTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static Dictionary<string, TarFileInfo> ListFileInfos(Stream inputStream)
        {
            var output = new Dictionary<string, TarFileInfo>();

            while (inputStream.Position < inputStream.Length)
            {
                var header = Utils.ReadStruct<Header>(inputStream);
                var magic = ToString(header.Magic);
                if (magic != "ustar ") break;

                var filename = ToString(header.Prefix) + ToString(header.Name);
                var size = Convert.ToInt32(ToString(header.Size), 8);

                switch (header.TypeFlag)
                {
                    case '0': // Regular file
                    case '5': // Directory
                        output[filename] = new TarFileInfo
                        {
                            Filename = filename,
                            Position = inputStream.Position,
                            Modified = _utcTime.AddSeconds((Convert.ToInt64(ToString(header.MTime), 8))),
                            Type = (header.TypeFlag == '0') ? TarFiletype.File : TarFiletype.Directory,
                            Size = size
                        };
                        break;
                }

                // Skip contents of file and align to 512 byte chunk boundary.
                inputStream.Seek(size + (int)((512 - (inputStream.Position % 512)) % 512), SeekOrigin.Current);
                Logger.Debug("Listing file from tar(" + size + "): \"" + filename + "\"");
            }

            inputStream.Close();

            return output;
        }

        public static void Untar(Stream inputStream, string outputDirectory)
        {
            var buffer = new byte[512 * 512];
            while (inputStream.Position < inputStream.Length)
            {
                var header = Utils.ReadStruct<Header>(inputStream);
                var magic = ToString(header.Magic);
                if (magic != "ustar ") break;

                var filename = ToString(header.Prefix) + ToString(header.Name);
                var size = Convert.ToInt32(ToString(header.Size), 8);

                if (header.TypeFlag == '0') // Regular file.
                {
                    var directory = Path.Combine(outputDirectory, Path.GetDirectoryName(filename) ?? ".");
                    Directory.CreateDirectory(directory);

                    var outputFilename = Path.Combine(outputDirectory, filename);
                    var outputStream = File.Create(outputFilename);

                    var bytesLeft = size;
                    while (bytesLeft > 0)
                    {
                        var byteReadCount = inputStream.Read(buffer, 0, Math.Min(bytesLeft, buffer.Length));
                        outputStream.Write(buffer, 0, byteReadCount);
                        bytesLeft -= byteReadCount;
                    }
                    outputStream.Close();
                }
                else if (header.TypeFlag == '5') // Directory.
                {
                    Directory.CreateDirectory(filename);
                }

                // Align to 512 byte chunk boundary.
                inputStream.Seek((int)((512 - (inputStream.Position % 512)) % 512), SeekOrigin.Current);
                Logger.Debug("Reading file from tar(" + size + "): \"" + filename + "\"");
            }
            inputStream.Close();
        }

        public static void Write(string outputFilename, params string[] sourceFilenames)
        {
            var utcTime = DateTime.Now.ToFileTimeUtc();
            
            for (var i = 0; i < sourceFilenames.Length; i++)
            {
                var filename = sourceFilenames[i];
                var fileInfo = new FileInfo(filename);
                var header = new Header
                {
                    Name = sourceFilenames[i].ToCharArray(),
                    Mode = "0000644".ToCharArray(),
                    Uid = "0000764".ToCharArray(),
                    Gid = "0000764".ToCharArray(),
                    Size = String.Format("{0:00000000000}", fileInfo.Length).ToCharArray(),
                    MTime = utcTime.ToString().ToCharArray(),
                    Checksum = "0".ToCharArray(),
                    TypeFlag = '0',
                    LinkName = "".ToCharArray(),
                    Magic = "ustar ".ToCharArray(),
                    Version = " ".ToCharArray(),
                    UName = "passion".ToCharArray(),
                    GName = "passion".ToCharArray(),
                    DevMajor = "".ToCharArray(),
                    DevMinor = "".ToCharArray(),
                    Prefix = "".ToCharArray()
                };

                var headerBytes = Utils.RawSerialize(header);
                var checksum = headerBytes.Aggregate(0, (current, t) => current + t);
                header.Checksum = checksum.ToString().ToCharArray();
                headerBytes = Utils.RawSerialize(header);
            }
        }

        private static string ToString(char[] chars)
        {
            return TrimNulls(new string(chars));
        }

        private static string TrimNulls(string text)
        {
            if (text == null) return null;

            int startIndex;
            for (startIndex = 0; startIndex < text.Length; startIndex++)
            {
                if (text[startIndex] != '\0') break;
            }

            var length = 0;
            for (var i = (text.Length - 1); i >= startIndex; i--)
            {
                length = (i + 1) - startIndex;
                if (text[i] != '\0') break;
            }

            return text.Substring(startIndex, length);
        }
    }
}
