namespace ESD.API.Dto;

/// <summary>Device projection for GET /api/devices.</summary>
public sealed record DeviceDto(
    string            Name,
    byte              Address,
    string            Protocol,
    string            PortName,
    bool              IsConnected,
    bool              IsOnline,
    string            State,
    string?           LastEventType,
    string?           LastEmployeeId,
    string?           LastStrapStatus,
    DateTime?         LastEventAt);

/// <summary>System status projection for GET /api/status.</summary>
public sealed record SystemStatusDto(
    string    Service,
    string    Status,
    int       TotalDevices,
    int       ConnectedDevices,
    int       OnlineDevices,
    int       DbQueueLength,
    DateTime  ServerTimeUtc);

/// <summary>Event projection for GET /api/events and SSE stream.</summary>
public sealed record EventDto(
    long      Id,
    DateTime  Timestamp,
    string    DeviceName,
    string?   EmployeeId,
    string    EventType,
    string    Status,
    string?   RawData,
    string?   Message);

/// <summary>Health check response.</summary>
public sealed record HealthDto(
    string Status,
    string Version,
    DateTime Utc);
