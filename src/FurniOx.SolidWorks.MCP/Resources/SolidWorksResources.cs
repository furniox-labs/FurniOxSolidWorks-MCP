using System;
using System.Linq;
using System.Text.Json;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Interfaces;
using FurniOx.SolidWorks.Shared.Configuration;
using ModelContextProtocol.Server;

namespace FurniOx.SolidWorks.MCP.Resources;

[McpServerResourceType]
public sealed class SolidWorksResources
{
    private readonly SolidWorksConnection _connection;
    private readonly ISmartRouter _router;
    private readonly SolidWorksSettings _settings;

    public SolidWorksResources(SolidWorksConnection connection, ISmartRouter router, SolidWorksSettings settings)
    {
        _connection = connection;
        _router = router;
        _settings = settings;
    }

    [McpServerResource(UriTemplate = "solidworks://connection/status",
        Name = "Connection Status",
        MimeType = "application/json")]
    public string GetConnectionStatus()
    {
        var (connected, healthy, revision, visible) = _connection.GetConnectionInfo();

        var status = new
        {
            Connected = connected,
            Healthy = healthy,
            Application = connected
                ? new { Revision = revision, Visible = visible }
                : (object?)null
        };

        return JsonSerializer.Serialize(status, JsonOptions);
    }

    [McpServerResource(UriTemplate = "solidworks://document/active",
        Name = "Active Document",
        MimeType = "application/json")]
    public string GetActiveDocument()
    {
        var (hasDoc, title, path, typeName, typeCode, saved, readOnly, errorReason) =
            _connection.GetActiveDocumentInfo();

        if (!hasDoc)
        {
            return JsonSerializer.Serialize(
                new { ActiveDocument = (object?)null, Reason = errorReason },
                JsonOptions);
        }

        var doc = new
        {
            Title = title,
            Path = path,
            Type = typeName,
            TypeCode = typeCode,
            Saved = saved,
            ReadOnly = readOnly
        };

        return JsonSerializer.Serialize(new { ActiveDocument = doc }, JsonOptions);
    }

    [McpServerResource(UriTemplate = "solidworks://metrics/performance",
        Name = "Performance Metrics",
        MimeType = "application/json")]
    public string GetPerformanceMetrics()
    {
        var metrics = _router.GetPerformanceMetrics();

        var data = new
        {
            TotalOperationTypes = metrics.Count,
            Metrics = metrics
                .Select(m => new
                {
                    m.Operation,
                    m.Invocations,
                    m.Successes,
                    Failures = m.Invocations - m.Successes,
                    SuccessRate = m.Invocations > 0
                        ? Math.Round((double)m.Successes / m.Invocations * 100, 1)
                        : 0.0,
                    AverageDurationMs = m.Invocations > 0
                        ? Math.Round(m.TotalDuration.TotalMilliseconds / m.Invocations, 1)
                        : 0.0
                })
                .OrderByDescending(m => m.Invocations)
                .ToList()
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    [McpServerResource(UriTemplate = "solidworks://project/about",
        Name = "Project About",
        MimeType = "application/json")]
    public string GetProjectAbout()
    {
        return JsonSerializer.Serialize(
            new
            {
                Name = "FurniOx SolidWorks MCP",
                Profile = new
                {
                    Mode = "public-basic",
                    PublicProfileEnabled = _settings.PublicProfile.Enabled
                },
                Contact = new
                {
                    GitHubRepository = _settings.PublicProfile.Contact.GitHubRepositoryUrl,
                    GitHubIssues = _settings.PublicProfile.Contact.GitHubIssuesUrl,
                    GitHubDiscussions = _settings.PublicProfile.Contact.GitHubDiscussionsUrl,
                    LinkedIn = _settings.PublicProfile.Contact.LinkedInUrl
                }
            },
            JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
