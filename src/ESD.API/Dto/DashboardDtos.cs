namespace ESD.API.Dto;

/// <summary>Aggregates returned by GET /api/dashboard/summary.</summary>
public sealed record DashboardSummaryDto(
    DateTime  Date,
    int       TotalEvents,
    int       OkEvents,
    int       NgEvents,
    int       WarningEvents,
    double    CompliancePercent,
    int       ActiveStations,
    int       OnlineDevices,
    int       TotalDevices,
    int       AlarmCount,
    int       DbQueueLength);

/// <summary>One bar of the hourly compliance trend chart.</summary>
public sealed record HourlyTrendDto(int Hour, int Total, int Ok, int Ng);

/// <summary>Station card with OK/NG/Warning totals for the day.</summary>
public sealed record StationSummaryDto(
    string  DeviceName,
    int     Total,
    int     Ok,
    int     Ng,
    int     Warning,
    double  CompliancePercent,
    string  State,          // EsdState from the live tracker
    bool    IsConnected,
    string? LastEventType,
    DateTime? LastEventAt);

/// <summary>Operator activity row (leader board).</summary>
public sealed record EmployeeActivityDto(
    string    EmployeeId,
    int       Total,
    int       Ok,
    int       Ng,
    double    CompliancePercent,
    DateTime  LastEvent);
