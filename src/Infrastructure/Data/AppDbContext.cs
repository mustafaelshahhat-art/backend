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
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<TeamJoinRequest> TeamJoinRequests { get; set; }
    public DbSet<MatchMessage> MatchMessages { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<Otp> Otps { get; set; }
    public DbSet<TournamentPlayer> TournamentPlayers { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

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

        // Note: Team ownership is now managed via Player entities with TeamRole.Captain

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



        // MatchEvent
        modelBuilder.Entity<MatchEvent>()
            .HasOne(me => me.Match)
            .WithMany(m => m.Events)
            .HasForeignKey(me => me.MatchId)
            .OnDelete(DeleteBehavior.Cascade);



        // MatchMessage
        modelBuilder.Entity<MatchMessage>()
            .HasOne(mm => mm.Match)
            .WithMany()
            .HasForeignKey(mm => mm.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tournament - Creator User
        modelBuilder.Entity<Tournament>()
            .HasOne(t => t.CreatorUser)
            .WithMany()
            .HasForeignKey(t => t.CreatorUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // TournamentPlayer Config
        modelBuilder.Entity<TournamentPlayer>()
            .HasOne(tp => tp.Tournament)
            .WithMany(t => t.TournamentPlayers)
            .HasForeignKey(tp => tp.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TournamentPlayer>()
            .HasOne(tp => tp.Registration)
            .WithMany()
            .HasForeignKey(tp => tp.RegistrationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TournamentPlayer>()
            .HasIndex(tp => new { tp.TournamentId, tp.PlayerId })
            .IsUnique()
            .HasDatabaseName("UQ_TournamentPlayer_Tournament_Player");

        // Match - Tournament
        modelBuilder.Entity<Match>()
            .HasOne(m => m.Tournament)
            .WithMany(t => t.Matches)
            .HasForeignKey(m => m.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Activity - User
        modelBuilder.Entity<Activity>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tournament precision
        modelBuilder.Entity<Tournament>().Property(t => t.EntryFee).HasPrecision(18, 2);

        // Soft Delete Configuration & Propagation
        
        // 1. User - Hard Deleted to allow email reuse

        // 2. Team (Principal: User/Captain)
        modelBuilder.Entity<Team>().Property<bool>("IsDeleted");
        modelBuilder.Entity<Team>().HasQueryFilter(t => 
            !EF.Property<bool>(t, "IsDeleted"));

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



        // 7. TeamJoinRequest (Principal: Team - User is hard-deleted)
        modelBuilder.Entity<TeamJoinRequest>().HasQueryFilter(r =>
            !EF.Property<bool>(r.Team!, "IsDeleted"));

        // 7.5 TournamentPlayer (Principal: Player/Team)
        modelBuilder.Entity<TournamentPlayer>().HasQueryFilter(tp =>
            !EF.Property<bool>(tp.Player!, "IsDeleted") &&
            !EF.Property<bool>(tp.Team!, "IsDeleted"));

        // 8. Notification (Cascade delete when User is hard deleted)
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 9. MatchEvent (Principal: Match -> Teams)
        modelBuilder.Entity<MatchEvent>().HasQueryFilter(me => 
            !EF.Property<bool>(me.Match!.HomeTeam!, "IsDeleted") && 
            !EF.Property<bool>(me.Match!.AwayTeam!, "IsDeleted"));

        // 10. MatchMessage (Principal: Match -> Teams)
        modelBuilder.Entity<MatchMessage>().HasQueryFilter(mm => 
            !EF.Property<bool>(mm.Match!.HomeTeam!, "IsDeleted") && 
            !EF.Property<bool>(mm.Match!.AwayTeam!, "IsDeleted"));

        // 11. OTP
        modelBuilder.Entity<Otp>()
            .HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Otp: User is hard-deleted with cascade, no IsDeleted filter needed

        // PERFORMANCE INDEXES
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("UQ_Users_Email");

        modelBuilder.Entity<User>()
            .HasIndex(u => new { u.Governorate, u.City, u.Neighborhood })
            .HasDatabaseName("IX_Users_Location");

        modelBuilder.Entity<Tournament>()
            .HasIndex(t => new { t.CreatorUserId, t.Status })
            .HasDatabaseName("IX_Tournaments_Creator_Status");

        modelBuilder.Entity<Activity>()
            .HasIndex(a => new { a.UserId, a.CreatedAt })
            .HasDatabaseName("IX_Activities_User_Date");

        modelBuilder.Entity<Activity>()
            .HasIndex(a => a.Type)
            .HasDatabaseName("IX_Activities_Type");

        modelBuilder.Entity<Match>()
            .HasIndex(m => m.Date)
            .HasDatabaseName("IX_Matches_Date");

        modelBuilder.Entity<Match>()
            .HasIndex(m => m.Status)
            .HasDatabaseName("IX_Matches_Status");

        modelBuilder.Entity<Match>()
            .HasIndex(m => new { m.TournamentId, m.Status })
            .HasDatabaseName("IX_Matches_Tournament_Status");

        modelBuilder.Entity<Player>()
            .HasIndex(p => p.UserId)
            .HasDatabaseName("IX_Players_User");

        // UNIQUE CONSTRAINTS
        modelBuilder.Entity<TeamRegistration>()
            .HasIndex(tr => new { tr.TournamentId, tr.TeamId })
            .IsUnique()
            .HasDatabaseName("UQ_TeamRegistration_Tournament_Team");

        modelBuilder.Entity<TeamRegistration>()
            .HasIndex(tr => tr.Status)
            .HasDatabaseName("IX_TeamRegistration_Status");

        modelBuilder.Entity<Match>()
            .HasIndex(m => m.HomeTeamId)
            .HasDatabaseName("IX_Matches_HomeTeamId");

        modelBuilder.Entity<Match>()
            .HasIndex(m => m.AwayTeamId)
            .HasDatabaseName("IX_Matches_AwayTeamId");

        modelBuilder.Entity<Player>()
            .HasIndex(p => new { p.TeamId, p.UserId })
            .IsUnique()
            .HasDatabaseName("UQ_Player_Team_User");
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
        
        // Handle Soft Delete for Team, Player (Users are now Hard Deleted to allow email reuse)
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted && 
               (entry.Entity is Team || entry.Entity is Player))
            {
                entry.State = EntityState.Modified;
                entry.Property("IsDeleted").CurrentValue = true;
            }
        }

        // Capture Domain Events
        var domainEvents = ChangeTracker.Entries<BaseEntity>()
            .Select(e => e.Entity)
            .SelectMany(e => 
            {
                var events = e.DomainEvents.ToList();
                e.ClearDomainEvents();
                return events;
            })
            .ToList();

        if (domainEvents.Any())
        {
            var outboxMessages = domainEvents.Select(domainEvent => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OccurredOn = domainEvent.OccurredOn,
                Type = domainEvent.GetType().Name,
                Content = System.Text.Json.JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                Status = OutboxMessageStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            this.OutboxMessages.AddRange(outboxMessages);
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
