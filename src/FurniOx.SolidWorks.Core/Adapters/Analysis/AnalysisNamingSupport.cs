using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis;

internal static class AnalysisNamingSupport
{
    public static string GetFeatureTypeName(string typeCode)
    {
        return typeCode switch
        {
            "ICE" => "Extrusion",
            "Revolution" => "Revolve",
            "Hole" => "Hole",
            "Fillet" => "Fillet",
            "Chamfer" => "Chamfer",
            "Shell" => "Shell",
            "ProfileFeature" => "Sketch",
            "RefPlane" => "Reference Plane",
            _ => typeCode
        };
    }

    public static string GetMateTypeName(int typeCode)
    {
        return typeCode switch
        {
            0 => "Coincident",
            1 => "Concentric",
            2 => "Perpendicular",
            3 => "Parallel",
            4 => "Tangent",
            5 => "Distance",
            6 => "Angle",
            7 => "Unknown",
            8 => "Symmetric",
            9 => "CAM Follower",
            10 => "Gear",
            11 => "Width",
            12 => "Lock to Sketch",
            13 => "Rack Pinion",
            14 => "Max Mates",
            15 => "Path",
            16 => "Lock",
            17 => "Screw",
            18 => "Linear Coupler",
            19 => "Universal Joint",
            20 => "Coordinate System",
            21 => "Slot",
            22 => "Hinge",
            23 => "Profile Center",
            _ => $"Unknown({typeCode})"
        };
    }

    public static string GetEntityTypeName(object? entity)
    {
        return entity switch
        {
            null => "Unknown",
            Face2 => "Face",
            Edge => "Edge",
            Vertex => "Vertex",
            RefPlane => "Plane",
            _ => "Unknown"
        };
    }

    public static string GetEntityName(object? entity)
    {
        if (entity == null)
        {
            return "Unknown";
        }

        if (entity is RefPlane)
        {
            var feature = entity as IFeature;
            return feature?.Name ?? "Plane";
        }

        if (entity is Face2 face)
        {
            try
            {
                var surface = face.GetSurface() as Surface;
                if (surface != null)
                {
                    if (surface.IsPlane()) return "Planar Face";
                    if (surface.IsCylinder()) return "Cylindrical Face";
                    if (surface.IsCone()) return "Conical Face";
                    if (surface.IsSphere()) return "Spherical Face";
                    if (surface.IsTorus()) return "Toroidal Face";
                }
            }
            catch
            {
            }

            return "Face";
        }

        if (entity is Edge edge)
        {
            try
            {
                var curve = edge.GetCurve() as Curve;
                if (curve != null)
                {
                    if (curve.IsLine()) return "Linear Edge";
                    if (curve.IsCircle()) return "Circular Edge";
                    if (curve.IsEllipse()) return "Elliptical Edge";
                }
            }
            catch
            {
            }

            return "Edge";
        }

        if (entity is Vertex)
        {
            return "Vertex";
        }

        return entity.GetType().Name;
    }

    public static string GetPaperSizeName(int code)
    {
        return code switch
        {
            0 => "A",
            1 => "A Landscape",
            2 => "B",
            3 => "C",
            4 => "D",
            5 => "E",
            6 => "A4",
            7 => "A4 Landscape",
            8 => "A3",
            9 => "A3 Landscape",
            10 => "A2",
            11 => "A1",
            12 => "A0",
            _ => "Custom"
        };
    }

    public static string GetViewTypeName(int type)
    {
        return type switch
        {
            1 => "Sheet",
            2 => "Section",
            3 => "Detail",
            4 => "Projected",
            5 => "Auxiliary",
            6 => "Standard",
            7 => "Named",
            8 => "Relative",
            _ => $"Unknown({type})"
        };
    }

    public static string GetDimensionTypeName(int type)
    {
        return type switch
        {
            0 => "Unknown",
            1 => "Linear",
            2 => "Angular",
            3 => "Radial",
            4 => "Diameter",
            5 => "Horizontal Linear",
            6 => "Vertical Linear",
            7 => "Ordinate",
            8 => "Horizontal Ordinate",
            9 => "Vertical Ordinate",
            10 => "Arc Length",
            11 => "Chamfer",
            _ => $"Unknown({type})"
        };
    }

    public static string GetBomTypeName(int type)
    {
        return type switch
        {
            0 => "Indented",
            1 => "Parts Only",
            2 => "Top Level Only",
            _ => $"Unknown({type})"
        };
    }
}
