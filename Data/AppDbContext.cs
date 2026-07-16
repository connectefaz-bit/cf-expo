using Microsoft.EntityFrameworkCore;

namespace CfMvc.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ContestEntity> Contests { get; set; }
    public DbSet<ProblemEntity> Problems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContestEntity>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.StartTimeSeconds);
            entity.Property(c => c.Name).HasMaxLength(512);
            entity.Property(c => c.Type).HasMaxLength(64);
            entity.Property(c => c.Phase).HasMaxLength(64);
        });

        modelBuilder.Entity<ProblemEntity>(entity =>
        {
            // Composite PK: (ContestId, Index) — matches the CF problem identity.
            entity.HasKey(p => new { p.ContestId, p.Index });
            entity.HasIndex(p => p.ContestId);
            entity.HasIndex(p => new { p.ContestId, p.Position });
            entity.Property(p => p.Index).HasMaxLength(16);
            entity.Property(p => p.Name).HasMaxLength(512);
            entity.Property(p => p.TagsJson).HasDefaultValue("[]");

            entity.HasOne(p => p.Contest)
                  .WithMany(c => c.Problems)
                  .HasForeignKey(p => p.ContestId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
