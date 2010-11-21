using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;

namespace Dicom2Volume
{
    public class Dcmtk
    {
        public static List<string> DecompressLittleEndianExplicitVr(string outputDirectory, params string[] inputFilenames)
        {
            var outputFilenames = new List<string>();
            Directory.CreateDirectory(outputDirectory);

            // Convert to a format that the dicom loader can handle easily.
            foreach (var inputFilename in inputFilenames)
            {
                var outputFilename = Path.Combine(outputDirectory, Path.GetFileName(inputFilename));
                Logger.Debug("dcmdjpeg.exe " + inputFilename + " " + outputFilename);

                var process = new Process
                {
                    // dcmdjpeg arguments default to explicit VR, little endian, decompressed - perfect!
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = Path.Combine(Application.StartupPath, "dcmdjpeg.exe"),
                        Arguments = "\"" + inputFilename + "\" \"" + outputFilename + "\"", 
                        UseShellExecute = false
                    }
                };

                if (!process.Start())
                {
                    throw new IOException("Unable to execute dcmdjpeg.exe!");
                }

                process.WaitForExit();
                outputFilenames.Add(outputFilename);
            }

            return outputFilenames;
        }
    }
}
