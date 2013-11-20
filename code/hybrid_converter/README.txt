Hybrid preview script, CSS, and visual markers:

\wics

jquery-1.9.1.min.js
wics.js

\wics\styles

main.css
tags.css

\wics\images

<visual markers>

To preview HTML files, copy wics folder to the same folder as the HTML files

=========== User Interface Parameters:

wics.js:

	  fragments: 'p,div',
- The list of HTML tags used as selectors to split the content into fragments
- If null value is set, the fragmentation is off

main.css:

wics-notranslate … .wics-withintext - the styles of ITS metadata categories
wics-image – the style of visual markers (see images folder)
wics-tip – the style of piece of content to which any metadata (metadata tips) are assigned
wics-no-tip – the style of piece of content without metadata
wics-fragment – the style of fragments of content
wics-fragment-selected – the style of selected fragment
wics-tip-selected – the style of selected fragment with any metadata tip
wics-note – the style of metadata tip
wics-note-selected – the style of selected metadata tip
wics-note-top – the style of upper frame of metadata tip
wics-note-close – the style of Close button in metadata tip
wics-note-name  – the style of header of metadata tip in upper tip frame
wics-note-text – the style of text of tip in bottom tip frame
wics-goto-window – the style of "Go to the Fragment" and "Go to the Tip" windows
wics-goto-num – the style of Edit element in "Go to *" windows.
wics-navbar – the style of control panel
wics-navbar I – the style of control panel buttons
wics-navbar I SPAN – the style of control panel button labels
wics-navbar I:hover – the style of control panel buttons under mouse pointer
wics-upperframe – the style of upper pane of content
wics-lowerframe – the style of bottom pane of metadata tips

tags.css:

- The styles applied to HTML and XML elements when markup preview mode selected in hybrid file format converter command line (-t)

=========== The resources to build its2wics.exe:

its2wics.cs
SgmlReaderDll.dll

=========== System requirements:

1) Any Windows OS with Microsoft .NET v3.5 or higher version installed

2) csc.exe: Microsoft C# compiler included in Microsoft .NET Framework

3) SgmlReaderDll.dll library

=========== The command line to build its2wics.exe:

csc its2wics.cs /reference:SgmlReaderDll.dll

=========== The command line syntax for its2wics.exe:

its2wics.exe <input XML or HTML file> <output HTML file> [-t]

-t   Force output with tags for HTML ("markup" output mode)

------------------------------------------------------------- Change Log:
19 Nov. 2013:
SgmlReaderDll.dll updated with an error fix
its2wics.cs updated

08 Nov. 2013:
wics.js updated