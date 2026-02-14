using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Application.Interfaces;
using Domain.Interfaces;

namespace Infrastructure.BackgroundJobs;

public class DomainEventTypeCache : IDomainEventTypeCache
{
    private readonly ConcurrentDictionary<string, Type> _cache = new();

    public DomainEventTypeCache()
    {
        // PROD-AUDIT: Refactored to scan only Domain/Application assemblies and handle TypeLoadExceptions safely.
        // Scanning all loaded assemblies causes crashes with third-party libs like Microsoft.Data.SqlClient.
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName != null && (a.FullName.StartsWith("Domain") || a.FullName.StartsWith("Application")));

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var type in types)
                {
                    _cache.TryAdd(type.Name, type);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Safety fallback for partially loaded types
                var validTypes = ex.Types.Where(t => t != null);
                foreach (var type in validTypes)
                {
                    if (typeof(IDomainEvent).IsAssignableFrom(type) && !type!.IsInterface && !type.IsAbstract)
                    {
                        _cache.TryAdd(type!.Name, type);
                    }
                }
            }
            catch
            {
                // Skip problematic assemblies
            }
        }
    }

    public Type? GetEventType(string typeName)
    {
        return _cache.TryGetValue(typeName, out var type) ? type : null;
    }
}
