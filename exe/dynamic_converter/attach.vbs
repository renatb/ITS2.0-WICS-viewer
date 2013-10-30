If WScript.Arguments.Count <> 1 then
  WScript.Echo "Drag And Drop one html/xhtml file on this VBS to attach the WICS code to them"
  WScript.Quit
end If

FindAndReplace WScript.Arguments.Item(0)
WScript.Echo "Operation Complete"

function FindAndReplace(strFilename)
    Set inputFile = CreateObject("Scripting.FileSystemObject").OpenTextFile(strFilename, 1)
    strInputFile = inputFile.ReadAll
    inputFile.Close
    Set inputFile = Nothing
    
	
	If InStr(strInputFile, "<!-- The beginning of the WICS code -->") then
		WScript.Echo "WICS Code already attached. Operation Failed."
		WScript.Quit
	end if
	
	If InStr(strInputFile, "</head>") = 0 then
		WScript.Echo "No such </head> tag. Operation Failed."
		WScript.Quit
	end if
	
	Set outputFile = CreateObject("Scripting.FileSystemObject").OpenTextFile(strFilename,2,true)
	
    outputFile.Write Replace(strInputFile, "</head>", "<!-- The beginning of the WICS code --><LINK href='css/wics_stylesheet.css' rel='stylesheet' type='text/css'/><script type='text/javascript' src='scripts/jquery-1.9.1.min.js'></script><script type='text/javascript' src='scripts/wics.js'></script><!-- The end of the WICS code --></head>")
    outputFile.Close
    Set outputFile = Nothing
end function 