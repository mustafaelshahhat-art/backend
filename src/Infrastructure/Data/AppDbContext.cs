using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<Player> Players { get; set; }
    public DbSet<Tournament> Tournaments { get; set; }
    public DbSet<TeamRegistration> TeamRegistrations { get; set; }
    public DbSet<Match> Matches { get; set; }
    public DbSet<MatchEvent> MatchEvents { get; set; }
    public DbSet<Objection> Objections { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<TeamJoinRequest> TeamJoinRequests { get; set; }
    public DbSet<MatchMessage> MatchMessages { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User - Team (Captain relationship)
        modelBuilder.Entity<Team>()
            .HasOne(t => t.Captain)
            .WithMany()
            .HasForeignKey(t => t.CaptainId)
            .OnDelete(DeleteBehavior.Restrict);

        // Player - Team
        modelBuilder.Entity<Player>()
            .HasOne(p => p.Team)
            .WithMany(t => t.Players)
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Player - User (Optional)
        modelBuilder.Entity<Player>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Tournament - Registrations
        modelBuilder.Entity<TeamRegistration>()
            .HasOne(tr => tr.Tournament)
            .WithMany(t => t.Registrations)
            .HasForeignKey(tr => tr.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // TeamRegistration - Team
        modelBuilder.Entity<TeamRegistration>()
            .HasOne(tr => tr.Team)
            .WithMany(t => t.Registrations)
            .HasForeignKey(tr => tr.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Match - Teams
        modelBuilder.Entity<Match>()
            .HasOne(m => m.HomeTeam)
            .WithMany()
            .HasForeignKey(m => m.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Match>()
            .HasOne(m => m.AwayTeam)
            .WithMany()
            .HasForeignKey(m => m.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Match - Referee
        modelBuilder.Entity<Match>()
            .HasOne(m => m.Referee)
            .WithMany()
            .HasForeignKey(m => m.RefereeId)
            .OnDelete(DeleteBehavior.Restrict);

        // MatchEvent
        modelBuilder.Entity<MatchEvent>()
            .HasOne(me => me.Match)
            .WithMany(m => m.Events)
            .HasForeignKey(me => me.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Objection
        modelBuilder.Entity<Objection>()
            .HasOne(o => o.Match)
            .WithMany(m => m.Objections)
            .HasForeignKey(o => o.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // MatchMessage
        modelBuilder.Entity<MatchMessage>()
            .HasOne(mm => mm.Match)
            .WithMany()
            .HasForeignKey(mm => mm.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tournament precision
        modelBuilder.Entity<Tournament>().Property(t => t.EntryFee).HasPrecision(18, 2);

        // Soft Delete filter for User
        modelBuilder.Entity<User>().HasQueryFilter(u => u.Status != UserStatus.Suspended); // Or use a separate IsDeleted flag?
        // Requirement says "Use soft delete for Users". Usually implies an IsDeleted flag.
        // But status "Suspended" might be different. 
        // I'll add IsDeleted property to User entity via shadow property or explicit if needed.
        // Requirement: "Use soft delete for Users".
        // I will stick to Status for logic, but for "Soft Delete" usually it means physically keeping the record but filtering it out.
        // I'll add IsDeleted to BaseEntity or User? BaseEntity is common.
        // Let's add IsDeleted to BaseEntity to be safe/standard
        // Re-reading entities: BaseEntity has Id, CreatedAt, UpdatedAt.
        // I'll add IsDeleted shadow property to User instead. Or explicit field.
        // I'll assume UserStatus.Suspended IS the soft delete equivalent or I should separate it.
        // "Delete user (or deactivate)" in contract implies soft delete.
        // I'll use a shadow property "IsDeleted" for User.
        modelBuilder.Entity<User>().Property<bool>("IsDeleted");
        modelBuilder.Entity<User>().HasQueryFilter(u => !EF.Property<bool>(u, "IsDeleted"));

        // Soft Delete for Team
        modelBuilder.Entity<Team>().Property<bool>("IsDeleted");
        modelBuilder.Entity<Team>().HasQueryFilter(t => !EF.Property<bool>(t, "IsDeleted"));

        // Soft Delete for Player
        modelBuilder.Entity<Player>().Property<bool>("IsDeleted");
        modelBuilder.Entity<Player>().HasQueryFilter(p => !EF.Property<bool>(p, "IsDeleted"));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        
        // Handle Soft Delete for User, Team, Player
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted && 
               (entry.Entity is User || entry.Entity is Team || entry.Entity is Player))
            {
                entry.State = EntityState.Modified;
                entry.Property("IsDeleted").CurrentValue = true;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
