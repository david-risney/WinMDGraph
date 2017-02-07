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
#   - IDisposable/ICloseable unprojection
    "-match Windows.Devices.Adc.AdcChannel",

# 4 Test covers:
#   - System.TimeSpan unprojection
    "-match Windows.Devices.Gpio.GpioPin",

# 5 Test covers:
#   - Method parameter that is a reference
    "-match Windows.Devices.Gpio.GpioController",

# 6 Test covers:
#   - IList unprojection
    "-match Windows.Devices.Gpio.GpioChangeReader",

# 7 Test covers:
#   - Static only class
    "-match Windows.Devices.Custom.KnownDeviceTypes",

# 8 Test covers:
#	- DateTime unprojection
	"-match Windows.Devices.SmartCards.SmartCardPinResetRequest",

# 9 Test covers:
#	- Nullable unprojection
	"-match Windows.Devices.Power.BatteryReport",

# 10 Test covers:
#	- IDictionary unprojection
	"-match Windows.Devices.Sms.SmsWapMessage",

# 11 Test covers:
#	- IReadOnlyDictionary unprojection
	"-match Windows.Devices.AllJoyn.AllJoynAboutDataView",

# 12 Test covers:
#	- IKeyValuepair unprojection
	"-match Windows.Devices.PointOfService.ClaimedBarcodeScanner"
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

