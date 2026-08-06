using Microsoft.EntityFrameworkCore;

namespace Afip.Arca.Sdk.Demo.Data;

internal sealed class AfipDemoDbContext : DbContext
{
    public AfipDemoDbContext(DbContextOptions<AfipDemoDbContext> options) : base(options) { }

    public DbSet<TenantAfipConfig> TenantConfigs => Set<TenantAfipConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantAfipConfig>(e =>
        {
            e.ToTable("tenant_afip_configs");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.TenantId).HasMaxLength(100);
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.Cuit).HasMaxLength(11);
        });
    }
}
