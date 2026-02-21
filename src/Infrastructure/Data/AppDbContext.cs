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
        // ENTERPRISE: Default to NoTracking for all read queries.
        // Command handlers that need tracking must use explicit .AsTracking() or rely on
        // GetByIdAsync (which uses FindAsync — always tracked).
        // NOTE: With AddDbContextPool, this runs once per pooled instance.
        // QueryTrackingBehavior persists across pool checkouts (EF doesn't reset it).
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
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
    public DbSet<Governorate> Governorates { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<Area> Areas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BaseEntity conventions — cross-cutting, kept here (replaces [Key] and [Timestamp] annotations)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).HasKey("Id");
                modelBuilder.Entity(entityType.ClrType).Property("RowVersion").IsRowVersion();
            }
        }

        // Global UTC conversion for all DateTime properties — cross-cutting, kept here
        var dateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableDateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }

        // Apply all IEntityTypeConfiguration<T> classes from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
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
        // PERF-FIX: Use ExecuteUpdateAsync for bulk cascades — single SQL per entity type,
        // no entity materialization into memory (was 7+ SELECT queries loading all dependents).
        if (teamsBeingDeleted.Count > 0)
        {
            // Cascade Players
            var affectedPlayerIds = await Players.IgnoreQueryFilters()
                .Where(p => teamsBeingDeleted.Contains(p.TeamId) && !EF.Property<bool>(p, "IsDeleted"))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            if (affectedPlayerIds.Count > 0)
            {
                await Players.IgnoreQueryFilters()
                    .Where(p => affectedPlayerIds.Contains(p.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(p => EF.Property<bool>(p, "IsDeleted"), true), cancellationToken);
            }

            // Cascade Matches + their dependents
            var affectedMatchIds = await Matches.IgnoreQueryFilters()
                .Where(m => (teamsBeingDeleted.Contains(m.HomeTeamId) || teamsBeingDeleted.Contains(m.AwayTeamId)) && !EF.Property<bool>(m, "IsDeleted"))
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            if (affectedMatchIds.Count > 0)
            {
                await Matches.IgnoreQueryFilters()
                    .Where(m => affectedMatchIds.Contains(m.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(m => EF.Property<bool>(m, "IsDeleted"), true), cancellationToken);

                await MatchEvents.IgnoreQueryFilters()
                    .Where(e => affectedMatchIds.Contains(e.MatchId) && !EF.Property<bool>(e, "IsDeleted"))
                    .ExecuteUpdateAsync(s => s.SetProperty(e => EF.Property<bool>(e, "IsDeleted"), true), cancellationToken);

                await MatchMessages.IgnoreQueryFilters()
                    .Where(msg => affectedMatchIds.Contains(msg.MatchId) && !EF.Property<bool>(msg, "IsDeleted"))
                    .ExecuteUpdateAsync(s => s.SetProperty(msg => EF.Property<bool>(msg, "IsDeleted"), true), cancellationToken);
            }

            // Cascade team-dependent entities (single SQL each)
            await TeamRegistrations.IgnoreQueryFilters()
                .Where(r => teamsBeingDeleted.Contains(r.TeamId) && !EF.Property<bool>(r, "IsDeleted"))
                .ExecuteUpdateAsync(s => s.SetProperty(r => EF.Property<bool>(r, "IsDeleted"), true), cancellationToken);

            await TeamJoinRequests.IgnoreQueryFilters()
                .Where(r => teamsBeingDeleted.Contains(r.TeamId) && !EF.Property<bool>(r, "IsDeleted"))
                .ExecuteUpdateAsync(s => s.SetProperty(r => EF.Property<bool>(r, "IsDeleted"), true), cancellationToken);

            await TeamStats.IgnoreQueryFilters()
                .Where(s => teamsBeingDeleted.Contains(s.TeamId) && !EF.Property<bool>(s, "IsDeleted"))
                .ExecuteUpdateAsync(s => s.SetProperty(ts => EF.Property<bool>(ts, "IsDeleted"), true), cancellationToken);

            // Cascade to tournament players via affected players
            if (affectedPlayerIds.Count > 0)
            {
                await TournamentPlayers.IgnoreQueryFilters()
                    .Where(tp => affectedPlayerIds.Contains(tp.PlayerId) && !EF.Property<bool>(tp, "IsDeleted"))
                    .ExecuteUpdateAsync(s => s.SetProperty(tp => EF.Property<bool>(tp, "IsDeleted"), true), cancellationToken);
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
