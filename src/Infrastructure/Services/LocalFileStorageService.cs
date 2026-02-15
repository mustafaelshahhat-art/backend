using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<LocalFileStorageService> _logger;
    private const string UploadsFolder = "uploads";

    public LocalFileStorageService(
        IWebHostEnvironment environment, 
        IHttpContextAccessor httpContextAccessor,
        ILogger<LocalFileStorageService> logger)
    {
        _environment = environment;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var webRootPath = _environment.WebRootPath;
        if (string.IsNullOrEmpty(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var uploadsPath = Path.Combine(webRootPath, UploadsFolder);
        if (!Directory.Exists(uploadsPath))
        {
            Directory.CreateDirectory(uploadsPath);
        }

        var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var filePath = Path.Combine(uploadsPath, uniqueFileName);

        using (var outputStream = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(outputStream, ct);
        }

        _logger.LogInformation("File saved to {FilePath}", filePath);

        var request = _httpContextAccessor.HttpContext?.Request;
        var baseUrl = request != null ? $"{request.Scheme}://{request.Host}" : "";
        return $"{baseUrl}/{UploadsFolder}/{uniqueFileName}";
    }

    public Task DeleteFileAsync(string fileUrl, CancellationToken ct = default)
    {
        try
        {
            var uri = new Uri(fileUrl);
            var fileName = Path.GetFileName(uri.LocalPath);
            var webRootPath = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var filePath = Path.Combine(webRootPath, UploadsFolder, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("File deleted: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from {FileUrl}", fileUrl);
        }

        return Task.CompletedTask;
    }
}
