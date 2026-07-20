# NetworkSpeedTray

NetworkSpeedTray is a small, dependency-free Windows utility that displays
system-wide download and upload speeds from the notification area.

## Features

- Samples traffic across active, non-loopback network adapters once per second.
- Shows current download and upload rates in the tray tooltip.
- Provides an optional always-on-top floating speed widget.
- Keeps the floating widget visible at the same position across Windows 11
  virtual desktops.
- Supports dragging, click-through mode, opacity choices, and several font sizes.
- Remembers the widget position and appearance between sessions.
- Can start automatically when the current user signs in.
- Uses a single static tray icon and prevents duplicate application instances.

## Requirements

- Windows 11 (Windows 10 is declared compatible, but virtual-desktop pinning is
  intended for Windows 11).
- .NET Framework 4.8.
- PowerShell to run the included build script.

No NuGet packages or third-party dependencies are required. The build script
uses the C# compiler included with .NET Framework on Windows.

## Build

Open PowerShell in the repository directory and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The compiled application is written to:

```text
NetworkSpeedTray.exe
```

## Run

Launch `NetworkSpeedTray.exe`. The application runs in the notification area
without opening a normal window.

- Hover over the tray icon to see the current rates.
- Double-click the tray icon to show or hide the floating widget.
- Right-click the tray icon to configure the widget, enable startup, or exit.
- Disable click-through mode before dragging the widget to a new position.

The application stores its widget settings in:

```text
%AppData%\NetworkSpeedTray\settings.ini
```

The **Run at startup** option creates a per-user entry under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; administrator rights are
not required.

## How traffic is measured

The application reads cumulative IPv4 byte counters from each active network
adapter, excluding loopback and tunnel adapters, and calculates the rate from
the elapsed time between samples. Values use binary units: 1 KB equals 1024
bytes.

Because counters are summed across adapters, systems that expose the same
traffic through multiple active or virtual adapters may report a higher total.

## Windows virtual desktops

Windows' public virtual-desktop API can identify and move windows but cannot pin
a window to every desktop. The floating widget therefore uses the Windows shell
interface behind Task View's **Show this window on all desktops** behavior. The
feature fails safely if that internal interface changes in a future Windows
release.

## Privacy

NetworkSpeedTray reads local network-interface counters only. It does not
inspect packet contents, contact an external service, or collect telemetry.

## License

This project is available under the [MIT License](LICENSE).

