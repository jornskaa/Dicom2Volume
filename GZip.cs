using System;
using System.IO;
using System.IO.Compression;

namespace Dicom2Volume
{
    public class GZip
    {
        public static void Compress(FileInfo fi)
        {
            using (var inFile = fi.OpenRead())
            {
                using (var outFile = File.Create(fi.FullName + ".gz"))
                {
                    using (var compress = new GZipStream(outFile, CompressionMode.Compress))
                    {
                        inFile.CopyTo(compress);
                        Logger.Debug("Compressed {0} from {1} to {2} bytes.", fi.Name, fi.Length, outFile.Length);
                    }
                }
            }
        }

        public static void Decompress(FileInfo fi)
        {
            using (var inFile = fi.OpenRead())
            {
                var curFile = fi.FullName;
                var origName = curFile.Remove(curFile.Length - fi.Extension.Length);
                using (var outFile = File.Create(origName))
                {
                    using (var decompress = new GZipStream(inFile, CompressionMode.Decompress))
                    {
                        decompress.CopyTo(outFile);
                        Logger.Debug("Decompressed: {0}", fi.Name);
                    }
                }
            }
        }
    }
}
