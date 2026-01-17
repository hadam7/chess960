using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Chess960.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<GameHistory> GameHistories { get; set; }
    public DbSet<Friendship> Friendships { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Friendship>()
            .HasOne(f => f.Requester)
            .WithMany()
            .HasForeignKey(f => f.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Friendship>()
            .HasOne(f => f.Receiver)
            .WithMany()
            .HasForeignKey(f => f.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
