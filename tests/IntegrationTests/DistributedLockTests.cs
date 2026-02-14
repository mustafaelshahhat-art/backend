using Application.Interfaces;
using FluentAssertions;
using IntegrationTests.Base;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class DistributedLockTests
{
    private readonly IntegrationTestFactory _factory;

    public DistributedLockTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DistributedLock_ShouldPreventConcurrentAcquisition()
    {
        // Arrange
        using var scope1 = _factory.Services.CreateScope();
        var lockService1 = scope1.ServiceProvider.GetRequiredService<IDistributedLock>();

        using var scope2 = _factory.Services.CreateScope();
        var lockService2 = scope2.ServiceProvider.GetRequiredService<IDistributedLock>();

        string lockKey = "test_lock";

        // Act
        var acquired1 = await lockService1.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(1));
        var acquired2 = await lockService2.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(1));

        // Assert
        acquired1.Should().BeTrue();
        acquired2.Should().BeFalse();

        // Release and try again
        await lockService1.ReleaseLockAsync(lockKey);
        var acquired2Retry = await lockService2.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(1));
        acquired2Retry.Should().BeTrue();
    }
}
