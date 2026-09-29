# ESD System

Architecture:

ESD.Device
  - Serial RS232/RS485 transport
  - Mock device
  - Modbus RTU CRC/frame builder

ESD.Core
  - Models
  - State Machine
  - Interfaces

ESD.Service
  - Device Manager
  - Event Manager
  - Background DB Queue
  - JSON configuration
  - File logger
  - Runtime orchestration

ESD.Data
  - MySQL repository

ESD.API
  - REST API
  - /api/status
  - /api/devices

ESD.UI
  - WinForms real-time monitor

## 1. Requirements

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 with .NET desktop development
- MySQL 8.x (only required for database persistence)

## 2. Run immediately without hardware

Open:

ESD.System.sln

Set `ESD.UI` as Startup Project.

Run.

The default config contains `ESD-MOCK-01`, which generates INSTALL/REMOVE events every ~1.5 seconds.

This demonstrates:

Device -> Event -> UI immediately
               |
               +-> Queue -> MySQL worker

The UI does NOT wait for MySQL before displaying an event.

## 3. MySQL

Create the database:

mysql -u root -p < database/schema.sql

Then edit:

src/ESD.UI/config/appsettings.json

Example:

"DatabaseConnectionString": "Server=localhost;Port=3306;Database=esd_system;User ID=root;Password=YOUR_PASSWORD;"

If MySQL is not running, UI real-time events still work. DB errors are written to logs.

## 4. RS232 / RS485

For a real serial device, change:

"Transport": "Serial"

and:

"PortName": "COM3"

Example:

{
  "Name": "ESD-RS485-01",
  "PortName": "COM5",
  "BaudRate": 9600,
  "DataBits": 8,
  "Parity": "None",
  "StopBits": 1,
  "Transport": "Serial",
  "Protocol": "ModbusRtu",
  "SlaveId": 1,
  "PollIntervalMs": 1000
}

Important:
RS485 is the physical electrical interface. Modbus RTU is a protocol commonly used on top of RS485.
RS232 is point-to-point and normally uses one COM port per device.

## 5. Real-time architecture

Device callback:
SerialPort.DataReceived
        |
        v
DeviceManager
        |
        v
StateMachine
        |
        v
EventManager.Publish()
        |
        +----> WinForms event handler -> UI
        |
        +----> Channel<DbEvent> -> BackgroundDbWorker -> MySQL

Therefore the UI does not wait for INSERT SQL.

## 6. Configuration

Configuration is stored in:

ESD.UI/config/appsettings.json
ESD.API/config/appsettings.json

The application automatically creates the config if it does not exist.

## 7. Logs

Logs are created under:

logs/YYYY-MM-DD.log

Each received serial/mock frame is logged with timestamp and HEX data.

## 8. API

Run ESD.API.

Default endpoints:

GET http://localhost:5080/
GET http://localhost:5080/api/status
GET http://localhost:5080/api/devices

In Visual Studio, set ESD.API as startup project if you want to test the REST service.

## 9. Important production note

The included SerialDevice demonstrates the complete receive path, but a production ESD device should add:
- exact frame parser for the vendor protocol
- Modbus polling/register map
- response timeout
- retry policy
- CRC validation before accepting a frame
- reconnect/backoff
- device heartbeat
- duplicate event filtering
- bounded queue / persistent outbox if no data loss is allowed
- authentication for API
- Windows Service hosting for ESD.Service

This project intentionally keeps the architecture clean so those pieces can be added without changing the UI/DB layers.

## UI update
`ESD.UI` is now a WPF application targeting `net8.0-windows`.
The UI includes:
- Factory-style blue navigation sidebar with Segoe MDL2 icons
- Dashboard KPI cards
- Compliance trend visualization
- Station status breakdown
- Real-time wrist strap event table
- Wrist Strap Usage History screen
- Devices / Stations screen
- Settings screen
- Real-time event updates remain independent from the background database queue

The visual structure is inspired by the supplied ESD Control / Wrist Strap Usage History reference.
# ESD_System
