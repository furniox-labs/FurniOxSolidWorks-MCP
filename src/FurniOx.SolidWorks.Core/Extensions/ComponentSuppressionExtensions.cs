using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Extensions;

/// <summary>
/// Reads <see cref="IComponent2"/> suppression state robustly across heavy
/// rename/rebrand operations. <c>GetSuppression2</c> is the documented preferred
/// API but returns <see cref="swComponentSuppressionState_e.swComponentInternalIdMismatch"/>
/// (=5) when a component's cached internal ID has drifted from the assembly's
/// — which happens for components that weren't loaded during recent rename
/// operations. In that case fall back to the older <c>GetSuppression</c>, which
/// doesn't track IDs and returns the raw enum state regardless.
/// Centralised so analyze/manual/batch/rename paths all see the same answer.
/// </summary>
public static class ComponentSuppressionExtensions
{
    /// <summary>
    /// Returns the canonical suppression state as <see cref="swComponentSuppressionState_e"/>
    /// integer. Never returns <c>swComponentInternalIdMismatch</c>.
    /// </summary>
    public static int ReadSuppressionState(this IComponent2 component)
    {
        var state = component.GetSuppression2();
        if (state == (int)swComponentSuppressionState_e.swComponentInternalIdMismatch)
        {
            state = component.GetSuppression();
        }
        return state;
    }

    /// <summary>
    /// True if the component is genuinely suppressed (not lightweight, not
    /// unresolved, not just ID-mismatched).
    /// </summary>
    public static bool IsTrulySuppressed(this IComponent2 component)
        => component.ReadSuppressionState() == (int)swComponentSuppressionState_e.swComponentSuppressed;
}
