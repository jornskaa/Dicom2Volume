using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Threading;
using System.Windows.Forms;

namespace Dicom2dds
{

    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var tarFiles = Tar.ListFileInfos(File.OpenRead(@"E:\test.tar"));
            Tar.Untar(File.OpenRead(@"E:\test.tar"), @"E:\untar\");
            
            if (args.Length > 1)
            {
                Logger.Warn("Too many arguments!");
                Logger.Info("Usage: dcm2dds [directory] - where directory contains your DICOM files.");
                Logger.Info("Check out dcm2dds.config for more configuration settings.");
            }

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

            if (filenames.Length == 0)
            {
                Logger.Warn("No files found in the specified directory!");
                return;
            }

            // Delete output directory before recreating it.
            var filename = filenames.First();
            var directory = Path.GetDirectoryName(filename) ?? ".";
            if (!Config.KeepFilesFlag.HasFlag(Config.KeepFilesFlags.OutputPath))
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

            // Perform the conversion.
            Logger.Info("Converting every " + Config.SkipEveryNSlices + " slices of " + filenames.Length + " to XML and RAW file formats..");
            var volumeData = Volume.Convert(Config.SkipEveryNSlices, filenames);
            if (volumeData != null)
            {
                Logger.Info("Converting RAW volume to DDS..");
                Dds.ConvertRawToDds(Config.VolumePathRaw, volumeData.Columns, volumeData.Rows, volumeData.Slices, 
                                    Config.VolumePathDds);
            }

            CleanupFiles();

            // Open Window Explorer upon completion.
            if (Config.OpenExplorerOnCompletion)
            {

                var argument = "/select, \"" + Path.Combine(directory, filename) + "\"";
                Process.Start("explorer.exe", argument);
            }

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

            if (volumeFileCounter == 3) directories.Add(Config.VolumePath);
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
    }
}
