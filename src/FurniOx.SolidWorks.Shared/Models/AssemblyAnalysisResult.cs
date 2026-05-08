using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FurniOx.SolidWorks.Shared.Models;

/// <summary>
/// Comprehensive assembly analysis result containing components, mates, and properties
/// </summary>
public record AssemblyAnalysisResult
{
    public AssemblyMetadata Metadata { get; init; } = new();
    /// <summary>
    /// Assembly-level features (3D sketches, reference planes, reference geometry, etc.).
    /// Does NOT include component features - those are in each component's part.
    /// </summary>
    public List<PartFeature> Features { get; init; } = new();
    /// <summary>
    /// Flat list of components. Names use instance path format for nested components (e.g., "SubAssy-1/Part-1").
    /// </summary>
    public List<AssemblyComponent> Components { get; init; } = new();
    /// <summary>
    /// Optional hierarchy tree (root-level components with Children populated). Only present when requested.
    /// </summary>
    public List<AssemblyComponent>? Hierarchy { get; init; }
    public List<AssemblyMate> Mates { get; init; } = new();
    public PartMassProperties? MassProperties { get; init; }
    public AssemblyInterferenceCheck? InterferenceCheck { get; init; }
    public Dictionary<string, string> CustomProperties { get; init; } = new();

    /// <summary>
    /// Configuration-specific custom properties keyed by configuration name.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>>? ConfigurationCustomProperties { get; init; }

    /// <summary>
    /// Built-in document summary information (Author, Title, Subject, Keywords, Comments, etc.)
    /// </summary>
    public DocumentSummaryInfo? SummaryInfo { get; init; }

    /// <summary>
    /// Per-component-document properties, keyed by normalized file path.
    /// Deduplicates across instances: 50 instances of Part1.SLDPRT = 1 entry.
    /// Only populated when includeComponentProperties=true.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, ComponentDocumentProperties>? ComponentProperties { get; init; }

    /// <summary>
    /// File paths of components whose properties could not be read
    /// (model not loaded and openReferencedDocs=false, or open failed).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? SkippedPropertyPaths { get; init; }

    /// <summary>
    /// If analyzed from selection, the selected component name. Null if analyzing active doc.
    /// </summary>
    public string? AnalyzedFromSelection { get; init; }

    /// <summary>
    /// Path filter applied (null if none)
    /// </summary>
    public string? PathFilter { get; init; }

    /// <summary>
    /// Fields mode used: minimal, standard, full
    /// </summary>
    public string FieldsMode { get; init; } = "standard";

    /// <summary>
    /// Total components before filtering
    /// </summary>
    public int TotalComponentCount { get; init; }

    /// <summary>
    /// Components after filtering (same as Components.Count)
    /// </summary>
    public int FilteredComponentCount { get; init; }
}

/// <summary>
/// Assembly metadata and high-level information
/// </summary>
public record AssemblyMetadata
{
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Full file path to the assembly document (e.g., "C:\Projects\Assembly.SLDASM")
    /// </summary>
    public string Path { get; init; } = string.Empty;
    public string Type { get; init; } = "Assembly";
    public string ConfigurationName { get; init; } = string.Empty;
    public int ComponentCount { get; init; }
    public int TotalParts { get; init; }
    public int TotalSubAssemblies { get; init; }
    public int TotalMates { get; init; }
    public bool IsTopLevel { get; init; }
}

/// <summary>
/// Assembly component (part or subassembly)
/// </summary>
public record AssemblyComponent
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty; // Part, Assembly

    /// <summary>
    /// FeatureManager folder membership for this component in its immediate owning assembly.
    /// Null means the component is not in a FeatureManager folder (root of the component list),
    /// or folder membership was not requested/available.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FeatureManagerFolderPath { get; init; }

    // Boolean fields always serialized (false/true) for consistent API behavior
    public bool Suppressed { get; init; }
    public bool Hidden { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsEnvelope { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConfigurationName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int InstanceCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssemblyTransform? Transform { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AssemblyComponent>? Children { get; init; } // For hierarchical structure

    /// <summary>
    /// The selection string for use with SelectByID2 to select this component.
    /// Format: "ComponentName-Instance@AssemblyName" (e.g., "Part1-1@MyAssembly")
    /// Use this with select_by_id2(name=SelectByIDString, type="COMPONENT") to select the component.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SelectByIDString { get; init; }
}

/// <summary>
/// Assembly component transform (position and orientation)
/// </summary>
public record AssemblyTransform
{
    public double[] TransformMatrix { get; init; } = new double[16]; // 4x4 matrix
    public Point3D Translation { get; init; } = new();
    public double[] Rotation { get; init; } = new double[9]; // 3x3 rotation matrix
}

/// <summary>
/// Assembly mate information
/// </summary>
public record AssemblyMate
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty; // Coincident, Parallel, Perpendicular, Tangent, etc.
    public int TypeCode { get; init; }
    public bool Suppressed { get; init; }
    public bool Broken { get; init; }
    public List<MateEntity> Entities { get; init; } = new();
    public double? Distance { get; init; } // For distance mates (mm)
    public double? Angle { get; init; } // For angle mates (degrees)
}

/// <summary>
/// Entity referenced in a mate
/// </summary>
public record MateEntity
{
    public string ComponentName { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty; // Face, Edge, Vertex, Plane
    public string EntityName { get; init; } = string.Empty;
}

/// <summary>
/// Interference detection results
/// </summary>
public record AssemblyInterferenceCheck
{
    public bool HasInterferences { get; init; }
    public int InterferenceCount { get; init; }
    public List<InterferenceDetail> Interferences { get; init; } = new();
}

/// <summary>
/// Detailed interference between two components
/// </summary>
public record InterferenceDetail
{
    public string Component1Name { get; init; } = string.Empty;
    public string Component2Name { get; init; } = string.Empty;
    public double InterferenceVolume { get; init; } // mm³
    public string Type { get; init; } = string.Empty; // Interference, Coincidence, Containment
}
