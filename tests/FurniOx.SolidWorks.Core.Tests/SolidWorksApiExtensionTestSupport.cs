using System;

namespace FurniOx.SolidWorks.Core.Tests;

internal static class SolidWorksApiExtensionTestSupport
{
    public static Array CreateOneBasedArray(Type elementType, params object?[] values)
    {
        var array = Array.CreateInstance(elementType, lengths: [values.Length], lowerBounds: [1]);
        for (var i = 0; i < values.Length; i++)
        {
            array.SetValue(values[i], i + 1);
        }

        return array;
    }
}
