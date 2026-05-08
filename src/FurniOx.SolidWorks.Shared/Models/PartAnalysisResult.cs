using System.Collections.Generic;

namespace FurniOx.SolidWorks.Shared.Models;

/// <summary>
/// Comprehensive part analysis result containing features, bodies, and properties
/// </summary>
public record PartAnalysisResult
{
    public PartMetadata Metadata { get; init; } = new();
    public List<PartFeature> Features { get; init; } = new();
    public PartMassProperties? MassProperties { get; init; }
    public PartBoundingBox? BoundingBox { get; init; }
    public List<PartBody> Bodies { get; init; } = new();
    public Dictionary<string, string> CustomProperties { get; init; } = new();

    /// <summary>
    /// Configuration-specific custom properties keyed by configuration name.
    /// Only populated when includeCustomProperties is true and fields != "minimal".
    /// </summary>
    public Dictionary<string, Dictionary<string, string>>? ConfigurationCustomProperties { get; init; }

    /// <summary>
    /// Built-in document summary information (Author, Title, Subject, Keywords, Comments, etc.)
    /// </summary>
    public DocumentSummaryInfo? SummaryInfo { get; init; }

    /// <summary>
    /// Assembly context information when part is edited in-context within an assembly.
    /// Null when part is opened standalone (not in assembly context).
    /// </summary>
    public PartAssemblyContext? AssemblyContext { get; init; }

    // ========== Context-aware analysis metadata ==========

    /// <summary>
    /// Component name if analyzed from selection within an assembly (e.g., "Part1-1").
    /// Null if analyzing active document directly.
    /// </summary>
    public string? AnalyzedFromSelection { get; init; }

    /// <summary>
    /// Fields mode used for this analysis: "minimal", "standard", or "full"
    /// </summary>
    public string FieldsMode { get; init; } = "standard";

    /// <summary>
    /// Total feature count before any filtering
    /// </summary>
    public int TotalFeatureCount { get; init; }

    /// <summary>
    /// Number of features included in result after filtering
    /// </summary>
    public int FilteredFeatureCount { get; init; }

    /// <summary>
    /// Transform matrix when part was analyzed from assembly selection context.
    /// Reuses AssemblyTransform since the data structure is identical.
    /// Null when analyzing a standalone part document.
    /// </summary>
    public AssemblyTransform? Transform { get; init; }

    /// <summary>
    /// FeatureManager folder path when part was analyzed from assembly selection context.
    /// Indicates which folder the component belongs to in the parent assembly's FeatureManager tree.
    /// Null when analyzing a standalone part document or when component is not in a folder.
    /// </summary>
    public string? FeatureManagerFolderPath { get; init; }
}

/// <summary>
/// Assembly context information for a part being edited in-context
/// </summary>
public record PartAssemblyContext
{
    /// <summary>
    /// Name of the parent assembly document
    /// </summary>
    public string ParentAssemblyName { get; init; } = string.Empty;

    /// <summary>
    /// Full path to the parent assembly file
    /// </summary>
    public string ParentAssemblyPath { get; init; } = string.Empty;

    /// <summary>
    /// Component name within the parent assembly (e.g., "Part1-1")
    /// </summary>
    public string ComponentName { get; init; } = string.Empty;

    /// <summary>
    /// Part's origin position relative to the assembly origin (in mm)
    /// This is the translation portion of the component transform
    /// </summary>
    public Point3D OriginInAssembly { get; init; } = new();

    /// <summary>
    /// Full 4x4 transform matrix (16 elements) relative to assembly origin.
    /// Indices 0-8: 3x3 rotation matrix (row-major)
    /// Indices 9-11: X, Y, Z translation (in mm)
    /// Indices 12-15: Scale and other transform data
    /// </summary>
    public double[] TransformMatrix { get; init; } = new double[16];

    /// <summary>
    /// 3x3 rotation matrix extracted from transform (row-major order)
    /// </summary>
    public double[] RotationMatrix { get; init; } = new double[9];
}

/// <summary>
/// Part metadata and high-level information
/// </summary>
public record PartMetadata
{
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Full file path to the part document (e.g., "C:\Projects\Part.SLDPRT")
    /// </summary>
    public string Path { get; init; } = string.Empty;
    public string Type { get; init; } = "Part";
    public string ConfigurationName { get; init; } = string.Empty;
    public int TotalFeatures { get; init; }
    public int TotalBodies { get; init; }
    public string Units { get; init; } = "mm"; // mm, in, etc.
}

/// <summary>
/// Part feature information
/// </summary>
public record PartFeature
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty; // FeatureExtrusion, FeatureRevolve, etc.
    public string TypeName { get; init; } = string.Empty; // Human-readable type
    public bool Suppressed { get; init; }
    public int ErrorState { get; init; } // 0 = no error
    public Dictionary<string, object>? Parameters { get; init; }
    public int ChildFeatureCount { get; init; }
}

/// <summary>
/// Mass properties of the part
/// </summary>
public record PartMassProperties
{
    public double Mass { get; init; } // kg
    public double Volume { get; init; } // mm³
    public double SurfaceArea { get; init; } // mm²
    public Point3D CenterOfMass { get; init; } = new();
    public MomentOfInertia? MomentOfInertia { get; init; }
}

/// <summary>
/// Moment of inertia tensor
/// </summary>
public record MomentOfInertia
{
    public double Ixx { get; init; }
    public double Iyy { get; init; }
    public double Izz { get; init; }
    public double Ixy { get; init; }
    public double Iyz { get; init; }
    public double Ixz { get; init; }
}

/// <summary>
/// Bounding box of the part
/// </summary>
public record PartBoundingBox
{
    public double MinX { get; init; }
    public double MaxX { get; init; }
    public double MinY { get; init; }
    public double MaxY { get; init; }
    public double MinZ { get; init; }
    public double MaxZ { get; init; }
    public double Width { get; init; } // Max X - Min X
    public double Height { get; init; } // Max Y - Min Y
    public double Depth { get; init; } // Max Z - Min Z
}

/// <summary>
/// Solid body information
/// </summary>
public record PartBody
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty; // Solid, Sheet, Surface
    public bool Visible { get; init; }
    public string? MaterialName { get; init; }
    public int FaceCount { get; init; }
    public int EdgeCount { get; init; }
    public int VertexCount { get; init; }
}
