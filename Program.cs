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
            var rootDirectory = Path.GetFullPath(Path.GetDirectoryName(filename) ?? ".");
            //Directory.SetCurrentDirectory(rootDirectory);

            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.OutputPath) &&
                Directory.Exists(Config.OutputPath))
            {
                try
                {
                    var outputPathFull = Path.Combine(rootDirectory, Config.OutputPath);
                    new FileIOPermission(FileIOPermissionAccess.AllAccess, outputPathFull).Demand();
                    Directory.Delete(outputPathFull, true);
                }
                catch (Exception err)
                {
                    Logger.Error("Unable to delete output path directory. " + err.Message);
                }
            }

            // Convert DICOM to XML slices and volume file formats as specified in app.config.

            Logger.Info("Converting DICOM to XML slices..");
            var sliceFilenames = Slices.ConvertDicom(Config.ImagesOutputPath, filenames);

            Logger.Info("Creating sorted XML slices based on slice location..");
            var sortedFilenames = Slices.Sort(Config.ImagesSortedOutputPath, Config.SkipEveryNSlices, sliceFilenames.ToArray());

            Logger.Info("Creating RAW volume from slices..");
            var volumeFilenames = Slices.CreateVolume(Config.VolumeOutputPath, Config.VolumeOutputName, sortedFilenames.ToArray());

            if (Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TarRawVolume) ||
                Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TgzRawVolume)) 
            {
                Logger.Info("Creating tar archive of RAW volume..");
                var taredVolumeFilename = Tar.Create(Path.Combine(Config.VolumeOutputPath, Config.VolumeOutputName + "_raw.tar"), volumeFilenames.ToArray());

                if (Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TgzRawVolume))
                {
                    Logger.Info("Creating gzipped archive of RAW volume..");
                    GZip.Compress(taredVolumeFilename, Path.Combine(Config.VolumeOutputPath, Config.VolumeOutputName + "_raw.tgz"));
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
                    var ddsVolumeFilename = Dds.ConvertRawToDds(volumeFilenames[1], volumeData.Columns, volumeData.Rows, volumeData.Slices, Path.Combine(Config.VolumeOutputPath, Config.VolumeOutputName + ".dds"));

                    if (Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TarDdsVolume) ||
                        Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TgzDdsVolume))
                    {
                        Logger.Info("Creating tar archive of DDS volume..");
                        var taredDdsFilename = Tar.Create(Path.Combine(Config.VolumeOutputPath, Config.VolumeOutputName + "_dds.tar"), new[] { volumeFilenames[0], ddsVolumeFilename });

                        if (Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.TgzDdsVolume))
                        {
                            Logger.Info("Creating gzipped archive of DDS volume..");
                            GZip.Compress(taredDdsFilename, Path.Combine(Config.VolumeOutputPath, Config.VolumeOutputName + "_dds.tgz"));
                        }
                    }
                }
            }

            // Remove files not marked with keep flag.
            CleanupFiles();

            // Open output folder in Windows Explorer.
            OpenWindowsExplorer(Path.Combine(rootDirectory, filename));

            Logger.Info("Done..");
            if (Config.WaitForEnterToExit)
            {
                Console.ReadLine();
            }
        }

        private static void CleanupFiles()
        {
            var volumeFiles = new List<string>();
            var directories = new List<string>();

            var volumeFileCounter = 0;
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.RawVolume)) volumeFiles.Add(Config.VolumePathRaw);
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.DdsVolume)) volumeFiles.Add(Config.VolumePathDds);
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.XmlVolume)) volumeFiles.Add(Config.VolumePathXml);

            foreach (var file in volumeFiles)
            {
                try
                {
                    File.Delete(file);
                    volumeFileCounter++;
                }
                catch (Exception err)
                {
                    Logger.Error("Unable to delete " + file + ". " + err.Message);
                }
            }

            if (volumeFileCounter == 3) directories.Add(Config.VolumeOutputPath);
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.XmlImages)) directories.Add(Config.ImagesOutputPath);
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.XmlImagesSorted)) directories.Add(Config.ImagesSortedOutputPath);

            foreach (var directory in directories)
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch (Exception err)
                {
                    Logger.Error("Unable to delete " + directory + ". " + err.Message);
                }
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
