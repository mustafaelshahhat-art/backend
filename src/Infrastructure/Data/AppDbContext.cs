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
    public DbSet<IdempotentRequest> IdempotentRequests { get; set; }
    public DbSet<TeamStats> TeamStats { get; set; }

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

        // Team - TeamStats (1-to-1)
        modelBuilder.Entity<Team>()
            .HasOne(t => t.Statistics)
            .WithOne(s => s.Team)
            .HasForeignKey<TeamStats>(s => s.TeamId);

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
        
        // Explicitly Ignore obsolete properties (attributes might fail if DLLs are out of sync)
        modelBuilder.Entity<Tournament>().Ignore(t => t.OpeningMatchHomeTeamId);
        modelBuilder.Entity<Tournament>().Ignore(t => t.OpeningMatchAwayTeamId);
        // PROD-FIX: Disable Output Clause for SQL Server triggers compatibility
        modelBuilder.Entity<Tournament>().ToTable(tb => tb.UseSqlOutputClause(false));

        // Soft Delete Configuration & Propagation
        // PERF-FIX: All entities get their OWN IsDeleted shadow property.
        // This eliminates forced JOINs from navigation-property-based query filters.
        // The IsDeleted flag is cascaded in SaveChangesAsync when a Team is soft-deleted.
        
        // 1. User - Hard Deleted to allow email reuse

        // 2. Team (Principal: User/Captain)
        modelBuilder.Entity<Team>().Property<bool>("IsDeleted");
        modelBuilder.Entity<Team>().HasQueryFilter(t => 
            !EF.Property<bool>(t, "IsDeleted"));

        // 3. Player (Principal: Team) — own IsDeleted, no JOIN to Team
        modelBuilder.Entity<Player>().Property<bool>("IsDeleted");
        modelBuilder.Entity<Player>().HasQueryFilter(p => 
            !EF.Property<bool>(p, "IsDeleted"));

        // 4. Match — own IsDeleted shadow property, no JOINs to HomeTeam/AwayTeam
        modelBuilder.Entity<Match>().Property<bool>("IsDeleted").HasDefaultValue(false);
        modelBuilder.Entity<Match>().HasQueryFilter(m => 
            !EF.Property<bool>(m, "IsDeleted"));

        // 5. TeamRegistration — own IsDeleted, no JOIN to Team
        modelBuilder.Entity<TeamRegistration>().Property<bool>("IsDeleted").HasDefaultValue(false);
        modelBuilder.Entity<TeamRegistration>().HasQueryFilter(tr => 
            !EF.Property<bool>(tr, "IsDeleted"));

        // 7. TeamJoinRequest — own IsDeleted, no JOIN to Team
        modelBuilder.Entity<TeamJoinRequest>().Property<bool>("IsDeleted").HasDefaultValue(false);
        modelBuilder.Entity<TeamJoinRequest>().HasQueryFilter(r =>
            !EF.Property<bool>(r, "IsDeleted"));

        // 7.5 TournamentPlayer — own IsDeleted, no JOINs to Player/Team
        modelBuilder.Entity<TournamentPlayer>().Property<bool>("IsDeleted").HasDefaultValue(false);
        modelBuilder.Entity<TournamentPlayer>().HasQueryFilter(tp =>
            !EF.Property<bool>(tp, "IsDeleted"));

        // 8. Notification (Cascade delete when User is hard deleted)
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 9. MatchEvent — own IsDeleted, no JOINs to Match→Teams
        modelBuilder.Entity<MatchEvent>().Property<bool>("IsDeleted").HasDefaultValue(false);
        modelBuilder.Entity<MatchEvent>().HasQueryFilter(me => 
            !EF.Property<bool>(me, "IsDeleted"));

        // 10. MatchMessage — own IsDeleted, no JOINs to Match→Teams
        modelBuilder.Entity<MatchMessage>().Property<bool>("IsDeleted").HasDefaultValue(false);
        modelBuilder.Entity<MatchMessage>().HasQueryFilter(mm => 
            !EF.Property<bool>(mm, "IsDeleted"));

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
            .HasIndex(t => t.Name)
            .IsUnique()
            .HasDatabaseName("UQ_Tournaments_Name");

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

        modelBuilder.Entity<IdempotentRequest>()
            .HasIndex(r => new { r.Key, r.Route })
            .IsUnique()
            .HasDatabaseName("UQ_IdempotentRequests_Key_Route");

        modelBuilder.Entity<OutboxMessage>()
            .HasIndex(m => new { m.Status, m.ScheduledAt })
            .HasDatabaseName("IX_OutboxMessages_Status_ScheduledAt");

        modelBuilder.Entity<TeamStats>()
            .HasIndex(s => s.TeamId)
            .IsUnique()
            .HasDatabaseName("IX_TeamStats_TeamId");

        // SCALE PROTECTION INDEXES
        modelBuilder.Entity<Team>()
            .HasIndex(t => t.IsActive)
            .HasDatabaseName("IX_Teams_IsActive");

        modelBuilder.Entity<Team>()
            .HasIndex(t => t.City)
            .HasDatabaseName("IX_Teams_City");

        modelBuilder.Entity<Player>()
            .HasIndex(p => p.Status)
            .HasDatabaseName("IX_Players_Status");

        modelBuilder.Entity<Player>()
            .HasIndex(p => p.Position)
            .HasDatabaseName("IX_Players_Position");

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead })
            .HasDatabaseName("IX_Notifications_User_Read");

        modelBuilder.Entity<Tournament>()
            .HasIndex(t => t.Status)
            .HasDatabaseName("IX_Tournaments_Status");

        modelBuilder.Entity<Match>()
            .HasIndex(m => new { m.TournamentId, m.GroupId })
            .HasDatabaseName("IX_Matches_Tournament_Group");

        modelBuilder.Entity<Match>()
            .HasIndex(m => new { m.TournamentId, m.RoundNumber })
            .HasDatabaseName("IX_Matches_Tournament_Round");
        modelBuilder.Entity<MatchEvent>()
            .HasIndex(me => me.MatchId)
            .HasDatabaseName("IX_MatchEvents_MatchId");

        modelBuilder.Entity<MatchMessage>()
            .HasIndex(mm => mm.MatchId)
            .HasDatabaseName("IX_MatchMessages_MatchId");

        modelBuilder.Entity<TeamJoinRequest>()
            .HasIndex(r => new { r.TeamId, r.Status })
            .HasDatabaseName("IX_TeamJoinRequests_Team_Status");

        modelBuilder.Entity<Tournament>()
            .HasIndex(t => t.StartDate)
            .HasDatabaseName("IX_Tournaments_StartDate");

        modelBuilder.Entity<Tournament>()
            .HasIndex(t => t.CreatedAt)
            .HasDatabaseName("IX_Tournaments_CreatedAt");

        modelBuilder.Entity<TeamRegistration>()
            .HasIndex(tr => tr.CreatedAt)
            .HasDatabaseName("IX_TeamRegistration_CreatedAt");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.CreatedAt)
            .HasDatabaseName("IX_Users_CreatedAt");

        modelBuilder.Entity<TeamJoinRequest>()
            .HasIndex(r => r.UserId)
            .HasDatabaseName("IX_TeamJoinRequests_UserId");

        // PHASE 2: MISSING PERFORMANCE INDEXES (D4+D5)
        // Notification: user inbox ordered by date (covers GetByUserIdAsync hot path)
        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Notifications_User_CreatedAt");

        // MatchMessage: chat history ordered by timestamp
        modelBuilder.Entity<MatchMessage>()
            .HasIndex(mm => new { mm.MatchId, mm.Timestamp })
            .HasDatabaseName("IX_MatchMessages_Match_Timestamp");

        // Otp: lookup by userId + type + active status
        modelBuilder.Entity<Otp>()
            .HasIndex(o => new { o.UserId, o.Type, o.IsUsed })
            .HasDatabaseName("IX_Otps_User_Type_IsUsed");

        // User: refresh token lookup (filtered — only non-null)
        modelBuilder.Entity<User>()
            .HasIndex(u => u.RefreshToken)
            .HasFilter("[RefreshToken] IS NOT NULL")
            .HasDatabaseName("IX_Users_RefreshToken_Filtered");

        // TeamRegistration: tournament + status (covers registration approval queries)
        modelBuilder.Entity<TeamRegistration>()
            .HasIndex(tr => new { tr.TournamentId, tr.Status })
            .HasDatabaseName("IX_TeamRegistration_Tournament_Status");

        // TournamentPlayer: by registrationId (FK lookup)
        modelBuilder.Entity<TournamentPlayer>()
            .HasIndex(tp => tp.RegistrationId)
            .HasDatabaseName("IX_TournamentPlayers_RegistrationId");

        // Match: covering index for listing (tournament + status + date)
        modelBuilder.Entity<Match>()
            .HasIndex(m => new { m.TournamentId, m.Status, m.Date })
            .HasDatabaseName("IX_Matches_Tournament_Status_Date");

        // TeamJoinRequests: user + status (covers "my pending requests")
        modelBuilder.Entity<TeamJoinRequest>()
            .HasIndex(r => new { r.UserId, r.Status })
            .HasDatabaseName("IX_TeamJoinRequests_User_Status");
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
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
        // PERF-FIX: Cascade IsDeleted to dependent entities so query filters don't need JOINs
        var teamsBeingDeleted = new List<Guid>();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted && 
               (entry.Entity is Team || entry.Entity is Player))
            {
                entry.State = EntityState.Modified;
                entry.Property("IsDeleted").CurrentValue = true;
                
                if (entry.Entity is Team team)
                {
                    teamsBeingDeleted.Add(team.Id);
                }
            }
        }
        
        // Cascade IsDeleted to all entities that depend on soft-deleted teams
        if (teamsBeingDeleted.Count > 0)
        {
            // Mark Players as deleted — use IgnoreQueryFilters to find all regardless of current filter state
            var players = await Players.IgnoreQueryFilters().Where(p => teamsBeingDeleted.Contains(p.TeamId) && !EF.Property<bool>(p, "IsDeleted")).ToListAsync(cancellationToken);
            foreach (var p in players) Entry(p).Property("IsDeleted").CurrentValue = true;
            
            // Mark Matches as deleted (home or away team deleted)
            var matches = await Matches.IgnoreQueryFilters()
                .Where(m => (teamsBeingDeleted.Contains(m.HomeTeamId) || teamsBeingDeleted.Contains(m.AwayTeamId)) && !EF.Property<bool>(m, "IsDeleted"))
                .ToListAsync(cancellationToken);
            foreach (var m in matches) Entry(m).Property("IsDeleted").CurrentValue = true;
            
            var matchIds = matches.Select(m => m.Id).ToList();
            if (matchIds.Count > 0)
            {
                // Mark MatchEvents as deleted
                var events = await MatchEvents.IgnoreQueryFilters().Where(e => matchIds.Contains(e.MatchId) && !EF.Property<bool>(e, "IsDeleted")).ToListAsync(cancellationToken);
                foreach (var e in events) Entry(e).Property("IsDeleted").CurrentValue = true;
                
                // Mark MatchMessages as deleted
                var messages = await MatchMessages.IgnoreQueryFilters().Where(msg => matchIds.Contains(msg.MatchId) && !EF.Property<bool>(msg, "IsDeleted")).ToListAsync(cancellationToken);
                foreach (var msg in messages) Entry(msg).Property("IsDeleted").CurrentValue = true;
            }
            
            // Mark TeamRegistrations as deleted
            var regs = await TeamRegistrations.IgnoreQueryFilters().Where(r => teamsBeingDeleted.Contains(r.TeamId) && !EF.Property<bool>(r, "IsDeleted")).ToListAsync(cancellationToken);
            foreach (var r in regs) Entry(r).Property("IsDeleted").CurrentValue = true;
            
            // Mark TeamJoinRequests as deleted
            var joinRequests = await TeamJoinRequests.IgnoreQueryFilters().Where(r => teamsBeingDeleted.Contains(r.TeamId) && !EF.Property<bool>(r, "IsDeleted")).ToListAsync(cancellationToken);
            foreach (var r in joinRequests) Entry(r).Property("IsDeleted").CurrentValue = true;
            
            // Mark TournamentPlayers as deleted (via player's team)
            var playerIds = players.Select(p => p.Id).ToList();
            if (playerIds.Count > 0)
            {
                var tournamentPlayers = await TournamentPlayers.IgnoreQueryFilters().Where(tp => playerIds.Contains(tp.PlayerId) && !EF.Property<bool>(tp, "IsDeleted")).ToListAsync(cancellationToken);
                foreach (var tp in tournamentPlayers) Entry(tp).Property("IsDeleted").CurrentValue = true;
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
                Payload = System.Text.Json.JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                Status = OutboxMessageStatus.Pending,
                ScheduledAt = DateTime.UtcNow,
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            this.OutboxMessages.AddRange(outboxMessages);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
