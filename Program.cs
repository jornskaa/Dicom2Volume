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
            var directory = Path.GetFullPath(Path.GetDirectoryName(filename) ?? ".");
            Directory.SetCurrentDirectory(directory);

            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.OutputPath) &&
                Directory.Exists(Config.OutputPath))
            {
                try
                {
                    var outputPathFull = Path.Combine(directory, Config.OutputPath);
                    new FileIOPermission(FileIOPermissionAccess.AllAccess, outputPathFull).Demand();
                    Directory.Delete(outputPathFull, true);
                }
                catch (Exception err)
                {
                    Logger.Error("Unable to delete output path directory. " + err.Message);
                }
            }

            // Convert DICOM to XML slices and volume file formats as specified in app.config.
            var sliceFilenames = Slices.ConvertDicom(Config.ImagesPath, filenames);
            var sortedFilenames = Slices.Sort(Config.SortedPath, Config.SkipEveryNSlices, sliceFilenames.ToArray());
            var volumeFilenames = Slices.CreateVolume(Config.VolumeOutputPath, Config.VolumeOutputName, sortedFilenames.ToArray());
            var taredVolumeFilename = Tar.Create(Path.Combine(Config.VolumeOutputPath, Config.VolumeOutputName + "_raw.tar"), volumeFilenames.ToArray());
            GZip.Compress(taredVolumeFilename, Path.Combine(Config.VolumeOutputPath, Config.VolumeOutputName + "_raw.tgz"));

            using (var volumeDataStream = File.OpenRead(volumeFilenames[0]))
            {
                var volumeDataSerializer = new XmlSerializer(typeof(VolumeData));
                var volumeData = (VolumeData)volumeDataSerializer.Deserialize(volumeDataStream);

                var ddsVolumeFilename = Dds.ConvertRawToDds(volumeFilenames[1], volumeData.Columns, volumeData.Rows, volumeData.Slices, Path.Combine(Config.VolumeOutputPath, Config.VolumeOutputName + ".dds"));
                var taredDdsFilename = Tar.Create(Path.Combine(Config.VolumeOutputPath, Config.VolumeOutputName + "_dds.tar"), new[] { volumeFilenames[0], ddsVolumeFilename });
                GZip.Compress(taredDdsFilename, Path.Combine(Config.VolumeOutputPath, Config.VolumeOutputName + "_dds.tgz"));
            }

            // Remove files not marked with keep flag.
            CleanupFiles();

            // Open output folder in Windows Explorer.
            OpenWindowsExplorer(Path.Combine(directory, filename));

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

            int volumeFileCounter = 0;
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.RawVolume)) volumeFiles.Add(Config.VolumePathRaw);
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.DdsVolume)) volumeFiles.Add(Config.VolumePathDds);
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.VolumeXml)) volumeFiles.Add(Config.VolumePathXml);

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
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.Images)) directories.Add(Config.ImagesPath);
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.SortedImages)) directories.Add(Config.SortedPath);

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
