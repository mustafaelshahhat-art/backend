using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using IntegrationTests.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Exceptions;
using System.Net;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class TournamentIntegrationTests
{
    private readonly IntegrationTestFactory _factory;

    public TournamentIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateTournament_ShouldThrowConcurrencyException_WhenConcurrentUpdatesOccur()
    {
        // Arrange
        using var scope1 = _factory.Services.CreateScope();
        var context1 = scope1.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();
        
        var tournament = new Tournament { Name = "Concurrency Test", MaxTeams = 10 };
        context1.Tournaments.Add(tournament);
        await context1.SaveChangesAsync();

        // Simulate two different "instances" fetching the same entity
        using var scope2 = _factory.Services.CreateScope();
        var context2 = scope2.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();
        
        var t1 = await context1.Tournaments.FindAsync(tournament.Id);
        var t2 = await context2.Tournaments.FindAsync(tournament.Id);

        // Act & Assert
        t1!.Name = "Update 1";
        await context1.SaveChangesAsync();

        t2!.Name = "Update 2";
        var act = () => context2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task RegisterTeam_ShouldRollback_IfUnexpectedFailureOccurs()
    {
        // This test specifically targets our TransactionManager implementation
        // Note: Real registration logic uses ITransactionManager in the pipeline/service
        
        using var scope = _factory.Services.CreateScope();
        var transactionManager = scope.ServiceProvider.GetRequiredService<Application.Interfaces.ITransactionManager>();
        var context = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();

        var tournament = new Tournament { Name = "Rollback Test", MaxTeams = 10 };
        context.Tournaments.Add(tournament);
        await context.SaveChangesAsync();

        var initialCount = await context.Tournaments.CountAsync();

        // Act
        try
        {
            await transactionManager.ExecuteInTransactionAsync(async () =>
            {
                var t = await context.Tournaments.FindAsync(tournament.Id);
                t!.Name = "Modified Name";
                await context.SaveChangesAsync();

                throw new Exception("Simulated Failure");
            });
        }
        catch
        {
            // Ignored
        }

        // Assert
        var reloaded = await context.Tournaments.FindAsync(tournament.Id);
        reloaded!.Name.Should().Be("Rollback Test"); // Should NOT be modified
    }
}
