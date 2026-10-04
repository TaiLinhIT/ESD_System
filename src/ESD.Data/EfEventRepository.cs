using ESD.Core;
using ESD.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ESD.Data;

/// <summary>
/// EF Core Code First implementation of <see cref="IEventRepository"/>.
/// Each insert gets its own short-lived DbContext (no shared state between
/// background worker calls).
/// </summary>
public sealed class EfEventRepository : IEventRepository
{
    private readonly DbContextOptions<EsdDbContext> _options;

    public EfEventRepository(DbContextOptions<EsdDbContext> options)
        => _options = options;

    public async Task InsertAsync(DbEvent item, CancellationToken ct)
    {
        await using var ctx = new EsdDbContext(_options);

        ctx.EsdEvents.Add(new EsdEventEntity
        {
            EventTime  = item.Timestamp,
            DeviceName = item.Device,
            EmployeeId = string.IsNullOrEmpty(item.EmployeeId) ? null : item.EmployeeId,
            EventType  = item.EventType,
            Status     = item.Status,
            RawData    = string.IsNullOrEmpty(item.RawData)    ? null : item.RawData,
            Message    = string.IsNullOrEmpty(item.Message)    ? null : item.Message,
        });

        await ctx.SaveChangesAsync(ct);
    }
}
