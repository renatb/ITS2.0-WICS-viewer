=========== System requirements:

1) Any Windows OS with Microsoft .NET v3.5 or higher version installed

2) csc.exe: Microsoft C# compiler included in Microsoft .NET Framework

3) SgmlReaderDll.dll library

=========== The command line to build its2wics.exe:

csc its2wics.cs /reference:SgmlReaderDll.dll

=========== The command line syntax for its2wics.exe:

its2wics.exe <input XML or HTML file> <output HTML file> [-t]

-t   Force output with tags for HTML ("markup" output mode)