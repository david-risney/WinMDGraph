#WinMD
The WinMD.dll module uses the builtin .NET WinMD parsing to expose the WinRT type system of specified WinMD files. You can use it from PowerShell or .NET

##PowerShell
To use WinMD.dll in powershell to load the metadata from the current OS:

    [System.Reflection.Assembly]::LoadFile("C:\Users\Dave\Development\WinMDGraph\WinMD\bin\Debug\WinMD.dll");
    $typeSystem = New-Object WinMD.WinMDTypes;
    $typeSystem.Types;

Or to load specific metadata:

    [System.Reflection.Assembly]::LoadFile("C:\Users\Dave\Development\WinMDGraph\WinMD\bin\Debug\WinMD.dll");
    $typeSystem = New-Object WinMD.WinMDTypes -ArgumentList @(,("C:\example1.winmd","C:\example2.winmd"));

##Examples
###Changed events and their corresponding properties

    $results = $typeSystem.Types.GetEvents() | ?{ $_.Name -match "Changed" } | %{ 
      $e = $_; 
      $eName = $e.Name.Substring(0, $e.Name.Length - ("Changed").Length); 
      $e.DeclaringType.GetProperties() | ?{ $_.Name -match $eName } | %{
        $p = $_;
        $nameMatchKind = "partial";
        if ($p.Name -eq $eName) { $nameMatchKind = "exact"; }
        elseif ($p.Name.StartsWith($eName)) { $nameMatchKind = "start"; }
        elseif ($p.Name.EndsWith($eName)) { $nameMatchKind = "end"; }
        $commonPrefix = (@("Is", "Has", "Can") | ?{ $p.Name.StartsWith($_); }).Count -gt 0;
        New-Object PSObject -P (@{Event=$e;Property=$p;NameMatchKind=$nameMatchKind;CommonPrefix=$commonPrefix})
      }
    }
### Events with IInspectable event args

```
[System.Reflection.Assembly]::LoadFile("C:\Users\davris\Source\Repos\WinMDGraph\WinMD\bin\Debug\WinMD.dll");
$types = (New-Object WinMD.WinMDTypes).Types;
# Print all matching event names 
$types | %{ $_.GetEvents() } | ?{ $gp = $_.EventHandlerType.GenericTypeArguments; $gp.Count -eq 2 -and $gp[1].Name -eq "Object" } | %{ $_.DeclaringType.FullName + "." + $_.Name } | sort -uniq
# Count matching and non-matching
$types | %{ $_.GetEvents() } | %{ $gp = $_.EventHandlerType.GenericTypeArguments; $gp.Count -eq 2 -and $gp[1].Name -eq "Object" } | group
```

Non-IInspectable | IInspectable | Total
---|---|---
7682 | 581 | 8263

### Name length
```
$types | ?{ $_.IsInterface } | %{
    $_.Name;
    $_.GetEvents().Name;
    $_.GetMethods() | ?{ !$_.IsSpecialName } | %{ $_.Name; };
    $_.GetMethods().CustomAttributes | ?{ $_.AttributeType.Name -eq "OverloadAttribute" } | %{ $_.ConstructorArguments.Value };
    $_.GetProperties().Name
} | sort Length -desc | group Length
``` 

### Boolean property first words
```
function NameToWords {
  param($Name);
  $start = 0;
  for ($idx = 0; $idx -lt $Name.Length; ++$idx) {
    if ($idx -gt 0 -and $Name[$idx - 1] -cmatch "[a-z]" -and $Name[$idx] -cmatch "[A-Z]") {
      $Name.Substring($start, $idx - $start);
      $start = $idx;
    }
    elseif ($idx -gt 0 -and $idx -lt $Name.Length -and $Name[$idx - 1] -cmatch "[A-Z]" -and $Name[$idx] -cmatch "[A-Z]" -and $Name[$idx + 1] -cmatch "[a-z]") {
      $Name.Substring($start, $idx - $start);
      $start = $idx;
    }
  }
  $Name.Substring($start, $Name.Length - $start);
}

[System.Reflection.Assembly]::LoadFile("C:\Users\Dave\Development\WinMDGraph\WinMD\bin\Debug\WinMD.dll");
$typeSystem = New-Object WinMD.WinMDTypes
$names = $typeSystem.Types.GetProperties() | ?{ $_.propertyType.Name -match "Boolean"; } | %{ New-Object PSObject -P @{"Prefix"=@(NameToWords $_.Name)[0];"Name"=($_.Name)} }
$names | group prefix | sort count
```

Count | Prefix
------|-------
2864 | Is
535 | Allow
364 | Can
257 | Use
164 | Exit
96 | Has
90 | Handled
74 | Auto
48 | Are
42 | Supported
24 | Enabled
24 | Should
24 | Cancel
23 | Supports
22 | Visible
22 | Show
20 | Enable
12 | Satisfied
11 | Compulsory
11 | Always
10 | One
10 | In
10 | Include
8 | Red
8 | Single
8 | Key
8 | No
8 | Consume
8 | Prevent
8 | Must
7 | Available
6 | Language
6 | Ignore
6 | Was
6 | Succeeded
6 | Data
6 | All
6 | Search
6 | Bordered
6 | Audio
6 | Power
6 | Suppress
6 | Active
6 | Require
6 | Disable
