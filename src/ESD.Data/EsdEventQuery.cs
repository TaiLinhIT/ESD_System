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
}
