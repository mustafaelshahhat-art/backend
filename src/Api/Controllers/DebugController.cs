using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous] // Make sure we can access it without login
public class DebugController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _env;

    public DebugController(IConfiguration configuration, AppDbContext dbContext, IWebHostEnvironment env)
    {
        _configuration = configuration;
        _dbContext = dbContext;
        _env = env;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var dbConn = _configuration.GetConnectionString("DefaultConnection") ?? "NULL";
        var redisConn = _configuration["Redis:ConnectionString"] ?? _configuration.GetConnectionString("Redis") ?? "NULL";

        // Mask secrets
        var maskedDb = MaskConnectionString(dbConn);
        
        var status = new
        {
            Environment = _env.EnvironmentName,
            ServerTime = DateTime.UtcNow,
            DbConfigured = maskedDb,
            RedisConfigured = redisConn,
            AdminPasswordSet = !string.IsNullOrEmpty(_configuration["AdminSettings:Password"]),
            JwtSecretSet = !string.IsNullOrEmpty(_configuration["JwtSettings:Secret"]),
            CanConnectToDb = false,
            DbError = "None"
        };

        try
        {
            await _dbContext.Database.CanConnectAsync();
            return Ok(new { status.Environment, status.ServerTime, status.DbConfigured, CanConnectToDb = true });
        }
        catch (Exception ex)
        {
            return Ok(new { status.Environment, status.ServerTime, status.DbConfigured, CanConnectToDb = false, DbError = ex.Message });
        }
    }

    private string MaskConnectionString(string conn)
    {
        if (string.IsNullOrEmpty(conn)) return "EMPTY";
        // Simple mask: show start, hide password
        try {
            var parts = conn.Split(';');
            var visible = parts.Where(p => !p.ToLower().Contains("password") && !p.ToLower().Contains("user id") && !p.ToLower().Contains("uid") && !p.ToLower().Contains("pwd")).ToArray();
            return string.Join(";", visible) + ";Password=***;UserId=***";
        } catch {
             return "PARSING_ERROR";
        }
    }

    [HttpGet("config-dump")]
    public IActionResult GetConfigDump()
    {
        var debugInfo = new Dictionary<string, string>();
        
        // 1. DUMP ALL CONFIG KEYS (Ordered)
        foreach(var kvp in _configuration.AsEnumerable().OrderBy(k => k.Key))
        {
            // Simple masking for safety
            var val = kvp.Value;
            if (!string.IsNullOrEmpty(val) && val.Length > 10)
                val = val.Substring(0, 3) + "***" + val.Substring(val.Length - 3);
            
            debugInfo[$"Config: {kvp.Key}"] = val ?? "NULL";
        }
        
        // 2. DUMP ALL OS ENV VARS
        var envVars = Environment.GetEnvironmentVariables();
        foreach (System.Collections.DictionaryEntry de in envVars)
        {
            var key = de.Key.ToString();
            var val = de.Value?.ToString();
             if (!string.IsNullOrEmpty(val) && val.Length > 10)
                val = val.Substring(0, 3) + "***" + val.Substring(val.Length - 3);

             debugInfo[$"OS_Env: {key}"] = val ?? "NULL";
        }

        return Ok(debugInfo);
    }
}
