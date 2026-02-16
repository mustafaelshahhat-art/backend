using System;
using System.Collections.Generic;

namespace Application.Interfaces;

public interface IBackgroundActivityLogger
{
    void LogActivity(string type, string message, Guid? userId = null, string? userName = null);
    void LogActivityByTemplate(string code, Dictionary<string, string> placeholders, Guid? userId = null, string? userName = null);

    // Enriched overloads with entity context
    void LogActivity(string type, string message, Guid? userId, string? userName,
        string? actorRole, Guid? entityId, string? entityType, string? entityName, string? metadata);
    void LogActivityByTemplate(string code, Dictionary<string, string> placeholders, Guid? userId, string? userName,
        string? actorRole, Guid? entityId, string? entityType, string? entityName, string? metadata);
}
