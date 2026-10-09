using ESD.API.Dto;
using ESD.Core;
using ESD.Data;
using ESD.Service;
using Microsoft.AspNetCore.Mvc;

namespace ESD.API.Controllers;

/// <summary>Dashboard aggregates: KPIs, trends, station cards, operators.</summary>
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly EsdEventQuery       _query;
    private readonly EsdRuntime          _runtime;
    private readonly DeviceStateTracker  _tracker;

    public DashboardController(
        EsdEventQuery query,
        EsdRuntime runtime,
        DeviceStateTracker tracker)
    {
        _query   = query;
        _runtime = runtime;
        _tracker = tracker;
    }

    /// <summary>KPI summary card for today.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? day = null, CancellationToken ct = default)
    {
        var summary = await _query.GetTodaySummaryAsync(day, ct);
        var states  = _tracker.GetStates();

        var compliance = (summary.Ok + summary.Ng + summary.Warning) switch
        {
            0 => 100.0,
            var tested => Math.Round(summary.Ok * 100.0 / tested, 1),
        };

        var dto = new DashboardSummaryDto(
            Date:               summary.Date,
            TotalEvents:        summary.Total,
            OkEvents:           summary.Ok,
            NgEvents:           summary.Ng,
            WarningEvents:      summary.Warning,
            CompliancePercent:  compliance,
            ActiveStations:     Math.Max(summary.ActiveStations, states.Count),
            OnlineDevices:      _runtime.Devices.Devices.Count(d => d.IsConnected),
            TotalDevices:       _runtime.Devices.Devices.Count,
            AlarmCount:         summary.Alarms,
            DbQueueLength:      _runtime.DbWorker.QueueCount);

        return Ok(dto);
    }

    /// <summary>Hourly OK/NG trend for the trend chart.</summary>
    [HttpGet("hourly-trend")]
    [ProducesResponseType(typeof(IReadOnlyList<HourlyTrendDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHourlyTrend(
        [FromQuery] DateTime? day = null, CancellationToken ct = default)
    {
        var points = await _query.GetHourlyTrendAsync(day, ct);
        return Ok(points.Select(p => new HourlyTrendDto(p.Hour, p.Total, p.Ok, p.Ng)).ToList());
    }

    /// <summary>Station cards: live state + today's OK/NG totals.</summary>
    [HttpGet("stations")]
    [ProducesResponseType(typeof(IReadOnlyList<StationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStations(
        [FromQuery] DateTime? day = null, CancellationToken ct = default)
    {
        var breakdown = await _query.GetStationBreakdownAsync(day, ct);
        var states    = _tracker.GetStates();

        // Merge DB aggregates with live tracker state.
        // A station with no events today still appears if it is known.
        var allNames = breakdown.Select(b => b.DeviceName)
            .Union(states.Select(s => s.DeviceName))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var result = new List<StationSummaryDto>();

        foreach (var name in allNames)
        {
            var agg   = breakdown.FirstOrDefault(b =>
                b.DeviceName.Equals(name, StringComparison.OrdinalIgnoreCase));
            var state = states.FirstOrDefault(s =>
                s.DeviceName.Equals(name, StringComparison.OrdinalIgnoreCase));

            var tested = (agg?.Ok ?? 0) + (agg?.Ng ?? 0) + (agg?.Warning ?? 0);
            var compliance = tested == 0 ? 100.0
                : Math.Round((agg?.Ok ?? 0) * 100.0 / tested, 1);

            result.Add(new StationSummaryDto(
                DeviceName:      name,
                Total:           agg?.Total ?? 0,
                Ok:              agg?.Ok ?? 0,
                Ng:              agg?.Ng ?? 0,
                Warning:         agg?.Warning ?? 0,
                CompliancePercent: compliance,
                State:           state?.State.ToString() ?? "Unknown",
                IsConnected:     state?.IsConnected ?? false,
                LastEventType:   state?.LastEvent?.EventType.ToString(),
                LastEventAt:     state?.LastEventAt));
        }

        return Ok(result);
    }

    /// <summary>Operator leader board for today.</summary>
    [HttpGet("employees")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeActivityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] DateTime? day = null,
        [FromQuery] int count = 10,
        CancellationToken ct = default)
    {
        var rows = await _query.GetTopEmployeesAsync(day, Math.Clamp(count, 1, 100), ct);

        var dto = rows.Select(r => new EmployeeActivityDto(
            EmployeeId:        r.EmployeeId,
            Total:             r.Total,
            Ok:                r.Ok,
            Ng:                r.Ng,
            CompliancePercent: (r.Ok + r.Ng) == 0 ? 100.0
                : Math.Round(r.Ok * 100.0 / (r.Ok + r.Ng), 1),
            LastEvent:         r.LastEvent)).ToList();

        return Ok(dto);
    }
}
