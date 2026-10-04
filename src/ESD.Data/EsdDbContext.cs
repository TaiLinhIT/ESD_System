using ESD.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ESD.Data;

public sealed class EsdDbContext : DbContext
{
    public EsdDbContext(DbContextOptions<EsdDbContext> options) : base(options) { }

    public DbSet<EsdEventEntity> EsdEvents => Set<EsdEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EsdEventEntity>(e =>
        {
            e.ToTable("EsdEvents");

            e.HasKey(x => x.Id);

            e.Property(x => x.EventTime)
             .IsRequired()
             .HasColumnType("datetime2");

            e.Property(x => x.DeviceName)
             .IsRequired()
             .HasMaxLength(100);

            e.Property(x => x.EmployeeId)
             .HasMaxLength(100);

            e.Property(x => x.EventType)
             .IsRequired()
             .HasMaxLength(60);

            e.Property(x => x.Status)
             .IsRequired()
             .HasMaxLength(40);

            e.Property(x => x.RawData)
             .HasMaxLength(500);

            e.Property(x => x.Message)
             .HasMaxLength(500);

            // Indexes for common query patterns
            e.HasIndex(x => x.EventTime)
             .HasDatabaseName("IX_EsdEvents_EventTime");

            e.HasIndex(x => x.EmployeeId)
             .HasDatabaseName("IX_EsdEvents_EmployeeId");

            e.HasIndex(x => new { x.DeviceName, x.EventType })
             .HasDatabaseName("IX_EsdEvents_Device_EventType");
        });
    }
}
