****************************************************************************************
Dicom2Volume (C# application/library) 
****************************************************************************************
Loads DICOM files and extracts image data, orientation, positions, scale and metadata 
like WindowLevel. DICOM slices are sorted based on calculated SliceLocation and image 
and metadata is exported to XML, RAW and DDS (Direct Draw Surface - ideal for Direct3D 
loading). Optional tar.gz compression can also be applied. Ideal if you want the 
simplest way of working with volume medical data without the hassle of DICOM. Can be 
used as standalone application or as a library.
****************************************************************************************

Example volume metadata:

  <?xml version="1.0" ?> 
  <VolumeData xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
	<Rows>512</Rows> 
	<Columns>512</Columns> 
	<Slices>361</Slices> 
	<Width>217</Width> 
	<Height>217</Height> 
	<Depth>252</Depth> 
	<WindowWidth>400</WindowWidth> 
	<WindowCenter>60</WindowCenter> 
	<RescaleIntercept>-1000</RescaleIntercept> 
	<RescaleSlope>1</RescaleSlope> 
	<ImageOrientationPatient>
	<double>1</double> 
	<double>0</double> 
	<double>0</double> 
	<double>0</double> 
	<double>1</double> 
	<double>0</double> 
	</ImageOrientationPatient>
	<ImagePositionPatient>
	<double>-112</double> 
	<double>-17.333334</double> 
	<double>-792</double> 
	</ImagePositionPatient>
	<FirstSliceLocation>-792</FirstSliceLocation> 
	<LastSliceLocation>-540</LastSliceLocation> 
	<MinIntensity>0</MinIntensity> 
	<MaxIntensity>4095</MaxIntensity> 
  </VolumeData>


****************************************************************************************
The idea is that this data is everything needed to successfully perform volume rendering
of the DICOM dataset. Currently only MONOCHROME2 Photometric Interpretation is supported.
However decompression is supported through external converters like dcmtk and gdcm. The 
process also anonymizes the data by ignoring all tags not relating to rendering.
****************************************************************************************
