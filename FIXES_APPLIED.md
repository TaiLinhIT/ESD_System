# ESD System - Fixes Applied

1. Fixed `A non ref-returning property or indexer may not be used as an out or ref value`.
   - `BackgroundDbWorker.QueueCount` is now backed by `_queueCount`.
   - `Interlocked.Increment/Decrement` operate on the backing field.
   - `QueueCount` is exposed using `Volatile.Read`.

2. Fixed WinForms target:
   - ESD.UI targets `net8.0-windows`.
   - `UseWindowsForms` remains enabled.

3. Fixed SerialPort reference:
   - ESD.Device references `System.IO.Ports` version 8.0.0.

4. Target frameworks are explicit per project:
   - Core/Device/Data/Service/API: `net8.0`
   - UI: `net8.0-windows`

5. Removed Visual Studio/build cache folders from this source ZIP.

Build on Windows with Visual Studio 2022 / .NET 8 SDK:
    dotnet restore ESD.System.sln
    dotnet build ESD.System.sln -c Debug

6. Converted ESD.UI from WinForms to WPF and added a Factory-style Dashboard + Wrist Strap History UI with built-in Segoe MDL2 icons.
