using System.IO;
using System.IO.Compression;

namespace Dicom2Volume
{
    public class GZip
    {
        public static void Compress(string sourceFilename, string outputFilename)
        {
            using (var inFile = File.OpenRead(sourceFilename))
            {
                using (var outFile = File.Create(outputFilename))
                {
                    using (var compress = new GZipStream(outFile, CompressionMode.Compress))
                    {
                        inFile.CopyTo(compress);
                    }
                }
            }
        }

        public static void Decompress(string sourceFilename, string outputFilename)
        {
            using (var inFile = File.OpenRead(sourceFilename))
            {
                using (var outFile = File.Create(outputFilename))
                {
                    using (var decompress = new GZipStream(inFile, CompressionMode.Decompress))
                    {
                        decompress.CopyTo(outFile);
                    }
                }
            }
        }

        public static Stream OpenRead(string filename)
        {
            return new GZipStream(File.OpenRead(filename), CompressionMode.Decompress);
        }
    }
}
