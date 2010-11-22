// ****************************************************************************************
// Copyright (C) 2010, Jorn Skaarud Karlsen 
// All rights reserved. 
//
// Redistribution and use in source and binary forms, with or without modification, are 
// permitted provided that the following conditions are met: 
//
// * Redistributions of source code must retain the above copyright notice, this list of 
//   conditions and the following disclaimer. 
// * Redistributions in binary form must reproduce the above copyright notice, this list 
//   of conditions and the following disclaimer in the documentation and/or other 
//   materials provided with the distribution. 
// * Neither the name of OFFIS e.V. nor the names of its contributors may be used to 
//   endorse or promote products derived from this software without specific prior 
//   written permission. 
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY 
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
// MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
// THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT 
// OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
// LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//****************************************************************************************

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
