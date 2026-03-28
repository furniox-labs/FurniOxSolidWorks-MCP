using System.IO;

namespace FurniOx.SolidWorks.Shared.Configuration;

public sealed class SolidWorksSettings
{
    /// <summary>
    /// Legacy SolidWorks version hint used for version-specific COM ProgIDs.
    /// Existing configs may still populate this value.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Optional SolidWorks major version hint for version-specific COM ProgIDs,
    /// for example "31". If omitted, the generic "SldWorks.Application" ProgID is
    /// still attempted first.
    /// </summary>
    public string? ProgIdVersion { get; set; }

    /// <summary>
    /// Optional installation year used only for conventional template path fallbacks,
    /// for example "2024". Prefer explicit template paths when possible.
    /// </summary>
    public string? TemplateVersion { get; set; }

    public CircuitBreakerSettings CircuitBreaker { get; set; } = new();
    public PublicProfileSettings PublicProfile { get; set; } = new();
    public int ComParameterLimit { get; set; } = 24;

    /// <summary>
    /// Path to the Part template file (.prtdot).
    /// If not specified, defaults to standard SolidWorks installation path.
    /// </summary>
    public string? PartTemplatePath { get; set; }

    /// <summary>
    /// Path to the Assembly template file (.asmdot).
    /// If not specified, defaults to standard SolidWorks installation path.
    /// </summary>
    public string? AssemblyTemplatePath { get; set; }

    /// <summary>
    /// Path to the Drawing template file (.drwdot).
    /// If not specified, defaults to standard SolidWorks installation path.
    /// </summary>
    public string? DrawingTemplatePath { get; set; }

    /// <summary>
    /// Gets the configured or conventional Part template path if available.
    /// </summary>
    public string? GetPartTemplatePath() => ResolveTemplatePath(PartTemplatePath, "Part.prtdot");

    /// <summary>
    /// Gets the configured or conventional Assembly template path if available.
    /// </summary>
    public string? GetAssemblyTemplatePath() => ResolveTemplatePath(AssemblyTemplatePath, "Assembly.asmdot");

    /// <summary>
    /// Gets the configured or conventional Drawing template path if available.
    /// </summary>
    public string? GetDrawingTemplatePath() => ResolveTemplatePath(DrawingTemplatePath, "Drawing.drwdot");

    public string? GetProgIdVersionHint()
    {
        if (!string.IsNullOrWhiteSpace(ProgIdVersion))
        {
            return ProgIdVersion.Trim();
        }

        return string.IsNullOrWhiteSpace(Version) ? null : Version.Trim();
    }

    private string? ResolveTemplatePath(string? explicitPath, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        if (string.IsNullOrWhiteSpace(TemplateVersion))
        {
            return null;
        }

        return $@"C:\ProgramData\SolidWorks\SOLIDWORKS {TemplateVersion.Trim()}\templates\{fileName}";
    }
}

public sealed class CircuitBreakerSettings
{
    public int FailureThreshold { get; set; } = 5;
    public int ResetTimeoutSeconds { get; set; } = 60;
}

public sealed class PublicProfileSettings
{
    public bool Enabled { get; set; } = true;

    public ProjectContactSettings Contact { get; set; } = new();
}

public sealed class ProjectContactSettings
{
    public string? GitHubRepositoryUrl { get; set; }

    public string? GitHubIssuesUrl { get; set; }

    public string? GitHubDiscussionsUrl { get; set; }

    public string? LinkedInUrl { get; set; }
}
