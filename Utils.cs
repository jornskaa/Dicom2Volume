using System.IO;
using System.Runtime.InteropServices;

namespace Dicom2dds
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
}
