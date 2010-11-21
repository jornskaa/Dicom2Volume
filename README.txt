Dicom2Volume (C# application/library) 


Loads DICOM files and extracts image data, orientation, positions, scale and metadata 
like WindowLevel. DICOM slices are sorted based on calculated SliceLocation and image 
and metadata is exported to XML, RAW and DDS (Direct Draw Surface - ideal for Direct3D 
loading). Optional tar.gz compression can also be applied. Ideal if you want the 
simplest way of working with volume medical data without the hassle of DICOM. Can be 
used as standalone application or as a library.

****************************************************************************************

This product uses the OFFIS DICOM Toolkit DCMTK (C) 1993-2008, OFFIS e.V. for converting
DICOM to uncompressed, little endian, explicit vr representation which again can be read
by the C# DICOM loader. The tool used is "dcmdjpeg.exe" and is distributed in this 
application in its binary form.

Copyright (C) 1993-2008, OFFIS e.V. 
All rights reserved. 

Redistribution and use in source and binary forms, with or without modification, are 
permitted provided that the following conditions are met: 

* Redistributions of source code must retain the above copyright notice, this list of 
  conditions and the following disclaimer. 
* Redistributions in binary form must reproduce the above copyright notice, this list 
  of conditions and the following disclaimer in the documentation and/or other 
  materials provided with the distribution. 
* Neither the name of OFFIS e.V. nor the names of its contributors may be used to 
  endorse or promote products derived from this software without specific prior 
  written permission. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY 
EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT 
OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

****************************************************************************************
