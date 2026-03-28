using System.Linq;
using FurniOx.SolidWorks.Core.Adapters;
using SolidWorks.Interop.swconst;
using Xunit;

namespace FurniOx.SolidWorks.Core.Tests;

public sealed class BatchCustomPropertySupportTests
{
    [Fact]
    public void ParseBatchSetItems_SupportsSnakeCaseShape()
    {
        const string json = """
            [
              {
                "instance_path": "Part1-1",
                "configuration": "Default",
                "type": "text",
                "properties": {
                  "Material": "Steel",
                  "Finish": "Matte"
                }
              }
            ]
            """;

        var items = BatchCustomPropertyJsonSupport.ParseBatchSetItems(json);

        Assert.NotNull(items);
        var item = Assert.Single(items!);
        Assert.Equal("Part1-1", item.InstancePath);
        Assert.Equal("Default", item.Configuration);
        Assert.Equal("text", item.Type);
        Assert.Equal(2, item.Properties.Count);
    }

    [Fact]
    public void ParseBatchDeleteItems_SupportsCamelCaseShape()
    {
        const string json = """
            [
              {
                "instancePath": "SubAssy-1/Part-2",
                "configuration": "",
                "propertyNames": ["Material", "Weight"]
              }
            ]
            """;

        var items = BatchCustomPropertyJsonSupport.ParseBatchDeleteItems(json);

        Assert.NotNull(items);
        var item = Assert.Single(items!);
        Assert.Equal("SubAssy-1/Part-2", item.InstancePath);
        Assert.Equal(2, item.PropertyNames.Count);
        Assert.Contains("Material", item.PropertyNames);
    }

    [Theory]
    [InlineData("part.sldprt", swDocumentTypes_e.swDocPART)]
    [InlineData("subassy.sldasm", swDocumentTypes_e.swDocASSEMBLY)]
    [InlineData("layout.slddrw", swDocumentTypes_e.swDocDRAWING)]
    public void InferDocumentType_UsesExtension(string path, swDocumentTypes_e expectedType)
    {
        var type = TargetDocumentResolutionSupport.InferDocumentType(path);

        Assert.Equal(expectedType, type);
    }
}
