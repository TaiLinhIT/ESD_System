using ESD.Core;
using ESD.Simulator;
using System.IO.Ports;

// ─────────────────────────────────────────────────────────────────────────────
//  ESD Hardware Simulator
//  Simulates a physical ESD wrist-strap device over a virtual COM port pair.
//
//  Setup (one-time):
//    1. Install com0com: https://sourceforge.net/projects/com0com/
//    2. Create a pair: COM10 ↔ COM11
//    3. ESD app  → listens on COM10  (ProtocolVersion: "ESD-V1", PortName: "COM10")
//    4. Simulator → connects to COM11 (this program)
// ─────────────────────────────────────────────────────────────────────────────

Console.OutputEncoding = System.Text.Encoding.UTF8;

PrintBanner();

// ── Choose COM port ───────────────────────────────────────────────────────────
var availablePorts = SerialPort.GetPortNames();
string simPort;

if (availablePorts.Length == 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\n  No COM ports found.");
    Console.WriteLine("  → Install com0com and create a virtual pair (e.g. COM10 ↔ COM11).");
    Console.ResetColor();
    Console.WriteLine("\n  Press any key to exit...");
    Console.ReadKey(true);
    return;
}

Console.WriteLine("\n  Available COM ports:");
for (int i = 0; i < availablePorts.Length; i++)
    Console.WriteLine($"    [{i + 1}] {availablePorts[i]}");

Console.Write("\n  Enter port number for SIMULATOR (e.g. COM11): ");
var portInput = Console.ReadLine()?.Trim() ?? "";

// Accept either "COM11" or just "11"
if (int.TryParse(portInput, out int idx) && idx >= 1 && idx <= availablePorts.Length)
    simPort = availablePorts[idx - 1];
else
    simPort = portInput.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
        ? portInput.ToUpper()
        : "COM11";

Console.Write("  Baud rate [9600]: ");
var baudInput = Console.ReadLine()?.Trim();
int baud = string.IsNullOrEmpty(baudInput) ? 9600 : int.Parse(baudInput);

// ── Open simulator ────────────────────────────────────────────────────────────
using var sim = new HardwareSimulator(simPort, baud, address: 0x01);

try
{
    sim.Open();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n  Failed to open {simPort}: {ex.Message}");
    Console.WriteLine("  Make sure com0com is installed and the port is not in use.");
    Console.ResetColor();
    Console.WriteLine("\n  Press any key to exit...");
    Console.ReadKey(true);
    return;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"\n  ✓ Simulator running on {simPort} @ {baud} baud");
Console.WriteLine($"  ✓ ESD app should connect to the OTHER end of the virtual pair.");
Console.ResetColor();

var autoLoop = new AutoEventLoop(sim, intervalMs: 2000);

PrintMenu();

// ── Main loop ─────────────────────────────────────────────────────────────────
byte empId = 1;

while (true)
{
    Console.Write("\n  > ");
    var input = Console.ReadLine()?.Trim().ToUpper() ?? "";

    switch (input)
    {
        case "A":
            if (autoLoop.IsRunning)
            {
                autoLoop.Stop();
                Console.WriteLine("  Auto mode stopped.");
            }
            else
            {
                autoLoop.Start();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  Auto mode started — sending events every 2 seconds.");
                Console.ResetColor();
            }
            break;

        case "C":
            sim.PushEvent(EsdEventType.DeviceConnected, 0, WristStrapStatus.NotConnected);
            break;

        case "D":
            sim.PushEvent(EsdEventType.DeviceDisconnected, 0, WristStrapStatus.NotConnected);
            break;

        case "I":
            Console.Write("  Employee ID (1-99) [default=1]: ");
            var empIn = Console.ReadLine()?.Trim();
            empId = string.IsNullOrEmpty(empIn) ? (byte)1 : byte.Parse(empIn);
            sim.PushEvent(EsdEventType.WorkerDetected, empId, WristStrapStatus.NotConnected);
            Console.WriteLine($"  → Worker detected: EMP{empId:0000}");
            break;

        case "R":
            sim.PushEvent(EsdEventType.WorkerRemoved, empId, WristStrapStatus.NotConnected);
            Console.WriteLine($"  → Worker removed: EMP{empId:0000}");
            break;

        case "S":
            sim.PushEvent(EsdEventType.WristStrapConnected, empId, WristStrapStatus.Ok);
            Console.WriteLine("  → Wrist strap CONNECTED — OK");
            break;

        case "X":
            sim.PushEvent(EsdEventType.WristStrapDisconnected, empId, WristStrapStatus.NotConnected);
            Console.WriteLine("  → Wrist strap DISCONNECTED");
            break;

        case "P":
            sim.PushEvent(EsdEventType.EsdTestPass, empId, WristStrapStatus.Ok);
            Console.WriteLine("  → ESD Test PASS");
            break;

        case "F":
            sim.PushEvent(EsdEventType.EsdTestFail, empId, WristStrapStatus.Ng);
            Console.WriteLine("  → ESD Test FAIL (NG)");
            break;

        case "W":
            sim.PushEvent(EsdEventType.EsdTestFail, empId, WristStrapStatus.Warning);
            Console.WriteLine("  → ESD Test WARNING");
            break;

        case "L":
            sim.PushAlarm($"ALARM EMP{empId:0000} STRAP_VIOLATION");
            Console.WriteLine("  → Alarm sent");
            break;

        case "M":
            PrintMenu();
            break;

        case "Q":
        case "EXIT":
            autoLoop.Stop();
            Console.WriteLine("\n  Simulator stopped. Goodbye.");
            return;

        default:
            Console.WriteLine("  Unknown command. Press M for menu.");
            break;
    }
}

// ─────────────────────────────────────────────────────────────────────────────

static void PrintBanner()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine();
    Console.WriteLine("  ╔══════════════════════════════════════════════╗");
    Console.WriteLine("  ║     ESD Hardware Simulator  v1.0             ║");
    Console.WriteLine("  ║     ESD Protocol V1 — COM Port Edition       ║");
    Console.WriteLine("  ╚══════════════════════════════════════════════╝");
    Console.ResetColor();
}

static void PrintMenu()
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("  ┌─────────────────────────────────────────────┐");
    Console.WriteLine("  │  COMMANDS                                   │");
    Console.WriteLine("  ├─────────────────────────────────────────────┤");
    Console.WriteLine("  │  A  — Toggle AUTO mode (events every 2s)   │");
    Console.WriteLine("  │  C  — Device CONNECTED                     │");
    Console.WriteLine("  │  D  — Device DISCONNECTED                  │");
    Console.WriteLine("  │  I  — Worker INSTALL (detected)            │");
    Console.WriteLine("  │  R  — Worker REMOVE                        │");
    Console.WriteLine("  │  S  — Wrist Strap CONNECT (OK)             │");
    Console.WriteLine("  │  X  — Wrist Strap DISCONNECT               │");
    Console.WriteLine("  │  P  — ESD Test PASS                        │");
    Console.WriteLine("  │  F  — ESD Test FAIL (NG)                   │");
    Console.WriteLine("  │  W  — ESD Test WARNING                     │");
    Console.WriteLine("  │  L  — Send ALARM                           │");
    Console.WriteLine("  │  M  — Show this menu                       │");
    Console.WriteLine("  │  Q  — Quit                                 │");
    Console.WriteLine("  └─────────────────────────────────────────────┘");
    Console.ResetColor();
}
