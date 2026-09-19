using Microsoft.EntityFrameworkCore;

namespace VantaiViet.MatchingApi.Data;

public sealed class MatchingDbContext(DbContextOptions<MatchingDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MatchingDbContext).Assembly);
    }
}
