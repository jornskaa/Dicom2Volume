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

using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Dicom2Volume
{
    public enum ValueTypes
    {
        String, DoubleString, UShort, Bytes, Separator, Item, Unknown
    }

    public struct Value
    {
        public string Text { get; set; }
        public long Long { get; set; }
        public double Double { get; set; }
        public byte[] Bytes { get; set; }
        public List<Element> Items { get; set; }
    }

    public class ElementTag
    {
        public ValueTypes VR { get; set; }
        public string Name { get; set; }

        public static uint GetCombinedId(ushort groupId, ushort elementId)
        {
            return (((uint)groupId) << 16) | elementId;
        }
    }

    public class Element
    {
        public ushort GroupId { get; set; }
        public ushort ElementId { get; set; }
        public Value[] Value { get; set; }
        public ElementTag Tag { get; set; }
    }

    internal class ReaderState
    {
        public bool UseImplicitVr { get; set; }
        public bool IsImplicitVr { get; set; }
        public BinaryReader Reader { get; set; }
        public string ExplicitVr { get; set; }
    }

    public class Dicom
    {
        public static readonly Dictionary<uint, ElementTag> Dictionary;
        public static readonly Dictionary<string, uint> ReverseDictionary;

        static Dicom()
        {
            Dictionary = Config.DicomDictionary;
            ReverseDictionary = new Dictionary<string, uint>();
            foreach (var key in Dictionary.Keys)
            {
                var value = Dictionary[key];
                ReverseDictionary[value.Name] = key;
            }
        }

        public static Dictionary<uint, Element> ReadFile(BinaryReader reader)
        {
            var state = new ReaderState()
            {
                UseImplicitVr = false,
                IsImplicitVr = false,
                Reader = reader,
            };

            reader.ReadBytes(128);

            var magicBytes = reader.ReadChars(4);
            var magicText = new string(magicBytes);
            if (magicText != "DICM")
            {
                Logger.Error("Fileformat not recognized!");
                return new Dictionary<uint, Element>();
            }

            var dicomData = new Dictionary<uint, Element>();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var groupId = reader.ReadUInt16();
                var elementId = reader.ReadUInt16();
                var combinedId = ElementTag.GetCombinedId(groupId, elementId);
                Logger.Debug("(0x{0:x4}, 0x{1:x4})", groupId, elementId);

                var element = ReadElement(state, groupId, elementId);
                if (element.Tag != null && element.Tag.VR != ValueTypes.Separator)
                {
                    dicomData.Add(combinedId, element);
                }
            }

            return dicomData;
        }

        private static Element ReadElement(ReaderState state, ushort groupId, ushort elementId)
        {
            var combinedId = ElementTag.GetCombinedId(groupId, elementId);
            state.IsImplicitVr = state.UseImplicitVr && (groupId != 0x0002);
            if (!state.IsImplicitVr && groupId != 0xfffe) // Sequence items are always implicit - hence 0xfffe.
            {
                state.ExplicitVr = new string(state.Reader.ReadChars(2));
            }

            // Always use Implicit VR if the tag is recognized in the dictionary.
            var vr = ValueTypes.Unknown;
            if (Dictionary.ContainsKey(combinedId))
            {
                var elementTag = Dictionary[combinedId];
                vr = elementTag.VR;
                Logger.Debug(elementTag.Name);
            }

            var value = new Value[0];
            switch (vr)
            {
                case ValueTypes.String:
                    value = ReadString(state);
                    break;
                case ValueTypes.DoubleString:
                    value = ReadDoubleString(state);
                    break;
                case ValueTypes.UShort:
                    value = ReadUShort(state);
                    break;
                case ValueTypes.Bytes:
                    value = ReadBytes(state);
                    break;
                case ValueTypes.Item:
                    value = ReadItem(state);
                    break;
                case ValueTypes.Separator:
                    value = ReadSeparator(state);
                    break;
                default:
                    ReadUnknown(state);
                    break;
            }

            // TransferSyntaxUID - See if implicit is specified. We hate implicit because shit cannot always be understood!
            state.UseImplicitVr = (combinedId == ReverseDictionary["TransferSyntaxUID"]) && (value[0].Text == "1.2.840.10008.1.2");
            var tag = Dictionary.ContainsKey(combinedId) ? Dictionary[combinedId] : null;

            return new Element()
            {
                GroupId = groupId, 
                ElementId = elementId,
                Value = value,
                Tag = tag
            };
        }

        private static Value[] ReadUShort(ReaderState state)
        {
            var length = ReadLength(state);
            var count = length / 2;

            var output = new Value[count];
            for (var i = 0; i < count; i++)
            {
                output[i] = new Value { Long = state.Reader.ReadUInt16() };
            }

            return output;
        }

        private static Value[] ReadDoubleString(ReaderState state)
        {
            var values = ReadString(state);
            var output = new Value[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                output[i] = new Value { Double = double.Parse(values[i].Text, NumberFormatInfo.InvariantInfo) };
            }

            return output;
        }

        private static Value[] ReadBytes(ReaderState state)
        {
            byte[] data = null;
            if (!state.IsImplicitVr)
            {
                state.Reader.ReadBytes(2);
            }

            var length = state.Reader.ReadUInt32();
            if (length == 0xffffffff) // Check for compressed data.
            {
                throw new IOException("Unable to read compressed data. Please convert using dcmdjpeg.exe");
            }
            else
            {
                data = state.Reader.ReadBytes((int)length);
            }

            return new[] { new Value { Bytes = data } };
        }

        private static Value[] ReadString(ReaderState state)
        {
            var length = ReadLength(state);
            var chars = state.Reader.ReadChars((int)length);
            var text = new string(chars);
            var values = text.Split('\\');

            var output = new Value[values.Length];
            for (var i = 0; i < output.Length; i++)
            {
                var value = values[i];
                while (value.EndsWith("\0"))
                {
                    value = value.Substring(0, value.Length - 1);
                }
                output[i] = new Value { Text = value };
            }

            return output;
        }

        private static Value[] ReadItem(ReaderState state)
        {
            Element element;
            string elementName;
            var items = new List<Element>();
            var length = state.Reader.ReadUInt32();
            var endItemPosition = length + state.Reader.BaseStream.Position;

            do
            {
                var groupId = state.Reader.ReadUInt16();
                var elementId = state.Reader.ReadUInt16();

                element = ReadElement(state, groupId, elementId);
                elementName = (element.Tag != null) ? element.Tag.Name : null;
                items.Add(element);

            } while (elementName != "ItemDelimitationItem" &&
                state.Reader.BaseStream.Position < endItemPosition);

            return new[] { new Value { Items = items } };
        }

        private static Value[] ReadSeparator(ReaderState state)
        {
            var length = state.Reader.ReadUInt32();
            return new[] { new Value { Long = length } };
        }

        private static void ReadUnknown(ReaderState state)
        {
            uint length;
            if (state.ExplicitVr == "OB" || state.ExplicitVr == "OW" ||
                state.ExplicitVr == "UN" || state.ExplicitVr == "SQ")
            {
                state.Reader.ReadBytes(2);
                length = state.Reader.ReadUInt32();
            }
            else
            {
                length = ReadLength(state);
            }

            if (state.ExplicitVr == "SQ")
            {
                var groupId = state.Reader.ReadUInt16();
                var elementId = state.Reader.ReadUInt16();
                ReadElement(state, groupId, elementId);
            }
            else
            {
                state.Reader.ReadBytes((int)length);
            }
        }

        private static uint ReadLength(ReaderState state)
        {
            return state.IsImplicitVr ? state.Reader.ReadUInt32() : state.Reader.ReadUInt16();
        }
    }
}
