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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace Dicom2Volume
{

    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 1)
            {
                Logger.Warn("Too many arguments!");
                Logger.Info("Usage: dcm2vol [directory] - where directory contains your DICOM files.");
                Logger.Info("Check out dcm2vol.config for more configuration settings.");
            }

            // List the DICOM files to be converted.
            var filenames = new string[0];
            if (args.Length == 1)
            {
                filenames = Directory.GetFiles(args[0]);
            }

            var ofd = new OpenFileDialog { Multiselect = true };
            if (filenames.Length == 0 && ofd.ShowDialog() == DialogResult.OK)
            {
                filenames = ofd.FileNames;
            }
            else
            {
                Logger.Info("User cancelled!");
                return;
            }

            // Delete the output directory if it should be cleaned up before conversion.
            var filename = filenames.First();
            Config.RootDirectory = Path.GetFullPath(Path.GetDirectoryName(filename) ?? ".");
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.OutputPath) &&
                 Directory.Exists(Config.FullOutputPath))
            {
                try
                {
                    new FileIOPermission(FileIOPermissionAccess.AllAccess, Config.FullOutputPath).Demand();
                    Directory.Delete(Config.FullOutputPath, true);
                }
                catch (Exception err)
                {
                    Logger.Error("Unable to delete output path directory. " + err.Message);
                }
            }

            // Convert DICOM to XML slices and volume file formats as specified in app.config.
            var cleanupFiles = new List<string>();

            var convertedFilenames = filenames;
            if (!String.IsNullOrEmpty(Config.DicomConverter))
            {
                Logger.Info("Ensuring DICOM is decompressed, little endian and explicit VR.");
                convertedFilenames = ExtDcmConv.Convert(Config.FullDcmdjpegOutputPath, filenames).ToArray();
                if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.Dcmdjpeg)) cleanupFiles.AddRange(convertedFilenames);
            }

            Logger.Info("Converting DICOM to XML slices..");
            var sliceFilenames = Slices.ConvertDicom(Config.FullXmlImagesOutputPath, convertedFilenames.ToArray());
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.XmlImages)) cleanupFiles.AddRange(sliceFilenames);

            Logger.Info("Creating sorted XML slices based on slice location..");
            var sortedFilenames = Slices.Sort(Config.FullXmlImagesSortedOutputPath, Config.SkipEveryNSlices, sliceFilenames.ToArray());
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.XmlImagesSorted)) cleanupFiles.AddRange(sortedFilenames);

            Logger.Info("Creating RAW volume from slices..");
            var volumeFilenames = Slices.CreateVolume(Config.FullVolumeOutputPath, Config.VolumeOutputName, sortedFilenames.ToArray());
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.XmlVolume)) cleanupFiles.Add(volumeFilenames[0]);
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.RawVolume)) cleanupFiles.Add(volumeFilenames[1]);

            if (Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TarRawVolume) ||
                Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TgzRawVolume)) 
            {
                Logger.Info("Creating tar archive of RAW volume..");
                var taredVolumeFilename = Tar.Create(Path.Combine(Config.FullVolumeOutputPath, Config.VolumeOutputName + "_raw.tar"), volumeFilenames.ToArray());
                if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TarRawVolume)) cleanupFiles.Add(taredVolumeFilename);

                if (Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TgzRawVolume))
                {
                    Logger.Info("Creating gzipped archive of RAW volume..");
                    var tgzVolumeFilename = GZip.Compress(taredVolumeFilename, Path.Combine(Config.FullVolumeOutputPath, Config.VolumeOutputName + "_raw.tgz"));
                    if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TgzRawVolume)) cleanupFiles.Add(tgzVolumeFilename);
                }
            }

            using (var volumeDataStream = File.OpenRead(volumeFilenames[0]))
            {
                var volumeDataSerializer = new XmlSerializer(typeof(VolumeData));
                var volumeData = (VolumeData)volumeDataSerializer.Deserialize(volumeDataStream);

                if (Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.DdsVolume) ||
                    Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TarDdsVolume) ||
                    Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TgzDdsVolume))
                {
                    Logger.Info("Creating Direct Draw Surface (DDS) volume..");
                    var ddsVolumeFilename = Dds.ConvertRawToDds(volumeFilenames[1], volumeData.Columns, volumeData.Rows, volumeData.Slices, Path.Combine(Config.FullVolumeOutputPath, Config.VolumeOutputName + ".dds"));
                    if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.DdsVolume)) cleanupFiles.Add(ddsVolumeFilename);

                    if (Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TarDdsVolume) ||
                        Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TgzDdsVolume))
                    {
                        Logger.Info("Creating tar archive of DDS volume..");
                        var taredDdsFilename = Tar.Create(Path.Combine(Config.FullVolumeOutputPath, Config.VolumeOutputName + "_dds.tar"), new[] { volumeFilenames[0], ddsVolumeFilename });
                        if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TarDdsVolume)) cleanupFiles.Add(taredDdsFilename);

                        if (Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TgzDdsVolume))
                        {
                            Logger.Info("Creating gzipped archive of DDS volume..");
                            var gzipDdsFilename = GZip.Compress(taredDdsFilename, Path.Combine(Config.FullVolumeOutputPath, Config.VolumeOutputName + "_dds.tgz"));
                            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TgzDdsVolume)) cleanupFiles.Add(gzipDdsFilename);
                        }
                    }
                }
            }

            // Remove files not marked with keep flag.
            CleanupFiles(cleanupFiles);

            // Open output folder in Windows Explorer.
            OpenWindowsExplorer(Path.Combine(Config.RootDirectory, filename));

            Logger.Info("Done..");
            if (Config.WaitForEnterToExit)
            {
                Console.ReadLine();
            }
        }

        private static void CleanupFiles(IEnumerable<string> filenames)
        {
            foreach (var file in filenames)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception err)
                {
                    Logger.Error("Unable to delete " + file + ". " + err.Message);
                }
            }

            if (Directory.Exists(Config.FullDcmdjpegOutputPath) &&
                Directory.GetFiles(Config.FullDcmdjpegOutputPath).Length == 0)
            {
                Directory.Delete(Config.FullDcmdjpegOutputPath);
            }

            if (Directory.Exists(Config.FullVolumeOutputPath) && 
                Directory.GetFiles(Config.FullVolumeOutputPath).Length == 0)
            {
                Directory.Delete(Config.FullVolumeOutputPath);
            }

            if (Directory.Exists(Config.FullXmlImagesSortedOutputPath) && 
                Directory.GetFiles(Config.FullXmlImagesSortedOutputPath).Length == 0)
            {
                Directory.Delete(Config.FullXmlImagesSortedOutputPath);
            }

            if (Directory.Exists(Config.FullXmlImagesOutputPath) &&
                Directory.GetFiles(Config.FullXmlImagesOutputPath).Length == 0)
            {
                Directory.Delete(Config.FullXmlImagesOutputPath);
            }
        }

        private static void OpenWindowsExplorer(string path)
        {
            if (!Config.OpenExplorerOnCompletion) return;

            var argument = "/select, \"" + path + "\"";
            Process.Start("explorer.exe", argument);
        }
    }
}
