using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DenialsCommandCenter.Api.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DenialsDbContext>
{
    public DenialsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<DenialsDbContext>().UseNpgsql("Host=localhost;Database=denials;Username=denials;Password=unused").Options);
}
