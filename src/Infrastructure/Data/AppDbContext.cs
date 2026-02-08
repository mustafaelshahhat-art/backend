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

        // Global UTC conversion for all DateTime properties
        var dateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
            }
        }

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

        // Soft Delete Configuration & Propagation
        
        // 1. User
        modelBuilder.Entity<User>().Property<bool>("IsDeleted");
        modelBuilder.Entity<User>().HasQueryFilter(u => !EF.Property<bool>(u, "IsDeleted"));

        // 2. Team (Principal: User/Captain)
        modelBuilder.Entity<Team>().Property<bool>("IsDeleted");
        modelBuilder.Entity<Team>().HasQueryFilter(t => 
            !EF.Property<bool>(t, "IsDeleted") && 
            !EF.Property<bool>(t.Captain!, "IsDeleted"));

        // 3. Player (Principal: Team)
        modelBuilder.Entity<Player>().Property<bool>("IsDeleted");
        modelBuilder.Entity<Player>().HasQueryFilter(p => 
            !EF.Property<bool>(p, "IsDeleted") && 
            !EF.Property<bool>(p.Team!, "IsDeleted"));

        // 4. Match (Principals: HomeTeam, AwayTeam)
        // Match does not have its own IsDeleted, but relies on Teams
        modelBuilder.Entity<Match>().HasQueryFilter(m => 
            !EF.Property<bool>(m.HomeTeam!, "IsDeleted") && 
            !EF.Property<bool>(m.AwayTeam!, "IsDeleted"));

        // 5. TeamRegistration (Principal: Team)
        modelBuilder.Entity<TeamRegistration>().HasQueryFilter(tr => 
            !EF.Property<bool>(tr.Team!, "IsDeleted"));

        // 6. Objection (Principal: Team)
        modelBuilder.Entity<Objection>().HasQueryFilter(o => 
            !EF.Property<bool>(o.Team!, "IsDeleted"));

        // 7. TeamJoinRequest (Principals: Team, User)
        modelBuilder.Entity<TeamJoinRequest>().HasQueryFilter(r => 
            !EF.Property<bool>(r.Team!, "IsDeleted") && 
            !EF.Property<bool>(r.User!, "IsDeleted"));

        // 8. Notification (Principal: User)
        modelBuilder.Entity<Notification>().HasQueryFilter(n => 
            !EF.Property<bool>(n.User!, "IsDeleted"));

        // 9. MatchEvent (Principal: Match -> Teams)
        modelBuilder.Entity<MatchEvent>().HasQueryFilter(me => 
            !EF.Property<bool>(me.Match!.HomeTeam!, "IsDeleted") && 
            !EF.Property<bool>(me.Match!.AwayTeam!, "IsDeleted"));

        // 10. MatchMessage (Principal: Match -> Teams)
        modelBuilder.Entity<MatchMessage>().HasQueryFilter(mm => 
            !EF.Property<bool>(mm.Match!.HomeTeam!, "IsDeleted") && 
            !EF.Property<bool>(mm.Match!.AwayTeam!, "IsDeleted"));

        // PERFORMANCE INDEXES
        modelBuilder.Entity<Match>()
            .HasIndex(m => m.Date)
            .HasDatabaseName("IX_Matches_Date");

        modelBuilder.Entity<Match>()
            .HasIndex(m => m.Status)
            .HasDatabaseName("IX_Matches_Status");

        modelBuilder.Entity<Match>()
            .HasIndex(m => new { m.TournamentId, m.Status })
            .HasDatabaseName("IX_Matches_Tournament_Status");
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
