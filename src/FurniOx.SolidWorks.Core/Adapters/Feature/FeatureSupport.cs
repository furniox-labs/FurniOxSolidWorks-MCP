using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Features;

internal static class FeatureSupport
{
    public static string GetEntityType(int conditionType)
    {
        return conditionType switch
        {
            (int)swStartConditions_e.swStartSurface => "FACE",
            (int)swStartConditions_e.swStartVertex => "VERTEX",
            (int)swEndConditions_e.swEndCondUpToSurface => "FACE",
            (int)swEndConditions_e.swEndCondOffsetFromSurface => "FACE",
            (int)swEndConditions_e.swEndCondUpToVertex => "VERTEX",
            (int)swEndConditions_e.swEndCondUpToBody => "SOLIDBODY",
            (int)swEndConditions_e.swEndCondUpToSelection => "FACE",
            _ => "FACE"
        };
    }

    public static string[]? GetStringArrayParam(IDictionary<string, object?> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is string[] stringArray)
        {
            return stringArray;
        }

        if (value is string singleValue)
        {
            return new[] { singleValue };
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object?>().Where(item => item != null).Select(item => item!.ToString() ?? string.Empty).ToArray();
        }

        return null;
    }

    public static string GetFilletOptionsDescription(int options)
    {
        var flags = new List<string>();
        if ((options & 0x1) != 0) flags.Add("Propagate");
        if ((options & 0x2) != 0) flags.Add("UniformRadius");
        if ((options & 0x4) != 0) flags.Add("StraightTransitions");
        if ((options & 0x8) != 0) flags.Add("UseHelpPoint");
        if ((options & 0x20) != 0) flags.Add("CornerType");
        if ((options & 0x40) != 0) flags.Add("AttachEdges");
        if ((options & 0x80) != 0) flags.Add("KeepFeatures");
        if ((options & 0x100) != 0) flags.Add("CurvatureContinuous");
        if ((options & 0x200) != 0) flags.Add("ConstantWidth");
        if ((options & 0x400) != 0) flags.Add("NoTrimNoAttach");
        if ((options & 0x800) != 0) flags.Add("ReverseFace1Dir");
        if ((options & 0x1000) != 0) flags.Add("ReverseFace2Dir");
        if ((options & 0x2000) != 0) flags.Add("PropagateFeatureToParts");
        if ((options & 0x4000) != 0) flags.Add("Asymmetric");
        return flags.Count > 0 ? string.Join(" | ", flags) : "None";
    }
}

