using Domain.Entities;
using FluentAssertions;
using IntegrationTests.Base;
using Microsoft.Extensions.DependencyInjection;
using Application.Interfaces;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class NestedTransactionTests
{
    private readonly IntegrationTestFactory _factory;

    public NestedTransactionTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_ShouldHandleNestedTransactions_WithoutError()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
        var context = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();

        var tournament = new Tournament { Name = "Nested Trans Test", MaxTeams = 2 };
        context.Tournaments.Add(tournament);
        await context.SaveChangesAsync();

        bool nestedExecuted = false;

        // Act
        var act = async () => 
        {
            await transactionManager.ExecuteInTransactionAsync(async () =>
            {
                // Outer transaction
                var t = await context.Tournaments.FindAsync(tournament.Id);
                t!.Name = "Outer Name";
                await context.SaveChangesAsync();

                await transactionManager.ExecuteInTransactionAsync(async () =>
                {
                    // Inner nested transaction
                    var tInner = await context.Tournaments.FindAsync(tournament.Id);
                    tInner!.Name = "Inner Name";
                    await context.SaveChangesAsync();
                    nestedExecuted = true;
                });
            });
        };

        // Assert
        await act.Should().NotThrowAsync();
        nestedExecuted.Should().BeTrue();

        // Refresh and verify final state
        var final = await context.Tournaments.FindAsync(tournament.Id);
        final!.Name.Should().Be("Inner Name");
    }
}
