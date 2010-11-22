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
// * Neither the name of Dicom2Volume nor the names of its contributors may be used to 
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
using System.Configuration;
using System.IO;

namespace Dicom2Volume
{
    public struct SerializeTest
    {
        public Config.KeepFilesFlags Flag;
    }

    public class Config
    {
        [Flags]
        public enum KeepFilesFlags
        {
            None = 0,
            XmlImages = 1,
            XmlImagesSorted = 2, 
            XmlVolume = 4,
            RawVolume = 8,
            DdsVolume = 16,
            TarRawVolume = 32,
            TgzRawVolume = 64,
            TarDdsVolume = 128,
            TgzDdsVolume = 256,
            OutputPath = 512,
            Dcmdjpeg = 1024
        }

        public static int SkipEveryNSlices { get; set; }
        public static bool OpenExplorerOnCompletion { get; set; }
        public static Logger.LogLevelType LogLevel { get; set; }
        public static KeepFilesFlags KeepFilesFlag { get; set; }
        public static bool WaitForEnterToExit { get; set; }

        public static string DicomConverter { get; set; }
        public static string DicomConverterArguments { get; set; }
        public static string RootDirectory { get; set; }
        public static string RelativeOutputPath { get; set; }
        public static string RelativeXmlImagesOutputPath = "images";
        public static string RelativeXmlImagesSortedOutputPath = "sorted";
        public static string RelativeVolumeOutputPath = "volume";
        public static string RelativeDcmdjpegOutputPath = "dcmdjpeg";
        public static string VolumeOutputName = "volume";

        public static string FullOutputPath
        {
            get { return Path.Combine(RootDirectory, RelativeOutputPath); }
        }

        public static string FullXmlImagesOutputPath
        {
            get { return Path.Combine(FullOutputPath, RelativeXmlImagesOutputPath); }
        }

        public static string FullXmlImagesSortedOutputPath
        {
            get { return Path.Combine(FullOutputPath, RelativeXmlImagesSortedOutputPath); }
        }

        public static string FullVolumeOutputPath
        {
            get { return Path.Combine(FullOutputPath, RelativeVolumeOutputPath); }
        }

        public static string FullDcmdjpegOutputPath 
        {
            get { return Path.Combine(FullOutputPath, RelativeDcmdjpegOutputPath); }
        }
        
        public static Dictionary<uint, ElementTag> DicomDictionary { get; set; }

        static Config()
        {
            SkipEveryNSlices = int.Parse(ConfigurationManager.AppSettings["SkipEveryNSlices"] ?? "1");
            OpenExplorerOnCompletion = bool.Parse(ConfigurationManager.AppSettings["OpenExplorerOnCompletion"] ?? "true");
            LogLevel = (Logger.LogLevelType)Enum.Parse(typeof(Logger.LogLevelType), ConfigurationManager.AppSettings["LogLevel"] ?? "Info");
            KeepFilesFlag = (KeepFilesFlags)Enum.Parse(typeof(KeepFilesFlags), ConfigurationManager.AppSettings["KeepFilesFlag"] ?? "Images, SortedImages, VolumeXml, RawVolume, DdsVolume, RelativeOutputPath, CompressedDds, CompressedRaw");
            WaitForEnterToExit = bool.Parse(ConfigurationManager.AppSettings["WaitForEnterToExit"] ?? "false");
            RelativeOutputPath = ConfigurationManager.AppSettings["RelativeOutputPath"] ?? "dcm2vol";
            DicomConverter = ConfigurationManager.AppSettings["DicomConverter"];
            DicomConverterArguments = ConfigurationManager.AppSettings["DicomConverterArguments"];

            DicomDictionary = new Dictionary<uint, ElementTag>();
            var section = DicomConfigSection.GetConfigSection();
            if (section == null) return;

            foreach (var tag in section.Tags)
            {
                var configTag = (DicomTagConfigElement) tag;
                Logger.Debug("DICOM dictionary  : " + configTag.ElementTag.Name + " - " + configTag.CombinedId);
                DicomDictionary[configTag.CombinedId] = configTag.ElementTag;
            }
        }
    }

    public class DicomTagConfigElement : ConfigurationElement
    {
        [ConfigurationProperty("groupId", IsRequired = true)]
        public string GroupIdString { get { return (string)this["groupId"]; } }

        [ConfigurationProperty("elementId", IsRequired = true)]
        public string ElementIdString { get { return (string)this["elementId"]; } }

        [ConfigurationProperty("name", IsRequired = true)]
        public string TagName { get { return (string)this["name"]; } }

        [ConfigurationProperty("type", IsRequired = true)]
        public string TagType { get { return (string)this["type"]; } }

        public ushort GroupId { get { return Convert.ToUInt16(GroupIdString, 16); } }
        public ushort ElementId { get { return Convert.ToUInt16(ElementIdString, 16); } }
        public uint CombinedId { get { return ElementTag.GetCombinedId(GroupId, ElementId); } }
        public ElementTag ElementTag { get { return new ElementTag { Name = TagName, VR = (ValueTypes)Enum.Parse(typeof(ValueTypes), TagType) }; } }
    }

    public class DicomTagConfigElementCollection : ConfigurationElementCollection
    {
        public DicomTagConfigElement this[int index] { get { return BaseGet(index) as DicomTagConfigElement; } }

        protected override ConfigurationElement CreateNewElement()
        {
            return new DicomTagConfigElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((DicomTagConfigElement)(element)).TagName;
        }
    }

    public class DicomConfigSection : ConfigurationSection
    {
        [ConfigurationProperty("tags")]
        public DicomTagConfigElementCollection Tags { get { return this["tags"] as DicomTagConfigElementCollection; } }

        public static DicomConfigSection GetConfigSection()
        {
            return ConfigurationManager.GetSection("dicom") as DicomConfigSection;
        }
    }
}
