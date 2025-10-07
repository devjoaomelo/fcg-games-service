using FCG.Games.Domain.Entities;
using FCG.Games.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FCG.Games.Infra.Data;

public sealed class GamesDbContext : DbContext
{
    public GamesDbContext(DbContextOptions<GamesDbContext> options) : base(options) { }

    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var titleConv = new ValueConverter<GameTitle, string>(
            v => v.Value,
            v => GameTitle.Create(v));

        var descConv = new ValueConverter<Description, string>(
            v => v.Value,
            v => Description.Create(v));

        var priceConv = new ValueConverter<Price, decimal>(
            v => v.Value,
            v => Price.Parse(v));

        var titleComp = new ValueComparer<GameTitle>(
            (a, b) => a.Value == b.Value,
            v => v.Value.GetHashCode(),
            v => GameTitle.Create(v.Value));

        var descComp = new ValueComparer<Description>(
            (a, b) => a.Value == b.Value,
            v => v.Value.GetHashCode(),
            v => Description.Create(v.Value));

        var priceComp = new ValueComparer<Price>(
            (a, b) => a.Value == b.Value,
            v => v.Value.GetHashCode(),
            v => Price.Parse(v.Value));


        modelBuilder.Entity<Game>(e =>
        {
            e.ToTable("games");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
             .HasColumnName("id")
             .ValueGeneratedNever();

            var titleProp = e.Property(x => x.Title)
                .HasColumnName("title")
                .HasConversion(titleConv)
                .IsRequired()
                .HasMaxLength(200);
            titleProp.Metadata.SetValueComparer(titleComp);

            var descProp = e.Property(x => x.Description)
                .HasColumnName("description")
                .HasConversion(descConv)
                .IsRequired()
                .HasMaxLength(1000);
            descProp.Metadata.SetValueComparer(descComp);

            var priceProp = e.Property(x => x.Price)
                .HasColumnName("price")
                .HasColumnType("decimal(10,2)")
                .HasConversion(priceConv);
            priceProp.Metadata.SetValueComparer(priceComp);

            e.HasIndex(x => x.Title);
        });
    }
}
