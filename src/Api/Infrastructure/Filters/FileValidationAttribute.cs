using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Api.Infrastructure.Filters;

public class FileValidationAttribute : ActionFilterAttribute
{
    private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };
    private readonly string[] _allowedMimeTypes = { "image/jpeg", "image/png", "application/pdf" };
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var files = context.HttpContext.Request.Form.Files;

        foreach (var file in files)
        {
            if (file.Length > MaxFileSize)
            {
                context.Result = new BadRequestObjectResult($"File {file.FileName} exceeds the 5MB limit.");
                return;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension) || !_allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                context.Result = new BadRequestObjectResult($"File {file.FileName} has an invalid type. Allowed types: JPG, PNG, PDF.");
                return;
            }
            
            // PROD-AUDIT: Optional magic bytes check could go here
        }

        base.OnActionExecuting(context);
    }
}
