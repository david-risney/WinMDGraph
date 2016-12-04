To use WinMD.dll in powershell:

[System.Reflection.Assembly]::LoadFile("C:\Users\Dave\Development\WinMDGraph\WinMD\bin\Debug\WinMD.dll");
$types = New-Object WinMD.WinMDTypes -ArgumentList @(,((dir C:\windows\system32\winmetadata\*winmd).fullname))


$types.Types.GetEvents() | ?{ $_.Name -match "Changed" } | %{ 
  $e = $_; 
  $eName = $e.Name.Substring(0, $e.Name.Length - ("Changed").Length); 
  $e.DeclaringType.GetProperties() | ?{ $_.Name -match $eName } | %{
    $p = $_;
    $nameMatchKind = "partial";
    if ($p.Name -eq $e.Name) { $nameMatchKind = "exact"; }
    elseif ($p.Name.StartsWith($e.Name)) { $nameMatchKind = "start"; }
    elseif ($p.Name.EndsWith($e.Name)) { $nameMatchKind = "end"; }
    $commonPrefix = (@("Is", "Has", "Can") | ?{ $p.Name.StartsWith($_); }).Count -gt 0;
    New-Object PSObject -P (@{Event=$e;Property=$p;NameMatchKind=$nameMatchKind;CommonPrefix=$commonPrefix})
  }
}