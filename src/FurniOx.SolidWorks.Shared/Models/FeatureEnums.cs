namespace FurniOx.SolidWorks.Shared.Models;

/// <summary>
/// Thin wall type options for revolve features
/// Maps to SolidWorks swThinWallType_e enumeration
/// </summary>
public enum ThinWallType
{
    /// <summary>Thin wall extends in one direction (default outward)</summary>
    OneDirection = 0,
    /// <summary>Thin wall extends in opposite direction</summary>
    TwoDirection = 1,
    /// <summary>Thin wall extends equally from sketch plane</summary>
    MidPlane = 2,
    /// <summary>Thin wall extends in both directions with different thicknesses</summary>
    TwoDirectionDifferent = 3
}

/// <summary>
/// Fillet types for fillet features
/// </summary>
public enum FilletType
{
    /// <summary>Constant radius fillet along edges</summary>
    ConstantRadius = 0,
    /// <summary>Variable radius fillet with different radii at control points</summary>
    VariableRadius = 1,
    /// <summary>Face fillet between two face sets</summary>
    FaceFillet = 2,
    /// <summary>Full round fillet between three adjacent faces</summary>
    FullRound = 3
}

/// <summary>
/// Fillet overflow handling options
/// </summary>
public enum FilletOverflowType
{
    /// <summary>Default overflow handling</summary>
    Default = 0,
    /// <summary>Keep edge when overflow occurs (more predictable)</summary>
    KeepEdge = 1,
    /// <summary>Keep surface when overflow occurs (smoother)</summary>
    KeepSurface = 2
}

/// <summary>
/// Fillet profile types
/// </summary>
public enum FilletProfileType
{
    /// <summary>Circular profile (standard)</summary>
    Circular = 0,
    /// <summary>Conic profile controlled by Rho parameter</summary>
    ConicRho = 1,
    /// <summary>Conic profile controlled by radius</summary>
    ConicRadius = 2,
    /// <summary>Conic profile with zero chamfer at ends</summary>
    ConicRhoZeroChamfer = 3
}

/// <summary>
/// Shell direction options
/// </summary>
public enum ShellDirection
{
    /// <summary>Shell inward (preserves exterior dimensions) - most common</summary>
    Inward = 0,
    /// <summary>Shell outward (expands exterior dimensions)</summary>
    Outward = 1
}
