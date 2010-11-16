using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Dicom2dds
{
    public class ImageData
    {
        public int Rows;
        public int Columns;
        public double Width;
        public double Height;
        public double WindowWidth;
        public double WindowCenter;
        public double RescaleIntercept;
        public double RescaleSlope;
        public double[] ImageOrientationPatient;
        public double[] ImagePositionPatient;
        public double SliceLocation;
        public int MinIntensity = int.MaxValue;
        public int MaxIntensity = int.MinValue;
        public byte[] PixelData;
    }

    public class VolumeData
    {
        public int Rows;
        public int Columns;
        public int Slices;
        public double Width;
        public double Height;
        public double Depth;
        public double WindowWidth;
        public double WindowCenter;
        public double RescaleIntercept;
        public double RescaleSlope;
        public double[] ImageOrientationPatient;
        public double[] ImagePositionPatient;
        public double FirstSliceLocation;
        public double LastSliceLocation;
        public int MinIntensity = int.MaxValue;
        public int MaxIntensity = int.MinValue;
    }

    public struct Vector
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public void Cross(Vector v1, Vector v2)
        {
            X = (v1.Y * v2.Z) - (v1.Z * v2.Y);
            Y = -(v1.X * v2.Z) - (v1.Z * v2.X);
            Z = (v1.X * v2.Y) - (v1.Z * v2.X);
        }
    }

    public class ImageDataInfo
    {
        public double SliceLocation;
        public string Filename;
        public string SortedFilename;
    }

    public class Volume
    {
        public static VolumeData Convert(int everyN, params string[] filenames)
        {
            var directory = Path.GetDirectoryName(filenames[0]) ?? ".";
            Directory.SetCurrentDirectory(directory);

            Directory.CreateDirectory(Config.ImagesPath);
            Directory.CreateDirectory(Config.SortedPath);
            Directory.CreateDirectory(Config.VolumePath);

            var volumeData = new VolumeData();
            var imageDataInfoList = new List<ImageDataInfo>();
            var isReferenceFrame = true;

            for (var i = 0; i < filenames.Length; i++ )
            {
                try
                {
                    var inputFilename = filenames[i];
                    var outputFilename = String.Format(Path.Combine(Config.ImagesPath, Path.GetFileNameWithoutExtension(inputFilename) ?? ".") + ".xml", i);
                    var imageData = ConvertSliceToXml(volumeData, inputFilename, outputFilename, isReferenceFrame);
                    if (imageData == null)
                    {
                        continue;
                    }

                    if (isReferenceFrame)
                    {
                        SetReferenceSlice(volumeData, imageData);
                        isReferenceFrame = false;
                    }

                    imageDataInfoList.Add(new ImageDataInfo()
                    {
                        Filename = outputFilename,
                        SliceLocation = imageData.SliceLocation
                    });
                }
                catch (Exception err)
                {
                    Logger.Error(err.ToString());
                }
            }

            if (imageDataInfoList.Count == 0)
            {
                Logger.Error("Unable to find any valid DICOM images as input!");
                return null;
            }

            var sortedImageDataInfoList = CreateSortedImageDataFiles(imageDataInfoList, everyN);
            WriteVolumeData(volumeData, sortedImageDataInfoList);

            return volumeData;
        }

        private static void WriteVolumeData(VolumeData volumeData, ICollection<ImageDataInfo> sortedImageDataInfoList)
        {
            volumeData.FirstSliceLocation = sortedImageDataInfoList.First().SliceLocation;
            volumeData.LastSliceLocation = sortedImageDataInfoList.Last().SliceLocation;
            volumeData.Depth = Math.Abs(volumeData.LastSliceLocation - volumeData.FirstSliceLocation);
            volumeData.Slices = sortedImageDataInfoList.Count;

            var volumeDataSerializer = new XmlSerializer(typeof(VolumeData));
            var volumeOutputStream = File.Create(Config.VolumePathXml);
            volumeDataSerializer.Serialize(volumeOutputStream, volumeData);
            volumeOutputStream.Close();

            var deserializer = new XmlSerializer(typeof(ImageData));
            var volumeStream = File.Create(Config.VolumePathRaw);
            var volumeWriter = new BinaryWriter(volumeStream);
            foreach (var imageFileInfo in sortedImageDataInfoList)
            {
                var imageDataStream = File.OpenRead(imageFileInfo.SortedFilename);
                var imageData = (ImageData)deserializer.Deserialize(imageDataStream);
                volumeWriter.Write(imageData.PixelData);
                imageDataStream.Close();
            }
            volumeWriter.Close();
            volumeStream.Close();
        }

        private static List<ImageDataInfo> CreateSortedImageDataFiles(IEnumerable<ImageDataInfo> imageDataSortInfos, int everyN)
        {
            var output = new List<ImageDataInfo>(imageDataSortInfos);
            output.Sort((a, b) => (a.SliceLocation.CompareTo(b.SliceLocation)));
            for (var i = 0; i < output.Count; i++)
            {
                var sortInfo = output[i];
                sortInfo.SortedFilename = Path.Combine(Config.SortedPath, String.Format("{0:00000}-" + Path.GetFileName(output[i].Filename), i));
                File.Copy(sortInfo.Filename, sortInfo.SortedFilename, true);
            }

            return output.Where((t, i) => i % everyN == 0).ToList();
        }

        private static ImageData ConvertSliceToXml(VolumeData volumeData, string filename, string outputFilename, bool isReferenceFrame)
        {
            ImageData imageData = null;
            using (var reader = new BinaryReader(File.OpenRead(filename)))
            {
                var dataset = Dicom.ReadFile(reader);
                if (dataset.Count > 0)
                {
                    if (!VerifyDataformat(dataset))
                    {
                        Logger.Warn("Not valid dataformat for " + Path.GetFileName(filename) + ". Skipping file..");
                        return null;
                    }

                    imageData = ReadImageData(dataset);

                    var serializer = new XmlSerializer(typeof(ImageData));
                    var outputStream = File.Create(outputFilename);
                    serializer.Serialize(outputStream, imageData);
                    outputStream.Close();

                    volumeData.MinIntensity = Math.Min(volumeData.MinIntensity, imageData.MinIntensity);
                    volumeData.MaxIntensity = Math.Max(volumeData.MaxIntensity, imageData.MaxIntensity);
                }
            }

            return imageData;
        }

        private static void SetReferenceSlice(VolumeData volumeData, ImageData referenceImageData)
        {
            volumeData.Columns = referenceImageData.Columns;
            volumeData.Rows = referenceImageData.Rows;
            volumeData.Height = referenceImageData.Height;
            volumeData.Width = referenceImageData.Width;
            volumeData.ImageOrientationPatient = referenceImageData.ImageOrientationPatient;
            volumeData.ImagePositionPatient = referenceImageData.ImagePositionPatient;
            volumeData.RescaleIntercept = referenceImageData.RescaleIntercept;
            volumeData.RescaleSlope = referenceImageData.RescaleSlope;
            volumeData.WindowCenter = referenceImageData.WindowCenter;
            volumeData.WindowWidth = referenceImageData.WindowWidth;
        }

        private unsafe static ImageData ReadImageData(Dictionary<uint, Element> dataset)
        {
            var imageData = new ImageData();

            var element = dataset[Dicom.ReverseDictionary["PixelData"]];
            imageData.PixelData = element.Value[0].Bytes;

            element = dataset[Dicom.ReverseDictionary["Rows"]];
            imageData.Rows = (int)element.Value[0].Long;

            element = dataset[Dicom.ReverseDictionary["Columns"]];
            imageData.Columns = (int)element.Value[0].Long;

            element = dataset[Dicom.ReverseDictionary["WindowWidth"]];
            imageData.WindowWidth = element.Value[0].Double;

            element = dataset[Dicom.ReverseDictionary["WindowCenter"]];
            imageData.WindowCenter = element.Value[0].Double;

            element = dataset[Dicom.ReverseDictionary["RescaleIntercept"]];
            imageData.RescaleIntercept = element.Value[0].Double;

            element = dataset[Dicom.ReverseDictionary["RescaleSlope"]];
            imageData.RescaleSlope = element.Value[0].Double;

            element = dataset[Dicom.ReverseDictionary["ImageOrientationPatient"]];
            imageData.ImageOrientationPatient = (from v in element.Value select v.Double).ToArray();

            element = dataset[Dicom.ReverseDictionary["ImagePositionPatient"]];
            imageData.ImagePositionPatient = (from v in element.Value select v.Double).ToArray();

            // Calculate SliceLocation.
            var x = new Vector { X = imageData.ImageOrientationPatient[0], Y = imageData.ImageOrientationPatient[1], Z = imageData.ImageOrientationPatient[2] };
            var y = new Vector { X = imageData.ImageOrientationPatient[3], Y = imageData.ImageOrientationPatient[4], Z = imageData.ImageOrientationPatient[5] };
            var z = new Vector();
            z.Cross(x, y);

            imageData.SliceLocation = imageData.ImagePositionPatient[0] * z.X + imageData.ImagePositionPatient[1] * z.Y + imageData.ImagePositionPatient[2] * z.Z;

            // Check pixel padding value - application is later.
            element = dataset[Dicom.ReverseDictionary["PixelRepresentation"]];
            var pixelRepresentation = element.Value[0].Long;

            var pixelPaddingValue = long.MinValue;
            var pixelPaddingValueId = Dicom.ReverseDictionary["PixelPaddingValue"];
            if (dataset.ContainsKey(pixelPaddingValueId))
            {
                element = dataset[pixelPaddingValueId];
                pixelPaddingValue = element.Value[0].Long;
                if (pixelRepresentation == 1)
                {
                    pixelPaddingValue -= ushort.MinValue;
                }
            }

            // Convert to unsigned short pixel data.
            if (pixelRepresentation == 1) // Need to convert to unsigned short.
            {
                fixed (byte* pixelData = imageData.PixelData)
                {
                    var shortPixelData = (short*)pixelData;
                    var ushortPixelData = (ushort*)pixelData;
                    var pixelCount = imageData.Rows * imageData.Columns;
                    for (var i = 0; i < pixelCount; i++)
                    {
                        ushortPixelData[i] = (ushort)(shortPixelData[i] - short.MinValue);
                    }
                }

                imageData.RescaleIntercept -= short.MinValue;
            }

            // Apply pixel padding value and record minimum and maximum intensities.
            fixed (byte* pixelData = imageData.PixelData)
            {
                var ushortPixelData = (ushort*)pixelData;
                var pixelCount = imageData.Rows * imageData.Columns;
                for (var i = 0; i < pixelCount; i++)
                {
                    if (ushortPixelData[i] == pixelPaddingValue)
                    {
                        ushortPixelData[i] = 0;
                    }

                    var value = ushortPixelData[i];
                    imageData.MinIntensity = Math.Min(imageData.MinIntensity, value);
                    imageData.MaxIntensity = Math.Max(imageData.MaxIntensity, value);
                }
            }

            // Calculate image physical dimensions in mm.
            element = dataset[Dicom.ReverseDictionary["PixelSpacing"]];
            var pixelSpacing = element;

            imageData.Width = imageData.Columns * pixelSpacing.Value[0].Double;
            imageData.Height = imageData.Rows * pixelSpacing.Value[0].Double;

            return imageData;
        }

        private static bool VerifyDataformat(IDictionary<uint, Element> dataset)
        {
            if (dataset.Count <= 0)
            {
                return false;
            }

            var element = dataset[Dicom.ReverseDictionary["PhotometricInterpretation"]];
            var pi = element.Value[0].Text;
            if (pi != "MONOCHROME2")
            {
                Logger.Warn("Unable to convert DICOM other than MONOCHROME2: " + pi);
                return false;
            }

            return true;
        }
    }
}
