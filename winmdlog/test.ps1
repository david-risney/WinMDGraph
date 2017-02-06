param(
    [switch] $Apply);

$exe = (dir bin\*\winmdlog.exe | sort LastWriteTime -desc)[0].fullname;

$idx = 0;

@(
# 0 Test covers: 
#   - IUri unprojection
#   - read only properties
#   - static methods
#   - activatable class
    "-match Windows.Foundation.Uri",

# 1 Test covers:
#   - IReadOnlyList/IReadOnlyCollection/IVectorView unprojection
#   - IIterable/IEnumerable unprojection
#   - non activatable class
    "-match Windows.Foundation.WwwFormUrlDecoder",

# 2 Test covers:
#   - events
#   - AsyncOperation
    "-match Windows.System.RemoteSystems.RemoteSystemSessionController",

# 3 Test covers:
#   - IDisposable unprojection
    "-match Windows.Devices.Adc.AdcChannel",

# 4 Test covers:
#   - System.TimeSpan unprojection
    "-match Windows.Devices.Gpio.GpioPin",

# 5 Test covers:
#   - Method parameter that is a reference
    "-match Windows.Devices.Gpio.GpioController"
) | %{
    $outPath = "tests\$idx.out.actual";
    if ($Apply) {
        $outPath = "tests\$idx.out.expected";
    }

    &$exe $_.Split(" ") | Out-File -Encoding utf8 -FilePath $outPath;
    if (!$Apply) {
        $diff = diff (gc tests\$idx.out.expected) (gc tests\$idx.out.actual);
        if ($diff) {
            "Mismatch in test ${idx}: $_";
        }
    }

    ++$idx;
}

