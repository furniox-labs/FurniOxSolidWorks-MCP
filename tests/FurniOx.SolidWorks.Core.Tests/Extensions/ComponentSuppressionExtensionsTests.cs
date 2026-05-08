namespace FurniOx.SolidWorks.Core.Tests.Extensions;

/// <summary>
/// Test coverage analysis for <see cref="FurniOx.SolidWorks.Core.Extensions.ComponentSuppressionExtensions"/>.
///
/// UNTESTABLE SURFACE (2 of 2 public methods):
///
/// Both methods — <c>ReadSuppressionState(IComponent2)</c> and
/// <c>IsTrulySuppressed(IComponent2)</c> — are thin wrappers over
/// <c>IComponent2.GetSuppression2()</c> and <c>IComponent2.GetSuppression()</c>.
///
/// <c>IComponent2</c> is defined in <c>SolidWorks.Interop.sldworks.dll</c>,
/// which is NOT referenced by this test project (only <c>swconst</c> is).
/// Adding the reference would require a SolidWorks installation on every CI
/// agent and still produce a COM interface that cannot be instantiated in
/// isolation. Neither Moq nor NSubstitute can proxy a COM-interop interface
/// that requires the SolidWorks runtime to back it.
///
/// WHAT WAS VERIFIED INSTEAD:
/// The suppression-state logic (fall back from <c>GetSuppression2</c> when it
/// returns <c>swComponentInternalIdMismatch</c> = 5) is a one-liner with no
/// pure-logic branching that can be extracted without coupling to the COM
/// interface. The correct coverage path for this class is an integration test
/// that exercises a real (or in-process bridge) <c>IComponent2</c>.
/// </summary>
public sealed class ComponentSuppressionExtensionsTests
{
    // Placeholder: no unit-testable surface exists for this class.
    // See XML doc above for rationale.
}
