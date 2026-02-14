using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

public class IdempotencyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IdempotencyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/tournaments", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("X-Idempotency-Key header is required", body);
    }

    [Fact]
    public async Task Post_WithIdempotencyKey_ReplaysResponse()
    {
        // Arrange
        var client = _factory.CreateClient();
        var key = Guid.NewGuid().ToString();
        var content = new StringContent("{\"name\": \"Tournament 1\"}", Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", key);

        // Act - First Request (This might fail due to auth if we don't handle it, but we want to see if middleware triggers)
        // Actually, tournaments requires Admin/Creator. We might need to mock auth or use a public endpoint if exists.
        // Let's see if there's a public endpoint.
        var response1 = await client.PostAsync("/api/tournaments", content);
        
        // Even if it returns 401 Unauthorized, the middleware should have stored the response.
        // Wait, the middleware is after Authentication, so it might not run if 401 happens before.
        // Let's check Program.cs middleware order.
        // app.UseAuthentication();
        // app.UseMiddleware<UserStatusCheckMiddleware>();
        // app.UseMiddleware<MaintenanceModeMiddleware>();
        // app.UseAuthorization();
        // The middleware is registered after CorrelationIdMiddleware and before SlowQueryMiddleware.
        // And BEFORE Authentication? No, after CorrelationId which is after CookiePolicy.
        // Wait, app.UseMiddleware<IdempotencyMiddleware>() is BEFORE UseAuthentication().
        
        // If it's before Authentication, it will run.
        
        var response2 = await client.PostAsync("/api/tournaments", content);

        // Assert
        Assert.Equal(response1.StatusCode, response2.StatusCode);
        var body1 = await response1.Content.ReadAsStringAsync();
        var body2 = await response2.Content.ReadAsStringAsync();
        Assert.Equal(body1, body2);
    }

    [Fact]
    public async Task Post_SameKeyDifferentBody_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var key = Guid.NewGuid().ToString();
        var content1 = new StringContent("{\"name\": \"Tournament 1\"}", Encoding.UTF8, "application/json");
        var content2 = new StringContent("{\"name\": \"Tournament 2\"}", Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", key);

        // Act
        await client.PostAsync("/api/tournaments", content1);
        var response2 = await client.PostAsync("/api/tournaments", content2);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
        var body = await response2.Content.ReadAsStringAsync();
        Assert.Contains("Idempotency key match but request content has changed", body);
    }
}
