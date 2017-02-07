param(
    [switch] $Apply);

$exe = (dir bin\*\winmdlog.exe | sort LastWriteTime -desc)[0].fullname;

$idx = 0;

@(
	#   - IUri unprojection
	#   - read only properties
	#   - static methods
	#   - activatable class
    "-match Windows.Foundation.Uri",

	#   - IReadOnlyList/IReadOnlyCollection/IVectorView unprojection
	#   - IIterable/IEnumerable unprojection
	#   - non activatable class
    "-match Windows.Foundation.WwwFormUrlDecoder",

	#   - events
	#   - AsyncOperation
    "-match Windows.System.RemoteSystems.RemoteSystemSessionController",

	#   - IDisposable/ICloseable unprojection
    "-match Windows.Devices.Adc.AdcChannel",

	#   - System.TimeSpan unprojection
    "-match Windows.Devices.Gpio.GpioPin",

	#   - Method parameter that is a reference
    "-match Windows.Devices.Gpio.GpioController",

	#   - IList unprojection
    "-match Windows.Devices.Gpio.GpioChangeReader",

	#   - Static only class
    "-match Windows.Devices.Custom.KnownDeviceTypes",

	#	- DateTime unprojection
	"-match Windows.Devices.SmartCards.SmartCardPinResetRequest",

	#	- Nullable unprojection
	"-match Windows.Devices.Power.BatteryReport",

	#	- IDictionary unprojection
	"-match Windows.Devices.Sms.SmsWapMessage",

	#	- IReadOnlyDictionary unprojection
	"-match Windows.Devices.AllJoyn.AllJoynAboutDataView",

	#	- IKeyValuepair unprojection
	"-match Windows.Devices.PointOfService.ClaimedBarcodeScanner",

	#	- System.Exception unprojection
	"-match Windows.ApplicationModel.PackageStagingEventArgs",

	#	- System.Object unprojection
	"-match Windows.ApplicationModel.Appointments.AppointmentCalendarSyncManager",

	#	- Delegate (no stub generated)
	"-match Windows.ApplicationModel.Background.BackgroundTaskCanceledEventHandler",

	#	- ICollection unprojection
	"-match Windows.ApplicationModel.DataTransfer.DataPackagePropertySet",

	#	- System.EventHandler unprojection
	"-match Windows.ApplicationModel.DataTransfer.Clipboard"
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

