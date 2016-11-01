.\bin\Debug\WinMDGraph.exe -file C:\Windows\System32\WinMetadata\Windows.Data.winmd -match Windows.Data | Out-File -Encoding utf8 .\examples\1\1.dot
dot.exe .\examples\1\1.dot -Tpng -O 

.\bin\Debug\WinMDGraph.exe -file C:\Windows\System32\WinMetadata\Windows.Web.winmd -match Windows.Web.Http | Out-File -Encoding utf8 .\examples\2\2.dot
dot.exe .\examples\2\2.dot -Tpng -O 

