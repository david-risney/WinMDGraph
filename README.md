# WinMDGraph
Parse the WinRT metadata files (WinMD) and produce a GraphViz DOT describing the relationships between runtimeclasses.

The WinMDGraph tool uses .NET reflection APIs to parse WinMD files and produces as output a DOT file that describes the relationships between runtimeclasses. A DOT file describes graphs and can be turned into an image using http://www.graphviz.org/ tools.

![alt text](https://raw.githubusercontent.com/david-risney/WinMDGraph/master/examples/3/3.dot.png "Example of WinMDGraph on Win10 system metadata for Windows.Services.Map")

![alt text](https://raw.githubusercontent.com/david-risney/WinMDGraph/master/examples/1/1.dot.png "Example of WinMDGraph on Win10 system metadata for Windows.Data")

![alt text](https://raw.githubusercontent.com/david-risney/WinMDGraph/master/examples/2/2.dot.png "Example of WinMDGraph on Win10 system metadata for Windows.Web.Http")

## WinMD
WinMDGraph uses [WinMD.dll to parse the WinMD files](WinMD/README.md). You can also use WinMD.dll on its own in PowerShell or .NET.