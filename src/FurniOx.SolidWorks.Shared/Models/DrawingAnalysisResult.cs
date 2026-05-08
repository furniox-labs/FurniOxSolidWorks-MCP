using System.Collections.Generic;

namespace FurniOx.SolidWorks.Shared.Models;

/// <summary>
/// Comprehensive drawing analysis result containing sheets, views, annotations, and BOM data
/// </summary>
public sealed record DrawingAnalysisResult
{
    public DrawingMetadata Metadata { get; init; } = new();
    public List<DrawingSheet> Sheets { get; init; } = new();
    public List<DrawingView> Views { get; init; } = new();
    public List<DrawingAnnotation> Annotations { get; init; } = new();
    public List<BomTable> BomTables { get; init; } = new();
    public List<ReferencedModel> ReferencedModels { get; init; } = new();
    public Dictionary<string, string> CustomProperties { get; init; } = new();

    /// <summary>
    /// Configuration-specific custom properties keyed by configuration name.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>>? ConfigurationCustomProperties { get; init; }

    /// <summary>
    /// Built-in document summary information (Author, Title, Subject, Keywords, Comments, etc.)
    /// </summary>
    public DocumentSummaryInfo? SummaryInfo { get; init; }
}

/// <summary>
/// Drawing metadata and high-level information
/// </summary>
public sealed record DrawingMetadata
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "Drawing";
    public int SheetCount { get; init; }
    public int ViewCount { get; init; }
    public string ActiveSheet { get; init; } = string.Empty;
    public string DrawingStandard { get; init; } = string.Empty; // ANSI, ISO, etc.
    public string Units { get; init; } = "mm";
    public string ProjectionType { get; init; } = string.Empty; // First Angle, Third Angle
}

/// <summary>
/// Individual sheet information
/// </summary>
public sealed record DrawingSheet
{
    public string Name { get; init; } = string.Empty;
    public double Scale { get; init; }
    public string SheetFormat { get; init; } = string.Empty;
    public PaperSize PaperSize { get; init; } = new();
    public bool IsActive { get; init; }
    public int ViewCount { get; init; }
    public Dictionary<string, string> CustomProperties { get; init; } = new();
}

/// <summary>
/// Paper size information
/// </summary>
public sealed record PaperSize
{
    public string StandardSize { get; init; } = string.Empty; // A4, A3, Letter, etc.
    public double Width { get; init; } // mm
    public double Height { get; init; } // mm
}

/// <summary>
/// Drawing view information
/// </summary>
public sealed record DrawingView
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty; // Standard, Section, Detail, Projected, etc.
    public int TypeCode { get; init; }
    public string SheetName { get; init; } = string.Empty;
    public double Scale { get; init; }
    public Point2D Position { get; init; } = new();
    public double RotationAngle { get; init; } // degrees
    public string ReferencedModelPath { get; init; } = string.Empty;
    public string ReferencedConfiguration { get; init; } = string.Empty;
    public string? ParentViewName { get; init; } // For projected views
    public SectionViewProperties? SectionProperties { get; init; }
    public DetailViewProperties? DetailProperties { get; init; }
}

/// <summary>
/// 2D point for drawing coordinates
/// </summary>
public sealed record Point2D
{
    public double X { get; init; } // mm
    public double Y { get; init; } // mm
}

/// <summary>
/// Section view specific properties
/// </summary>
public sealed record SectionViewProperties
{
    public string CuttingLineLabel { get; init; } = string.Empty;
    public double CuttingLineAngle { get; init; } // degrees
    public string SectionDepth { get; init; } = string.Empty;
}

/// <summary>
/// Detail view specific properties
/// </summary>
public sealed record DetailViewProperties
{
    public string DetailCircleLabel { get; init; } = string.Empty;
    public Point2D CircleCenter { get; init; } = new();
    public double CircleRadius { get; init; } // mm
}

/// <summary>
/// Drawing annotation (dimensions, notes, balloons, etc.)
/// </summary>
public sealed record DrawingAnnotation
{
    public string Type { get; init; } = string.Empty; // Dimension, Note, Balloon, Datum, SurfaceFinish, Weld
    public int TypeCode { get; init; }
    public string Text { get; init; } = string.Empty;
    public string ViewName { get; init; } = string.Empty;
    public Point2D Position { get; init; } = new();
    public DimensionProperties? DimensionProps { get; init; }
    public BalloonProperties? BalloonProps { get; init; }
}

/// <summary>
/// Dimension-specific properties
/// </summary>
public sealed record DimensionProperties
{
    public string DimensionType { get; init; } = string.Empty; // Linear, Angular, Radial, Diameter, Ordinate
    public double Value { get; init; }
    public string Units { get; init; } = string.Empty;
    public double Tolerance { get; init; }
    public string ToleranceType { get; init; } = string.Empty;
}

/// <summary>
/// Balloon-specific properties
/// </summary>
public sealed record BalloonProperties
{
    public int ItemNumber { get; init; }
    public string ItemText { get; init; } = string.Empty;
    public string BalloonStyle { get; init; } = string.Empty; // Circular, Triangle, etc.
}

/// <summary>
/// BOM (Bill of Materials) table
/// </summary>
public sealed record BomTable
{
    public string TableName { get; init; } = string.Empty;
    public string TableType { get; init; } = string.Empty; // Parts Only, Indented, Top-level only
    public int TypeCode { get; init; }
    public string SheetName { get; init; } = string.Empty;
    public Point2D Position { get; init; } = new();
    public int RowCount { get; init; }
    public int ColumnCount { get; init; }
    public List<string> ColumnHeaders { get; init; } = new();
    public List<BomRow> Rows { get; init; } = new();
}

/// <summary>
/// BOM table row
/// </summary>
public sealed record BomRow
{
    public int RowIndex { get; init; }
    public int ItemNumber { get; init; }
    public int Quantity { get; init; }
    public string PartNumber { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Dictionary<string, string> Columns { get; init; } = new(); // All column values by header
}

/// <summary>
/// Referenced model information
/// </summary>
public sealed record ReferencedModel
{
    public string ModelPath { get; init; } = string.Empty;
    public string ModelType { get; init; } = string.Empty; // Part, Assembly
    public List<string> Configurations { get; init; } = new();
    public int ViewCount { get; init; } // How many views reference this model
}
