using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Sorting;

internal sealed record SortingFeatureTreeFeature(IFeature Feature, string? FolderPath);

internal sealed record SortingFeatureFolderGroupState(
    Dictionary<string, int> DesiredIndexByName,
    List<(string? FolderPath, List<IFeature> Features)> Groups);

internal sealed record SortingFeatureTreeComponent(IComponent2 Component, string? FolderPath);

internal sealed record SortingTopLevelTreeItem(
    IFeature Feature,
    string DisplayName,
    bool IsFolder,
    string? FolderPath,
    string? ComponentName2);

internal sealed record SortingFolderListItem(
    string Name,
    string Path,
    int Position);

internal sealed record SortingFolderGroupState(
    Dictionary<string, int> DesiredIndexByName,
    List<(string? FolderPath, List<IComponent2> Components)> Groups);

internal sealed class SortingPositionEntry
{
    public string Name { get; init; } = string.Empty;
    public int Position { get; init; }
}
