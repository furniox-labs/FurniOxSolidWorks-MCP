namespace FurniOx.SolidWorks.Core.Adapters.SketchParametric;

internal static class SketchParametricTypeSupport
{
    internal static string? MapConstraintTypeToCode(string constraintType)
    {
        return constraintType switch
        {
            "fixed" => "sgFIXED",
            "coincident" => "sgCOINCIDENT",
            "horizontal" => "sgHORIZONTAL2D",
            "vertical" => "sgVERTICAL2D",
            "midpoint" => "sgATMIDDLE",
            "collinear" => "sgCOLINEAR",
            "perpendicular" => "sgPERPENDICULAR",
            "parallel" => "sgPARALLEL",
            "equallength" => "sgSAMELENGTH",
            "tangent" => "sgTANGENT",
            "samecurvelength" => "sgSAMECURVELENGTH",
            "concentric" => "sgCONCENTRIC",
            "coradial" => "sgCORADIAL",
            _ => null
        };
    }

    internal static int GetRequiredConstraintEntityCount(string constraintType)
    {
        return constraintType switch
        {
            "fixed" => 1,
            "horizontal" => 1,
            "vertical" => 1,
            _ => 2
        };
    }

    internal static bool IsValidDimensionType(string dimensionType)
    {
        return dimensionType is "distance" or "angle" or "diameter" or "radius";
    }

    internal static int GetRequiredDimensionEntityCount(string dimensionType)
    {
        return dimensionType switch
        {
            "diameter" => 1,
            "radius" => 1,
            "distance" => 2,
            "angle" => 2,
            _ => 2
        };
    }
}
