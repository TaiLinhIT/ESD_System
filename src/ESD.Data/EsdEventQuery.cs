using ESD.Core;
using ESD.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ESD.Data;

/// <summary>
/// Read-side queries for the Dashboard, History, Report screens.
/// </summary>
public sealed class EsdEventQuery
{
    private readonly DbContextOptions<EsdDbContext> _options;

    public EsdEventQuery(DbContextOptions<EsdDbContext> options)
        => _options = options;

    /// <summary>Most recent N events across all devices.</summary>
    public async Task<List<EsdEventEntity>> GetRecentAsync(int count = 200, CancellationToken ct = default)
    {
        await using var ctx = new EsdDbContext(_options);
        return await ctx.EsdEvents
            .OrderByDescending(e => e.EventTime)
            .Take(count)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <summary>Events for a specific device in a date range.</summary>
    public async Task<List<EsdEventEntity>> GetByDeviceAsync(
        string deviceName, DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var ctx = new EsdDbContext(_options);
        return await ctx.EsdEvents
            .Where(e => e.DeviceName == deviceName
                     && e.EventTime >= from
                     && e.EventTime <= to)
            .OrderByDescending(e => e.EventTime)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <summary>Compliance summary: OK count vs total for today.</summary>
    public async Task<(int Total, int Ok)> GetTodayComplianceAsync(CancellationToken ct = default)
    {
        await using var ctx = new EsdDbContext(_options);
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var total = await ctx.EsdEvents
            .Where(e => e.EventTime >= today && e.EventTime < tomorrow)
            .CountAsync(ct);

        var ok = await ctx.EsdEvents
            .Where(e => e.EventTime >= today && e.EventTime < tomorrow
                     && e.Status == "Ok")
            .CountAsync(ct);

        return (total, ok);
    }

    /// <summary>
    /// Filtered, paged search. All filters are optional;
    /// null/empty means "no filter" for that field.
    /// </summary>
    public async Task<PagedResult<EsdEventEntity>> SearchAsync(
        EventQueryFilter filter,
        CancellationToken ct = default)
    {
        await using var ctx = new EsdDbContext(_options);

        var query = ctx.EsdEvents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.DeviceName))
            query = query.Where(e => e.DeviceName == filter.DeviceName);

        if (!string.IsNullOrWhiteSpace(filter.EmployeeId))
            query = query.Where(e => e.EmployeeId == filter.EmployeeId);

        if (!string.IsNullOrWhiteSpace(filter.EventType))
            query = query.Where(e => e.EventType == filter.EventType);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(e => e.Status == filter.Status);

        if (filter.From is not null)
            query = query.Where(e => e.EventTime >= filter.From.Value);

        if (filter.To is not null)
            query = query.Where(e => e.EventTime <= filter.To.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.EventTime)
            .ThenByDescending(e => e.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return PagedResult<EsdEventEntity>.Of(items, totalCount, filter.Page, filter.PageSize);
    }

    /// <summary>Hourly OK/NG breakdown for a date — powers dashboard trend charts.</summary>
    public async Task<List<HourlyTrendPoint>> GetHourlyTrendAsync(
        DateTime? day = null, CancellationToken ct = default)
    {
        await using var ctx = new EsdDbContext(_options);
        var d = day?.Date ?? DateTime.Today;
        var end = d.AddDays(1);

        // EF Core cannot translate positional-record construction
        // inside a GroupBy projection — aggregate to an anonymous
        // type in SQL, then map to the record client-side.
        var rows = await ctx.EsdEvents
            .Where(e => e.EventTime >= d && e.EventTime < end)
            .GroupBy(e => e.EventTime.Hour)
            .Select(g => new
            {
                Hour   = g.Key,
                Total  = g.Count(),
                Ok     = g.Count(e => e.Status == "Ok"),
                Ng     = g.Count(e => e.Status == "Ng"),
            })
            .OrderBy(p => p.Hour)
            .ToListAsync(ct);

        return rows.Select(r => new HourlyTrendPoint(
            r.Hour, r.Total, r.Ok, r.Ng)).ToList();
    }

    /// <summary>Per-device OK/NG/Warning breakdown for a date — station status cards.</summary>
    public async Task<List<StationBreakdownPoint>> GetStationBreakdownAsync(
        DateTime? day = null, CancellationToken ct = default)
    {
        await using var ctx = new EsdDbContext(_options);
        var d = day?.Date ?? DateTime.Today;
        var end = d.AddDays(1);

        var rows = await ctx.EsdEvents
            .Where(e => e.EventTime >= d && e.EventTime < end)
            .GroupBy(e => e.DeviceName)
            .Select(g => new
            {
                DeviceName = g.Key,
                Total      = g.Count(),
                Ok         = g.Count(e => e.Status == "Ok"),
                Ng         = g.Count(e => e.Status == "Ng"),
                Warning    = g.Count(e => e.Status == "Warning"),
            })
            .OrderBy(p => p.DeviceName)
            .ToListAsync(ct);

        return rows.Select(r => new StationBreakdownPoint(
            r.DeviceName, r.Total, r.Ok, r.Ng, r.Warning)).ToList();
    }

    /// <summary>Top operators by event count for a date.</summary>
    public async Task<List<EmployeeActivityPoint>> GetTopEmployeesAsync(
        DateTime? day = null, int count = 10, CancellationToken ct = default)
    {
        await using var ctx = new EsdDbContext(_options);
        var d = day?.Date ?? DateTime.Today;
        var end = d.AddDays(1);

        var rows = await ctx.EsdEvents
            .Where(e => e.EventTime >= d && e.EventTime < end
                     && e.EmployeeId != null)
            .GroupBy(e => e.EmployeeId)
            .Select(g => new
            {
                EmployeeId = g.Key!,
                Total      = g.Count(),
                Ok         = g.Count(e => e.Status == "Ok"),
                Ng         = g.Count(e => e.Status == "Ng"),
                LastEvent  = g.Max(e => e.EventTime),
            })
            .OrderByDescending(p => p.Total)
            .Take(count)
            .ToListAsync(ct);

        return rows.Select(r => new EmployeeActivityPoint(
            r.EmployeeId, r.Total, r.Ok, r.Ng, r.LastEvent)).ToList();
    }

    /// <summary>
    /// Full compliance summary for a date:
    /// totals, OK/NG/Warning counts and alarm count.
    /// </summary>
    public async Task<DaySummary> GetTodaySummaryAsync(
        DateTime? day = null, CancellationToken ct = default)
    {
        await using var ctx = new EsdDbContext(_options);
        var d = day?.Date ?? DateTime.Today;
        var end = d.AddDays(1);

        var events = ctx.EsdEvents.Where(e => e.EventTime >= d && e.EventTime < end);

        var total   = await events.CountAsync(ct);
        var ok      = await events.CountAsync(e => e.Status == "Ok", ct);
        var ng      = await events.CountAsync(e => e.Status == "Ng", ct);
        var warning = await events.CountAsync(e => e.Status == "Warning", ct);
        var alarms  = await events.CountAsync(e => e.EventType == "Alarm", ct);
        var active  = await events.Select(e => e.DeviceName)
                                  .Distinct()
                                  .CountAsync(ct);

        return new DaySummary(d, total, ok, ng, warning, alarms, active);
    }

    /// <summary>
    /// Distinct values for UI filter dropdowns
    /// (devices, employees, event types, statuses).
    /// </summary>
    public async Task<EventFilterValues> GetFilterValuesAsync(CancellationToken ct = default)
    {
        await using var ctx = new EsdDbContext(_options);

        var devices = await ctx.EsdEvents
            .Select(e => e.DeviceName).Distinct().OrderBy(x => x).ToListAsync(ct);

        var employees = await ctx.EsdEvents
            .Where(e => e.EmployeeId != null)
            .Select(e => e.EmployeeId!).Distinct().OrderBy(x => x).ToListAsync(ct);

        var types = await ctx.EsdEvents
            .Select(e => e.EventType).Distinct().OrderBy(x => x).ToListAsync(ct);

        var statuses = await ctx.EsdEvents
            .Select(e => e.Status).Distinct().OrderBy(x => x).ToListAsync(ct);

        return new EventFilterValues(devices, employees, types, statuses);
    }
}

/// <summary>Filter criteria for <see cref="EsdEventQuery.SearchAsync"/>.</summary>
public sealed record EventQueryFilter
{
    public string? DeviceName  { get; init; }
    public string? EmployeeId  { get; init; }
    public string? EventType   { get; init; }
    public string? Status      { get; init; }
    public DateTime? From      { get; init; }
    public DateTime? To        { get; init; }
    public int Page            { get; init; } = 1;
    public int PageSize        { get; init; } = 50;
}

/// <summary>Aggregated OK/NG counts per hour.</summary>
public sealed record HourlyTrendPoint(int Hour, int Total, int Ok, int Ng);

/// <summary>Compliance totals for one day.</summary>
public sealed record DaySummary(
    DateTime Date, int Total, int Ok, int Ng, int Warning, int Alarms, int ActiveStations);

/// <summary>Aggregated OK/NG/Warning counts per station (device).</summary>
public sealed record StationBreakdownPoint(string DeviceName, int Total, int Ok, int Ng, int Warning);

/// <summary>Aggregated activity per operator.</summary>
public sealed record EmployeeActivityPoint(string EmployeeId, int Total, int Ok, int Ng, DateTime LastEvent);

/// <summary>Distinct filter values for search screens.</summary>
public sealed record EventFilterValues(
    IReadOnlyList<string> Devices,
    IReadOnlyList<string> Employees,
    IReadOnlyList<string> EventTypes,
    IReadOnlyList<string> Statuses);
