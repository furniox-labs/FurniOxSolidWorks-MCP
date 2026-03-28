using System.Collections.Generic;

namespace FurniOx.SolidWorks.Shared.Models;

/// <summary>
/// Comprehensive sketch analysis result containing all geometry, constraints, and dimensions
/// </summary>
public sealed record SketchAnalysisResult
{
    public SketchMetadata Metadata { get; init; } = new();
    public List<SketchPoint> Points { get; init; } = new();
    public List<SketchSegment> Segments { get; init; } = new();
    public List<SketchRelation> Relations { get; init; } = new();
    public List<SketchDimension> Dimensions { get; init; } = new();
    public SketchStatistics? Statistics { get; init; }
    public SketchConnectivity? Connectivity { get; init; }
    public SketchValidation? Validation { get; init; }

    // ========== Context-aware analysis metadata (aligned with PartAnalysisResult/AssemblyAnalysisResult) ==========

    /// <summary>
    /// How the sketch was found for analysis: "selected" (from tree selection) or "active" (in edit mode)
    /// </summary>
    public string? AnalysisSource { get; init; }

    /// <summary>
    /// Fields mode used for this analysis: "minimal", "standard", or "full"
    /// </summary>
    public string FieldsMode { get; init; } = "standard";
}

/// <summary>
/// Sketch metadata and high-level information
/// </summary>
public sealed record SketchMetadata
{
    public string SketchName { get; init; } = string.Empty;
    public string FeatureName { get; init; } = string.Empty;
    public bool Is3D { get; init; }
    public string ReferencePlane { get; init; } = string.Empty;
    public int TotalSegments { get; init; }
    public int TotalPoints { get; init; }
    public int TotalRelations { get; init; }
    public int TotalDimensions { get; init; }
    public SketchBounds? Bounds { get; init; }
    public string Complexity { get; init; } = "simple"; // simple, medium, complex
}

/// <summary>
/// Bounding box for sketch entities
/// </summary>
public sealed record SketchBounds
{
    public double MinX { get; init; }
    public double MaxX { get; init; }
    public double MinY { get; init; }
    public double MaxY { get; init; }
    public double MinZ { get; init; }
    public double MaxZ { get; init; }
}

/// <summary>
/// Sketch point with coordinates
/// </summary>
public sealed record SketchPoint
{
    public int Index { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
}

/// <summary>
/// Sketch segment with type-specific geometry
/// </summary>
public sealed record SketchSegment
{
    public int Index { get; init; }
    public int TypeCode { get; init; }
    public string Type { get; init; } = string.Empty; // Line, Arc, Circle, Ellipse, Spline, etc.
    public int[]? Id { get; init; } // SolidWorks GetID() result [2 integers]
    public bool IsConstruction { get; init; }
    public SketchSegmentGeometry Geometry { get; init; } = new();
}

/// <summary>
/// Type-specific geometry for sketch segments
/// </summary>
public sealed record SketchSegmentGeometry
{
    // Common properties
    public Point3D? StartPoint { get; init; }
    public Point3D? EndPoint { get; init; }

    // Arc/Circle properties
    public Point3D? CenterPoint { get; init; }
    public double? Radius { get; init; }
    public bool? IsFullCircle { get; init; }
    public double? SweepAngle { get; init; } // Degrees
    public double? SweepAngleRadians { get; init; }
    public string? Direction { get; init; } // CW, CCW

    // Ellipse properties
    public Point3D? MajorPoint { get; init; }
    public Point3D? MinorPoint { get; init; }
    public double? MajorRadius { get; init; }
    public double? MinorRadius { get; init; }

    // Spline properties (future)
    public List<Point3D>? ControlPoints { get; init; }

    // Calculated properties
    public double? Length { get; init; }
}

/// <summary>
/// 3D point coordinate
/// </summary>
public sealed record Point3D
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
}

/// <summary>
/// Sketch relation/constraint information
/// </summary>
public sealed record SketchRelation
{
    public int Index { get; init; }
    public int TypeCode { get; init; }
    public string Type { get; init; } = string.Empty; // Parallel, Perpendicular, Tangent, etc.
    public bool Suppressed { get; init; }
    public int EntityCount { get; init; }
    public List<RelationEntity> Entities { get; init; } = new();
    public string? Status { get; init; } // satisfied, over-constrained, broken
}

/// <summary>
/// Entity referenced in a relation
/// </summary>
public sealed record RelationEntity
{
    public string Type { get; init; } = string.Empty; // SketchSegment, SketchPoint, ModelGeometry
    public int? SegmentType { get; init; } // For SketchSegment: 0=Line, 1=Arc, etc.
    public string? SegmentTypeName { get; init; }
    public int[]? Id { get; init; } // SolidWorks GetID() result
    public int? Index { get; init; } // Index in segments/points array
    public string? ModelGeometryType { get; init; } // Plane, Edge, Face
    public string? ModelGeometryName { get; init; }
}

/// <summary>
/// Sketch dimension information
/// </summary>
public sealed record SketchDimension
{
    public string Name { get; init; } = string.Empty;
    public double Value { get; init; } // In mm
    public int TypeCode { get; init; }
    public string Type { get; init; } = string.Empty; // Linear, Angular, Radial, Diameter
    public bool IsDriven { get; init; }
    public bool? IsEquationDriven { get; init; }
    public string? Tolerance { get; init; }
    public List<int>? ReferencedEntityIndices { get; init; } // Indices of segments/points this dimension references
}

/// <summary>
/// Statistical analysis of sketch
/// </summary>
public sealed record SketchStatistics
{
    public double TotalLength { get; init; } // Total perimeter in mm
    public double? TotalArea { get; init; } // Enclosed area in mm²
    public Dictionary<string, int> EntityTypeDistribution { get; init; } = new();
    public int ConstructionEntities { get; init; }
    public int RegularEntities { get; init; }
}

/// <summary>
/// Connectivity analysis of sketch entities (pure geometry-based).
/// </summary>
public sealed record SketchConnectivity
{
    public double ToleranceMm { get; init; }
    public bool IncludeConstructionGeometry { get; init; }
    public int ConsideredSegmentCount { get; init; }
    public int EndpointCount { get; init; }
    public int OpenEndpointCount { get; init; }
    public List<SketchOpenEndpoint> OpenEndpoints { get; init; } = new();
}

public sealed record SketchOpenEndpoint
{
    public int SegmentIndex { get; init; }
    public int[]? SegmentId { get; init; }
    public string SegmentType { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty; // "start" | "end"
    public Point3D Point { get; init; } = new();
    public double? NearestEndpointDistanceMm { get; init; }
}

/// <summary>
/// Validation results for sketch
/// </summary>
public sealed record SketchValidation
{
    public bool IsFullyConstrained { get; init; }
    public int DegreesOfFreedom { get; init; }
    public bool HasOverConstraints { get; init; }
    public bool HasBrokenConstraints { get; init; }
    public List<string> ValidationMessages { get; init; } = new();
}
