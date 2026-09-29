namespace ESD.Core;

public enum DeviceTransport { Serial, Mock }
public enum DeviceProtocol { Raw, ModbusRtu }

public sealed record DeviceConfig(
    string Name,
    string PortName,
    int BaudRate,
    int DataBits,
    string Parity,
    int StopBits,
    DeviceTransport Transport,
    DeviceProtocol Protocol,
    byte SlaveId,
    int PollIntervalMs);

public sealed record EsdEvent(
    DateTime Timestamp,
    string Device,
    string EmployeeId,
    string EventType,
    string Status,
    string RawData,
    string Message);

public sealed record DeviceMessage(
    DateTime Timestamp,
    string Device,
    byte[] Data,
    string RawText);

public sealed record DbEvent(
    DateTime Timestamp,
    string Device,
    string EmployeeId,
    string EventType,
    string Status,
    string RawData,
    string Message);
