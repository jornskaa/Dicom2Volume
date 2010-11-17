using System.IO;
using System.Runtime.InteropServices;

namespace Dicom2Volume
{
    public class Utils
    {
        // Ref: http://www.developerfusion.com/article/84519/mastering-structs-in-c/
        public static byte[] RawSerialize(object anything)
        {
            var rawsize = Marshal.SizeOf(anything);
            var rawdata = new byte[rawsize];
            var handle = GCHandle.Alloc(rawdata, GCHandleType.Pinned);
            Marshal.StructureToPtr(anything, handle.AddrOfPinnedObject(), false);
            handle.Free();

            return rawdata;
        }

        // Ref: http://www.developerfusion.com/article/84519/mastering-structs-in-c/
        public static T ReadStruct<T>(Stream fs)
        {
            var buffer = new byte[Marshal.SizeOf(typeof(T))];
            fs.Read(buffer, 0, Marshal.SizeOf(typeof(T)));
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var temp = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();

            return temp;
        }
    }

    //Tar.Create(@"E:\tar\a.tar", @"E:\data\volume\phenix\dcm2vol\volume\volume.dds", @"E:\data\volume\phenix\dcm2vol\volume\volume.xml");
    //GZip.Compress(@"E:\tar\a.tar", @"E:\tar\b.tar.gz");
    //GZip.Decompress(@"E:\tar\b.tar.gz", @"E:\tar\c.tar");
    //Tar.Untar(File.OpenRead(@"E:\tar\c.tar"), @"E:\tar\d\");
    //var files = Tar.ListFileInfos(GZip.OpenRead(@"E:\tar\b.tar.gz"));
    //Tar.Untar(GZip.OpenRead(@"E:\tar\b.tar.gz"), @"e:\tar\output");
    //var file = Tar.SkipToData(GZip.OpenRead(@"E:\tar\b.tar.gz"), files.Values.First());
}
