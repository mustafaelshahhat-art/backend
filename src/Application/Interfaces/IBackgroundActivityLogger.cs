using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface IBackgroundActivityLogger
{
    void LogActivity(string type, string message, Guid? userId = null, string? userName = null);
    void LogActivityByTemplate(string code, Dictionary<string, string> placeholders, Guid? userId = null, string? userName = null);
}
