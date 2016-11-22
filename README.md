# WinMDGraph
Parse the WinRT metadata files (WinMD) and produce a GraphViz DOT describing the relationships between runtimeclasses.

The WinMDGraph tool uses .NET reflection APIs to parse WinMD files and produces as output a DOT file that describes the relationships between runtimeclasses. A DOT file describes graphs and can be turned into an image using http://www.graphviz.org/ tools.

![alt text](https://raw.githubusercontent.com/david-risney/WinMDGraph/master/examples/3/3.dot.png "Example of WinMDGraph on Win10 system metadata for Windows.Services.Map")

![alt text](https://raw.githubusercontent.com/david-risney/WinMDGraph/master/examples/1/1.dot.png "Example of WinMDGraph on Win10 system metadata for Windows.Data")

![alt text](https://raw.githubusercontent.com/david-risney/WinMDGraph/master/examples/2/2.dot.png "Example of WinMDGraph on Win10 system metadata for Windows.Web.Http")

# Statistics
## Boolean property first words
```
[System.Reflection.Assembly]::LoadFile("C:\Users\Dave\Development\WinMDGraph\WinMD\bin\Debug\WinMD.dll");
$types = (New-Object WinMD.WinMDTypes -ArgumentList @(,((dir C:\windows\system32\winmetadata\*winmd).fullname))).Types;
$names = $types.GetProperties() | ?{ $_.propertyType.Name -match "Boolean"; } | %{ New-Object PSObject -P @{"Prefix"=@(NameToWords $_.Name)[0];"Name"=($_.Name)} }
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
5 | Emphasized
5 | Remember
5 | Horizontally
5 | Vertically
5 | Hardware
4 | Selection
4 | Wait
4 | Year
4 | Cross
4 | Assistant
4 | Thumbnail
4 | Focus
4 | Same
4 | Is3D
4 | Muted
4 | Adjacent
4 | Day
4 | Shows
4 | Accepts
4 | Business
4 | Transit
4 | Month
4 | Stroke
4 | Make
4 | Roaming
4 | Canceled
4 | Removed
4 | Stereo
4 | Notify
4 | Keep
4 | Send
4 | Prefer
4 | Real
4 | Incoming
4 | Maintain
4 | Shuffle
4 | Value
4 | Initial
4 | Needs
4 | Concurrent
4 | Selected
3 | Bypass
3 | Minimizable
3 | Prelaunch
3 | Maximizable
3 | Is180Rotation
2 | Extend
2 | Request
2 | Pending
2 | Update
2 | Hides
2 | Change
2 | Ready
2 | Disabled
2 | Choose
2 | Mirrored
2 | Specified
2 | Confirmed
2 | Deployment
2 | Dependency
2 | Encipher
2 | Crl
2 | Serialize
2 | Background
2 | Dont
2 | Digital
2 | High
2 | Duplex
2 | Draw
2 | Terminate
2 | Scan
2 | Suspicious
2 | Audit
2 | Exclude
2 | Revocation
2 | Checked
2 | Photo
2 | Repeating
2 | Bounds
2 | Network
2 | Current
2 | Multicast
2 | Non
2 | Authority
2 | Caller
2 | Fit
2 | Ensured
2 | Fully
2 | Append
2 | Requested
2 | Camera
2 | Time
2 | Projection
2 | Mute
2 | Default
2 | Lock
2 | Manipulation
2 | Input
2 | Usable
2 | Carrier
2 | Shrink
2 | Credential
2 | Touch
2 | Recognized
2 | Break
2 | Remote
2 | Stopped
2 | Expires
2 | Self
2 | Animations
2 | Clear
2 | Returned
2 | Package
2 | Servicing
2 | Address
2 | Cursor
2 | Overview
2 | Street
2 | Design
2 | Landmarks
2 | Pedestrian
2 | Zoom
2 | Tampered
2 | Confirmation
2 | License
2 | Control
2 | Caret
2 | Stop
2 | Automatic
2 | Http
2 | Hidden
2 | Contains
2 | Secure
2 | Validate
2 | Resolve
2 | Prohibit
2 | Element
2 | Traffic
2 | Discontinuous
2 | Over
2 | Disallow
2 | Mandatory
2 | Compliant
2 | Boolean
2 | Display
2 | Name
2 | Account
2 | Synchronous
2 | Approaching
2 | Treat
2 | Email
2 | Full
2 | Does
2 | Card
2 | Cal
2 | Bring
2 | Modified
2 | Not
2 | Complete
2 | Animation
2 | Outgoing
2 | Contacts
2 | Calendar
1 | Software
1 | Transmitter
