using Microsoft.EntityFrameworkCore;
using Scribble.Repository.Data.Entities;

namespace Scribble.Repository.DbContext;

public class ScribbleDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public ScribbleDbContext(DbContextOptions<ScribbleDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<GameScore> GameScores { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<LeaderboardEntry> LeaderboardEntries { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Friendship> Friendships { get; set; }
    public DbSet<RoomInvitation> RoomInvitations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConnectionId);
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.Room)
                  .WithMany(r => r.Players)
                  .HasForeignKey(e => e.RoomId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RoomCode).IsUnique();
            entity.HasIndex(e => e.RoomType);
        });

        modelBuilder.Entity<GameScore>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Player)
                  .WithMany()
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Room)
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Player)
                  .WithMany()
                  .HasForeignKey(e => e.PlayerId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Room)
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeaderboardEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.TotalScore);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MobileNumber).IsUnique();
            entity.HasIndex(e => e.IsOnline);
        });

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RequesterId, e.AddresseeId }).IsUnique();
            entity.HasOne(e => e.Requester)
                  .WithMany()
                  .HasForeignKey(e => e.RequesterId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Addressee)
                  .WithMany()
                  .HasForeignKey(e => e.AddresseeId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoomInvitation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RoomId, e.InviteeId, e.Status });
            entity.HasOne(e => e.Room)
                  .WithMany()
                  .HasForeignKey(e => e.RoomId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Inviter)
                  .WithMany()
                  .HasForeignKey(e => e.InviterId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Invitee)
                  .WithMany()
                  .HasForeignKey(e => e.InviteeId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
