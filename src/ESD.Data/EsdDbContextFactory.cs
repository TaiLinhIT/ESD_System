using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ESD.Data;

/// <summary>
/// Used by EF Core Tools (dotnet ef migrations add / database update)
/// at design time. Not used at runtime.
/// </summary>
public sealed class EsdDbContextFactory : IDesignTimeDbContextFactory<EsdDbContext>
{
    public EsdDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EsdDbContext>()
            .UseSqlServer(
                @"Server=ADMIN-PC\ANTHONYNGUYEN;Database=EsdSystem;Trusted_Connection=True;TrustServerCertificate=True;",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory"))
            .Options;

        return new EsdDbContext(options);
    }
}
