$exe = (dir bin\*\winmdlog.exe | sort LastWriteTime -desc)[0].fullname;

$idx = 0;

@(
    "-match Windows.Foundation.Uri",
    "-match Windows.Foundation.WwwFormUrlDecoder",
    "-match Windows.System.RemoteSystems.RemoteSystemSessionController"
) | %{
    &$exe $_.Split(" ") > "tests\$idx.out.actual";
    $diff = diff (gc tests\$idx.out.expected) (gc tests\$idx.out.actual);
    if ($diff) {
        "Mismatch in test ${idx}: $_";
    }

    ++$idx;
}

