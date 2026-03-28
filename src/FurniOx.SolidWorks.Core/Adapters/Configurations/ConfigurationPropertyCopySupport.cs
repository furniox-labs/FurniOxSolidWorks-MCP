using System;
using FurniOx.SolidWorks.Core.Extensions;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Configurations;

internal static class ConfigurationPropertyCopySupport
{
    public static int CopyCustomProperties(
        ModelDoc2 model,
        string sourceConfigurationName,
        string targetConfigurationName,
        ILogger logger)
    {
        try
        {
            var sourcePropertyManager = (ICustomPropertyManager?)model.Extension.CustomPropertyManager[sourceConfigurationName];
            var targetPropertyManager = (ICustomPropertyManager?)model.Extension.CustomPropertyManager[targetConfigurationName];
            if (sourcePropertyManager == null || targetPropertyManager == null)
            {
                return 0;
            }

            object? propertyNamesObject = null;
            object? propertyTypesObject = null;
            object? propertyValuesObject = null;
            sourcePropertyManager.GetAll(ref propertyNamesObject, ref propertyTypesObject, ref propertyValuesObject);

            var propertyNames = propertyNamesObject.ToObjectArraySafe();
            var propertyTypes = propertyTypesObject.ToObjectArraySafe();
            var propertyValues = propertyValuesObject.ToObjectArraySafe();
            if (propertyNames == null || propertyTypes == null || propertyValues == null)
            {
                return 0;
            }

            var copiedCount = 0;
            for (var i = 0; i < propertyNames.Length; i++)
            {
                var propertyName = propertyNames[i]?.ToString();
                var propertyValue = propertyValues[i]?.ToString();
                if (string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(propertyValue))
                {
                    continue;
                }

                var propertyType = Convert.ToInt32(propertyTypes[i]);
                targetPropertyManager.Add3(
                    propertyName,
                    propertyType,
                    propertyValue,
                    (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                copiedCount++;
            }

            return copiedCount;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to copy custom properties from '{SourceConfiguration}' to '{TargetConfiguration}'",
                sourceConfigurationName,
                targetConfigurationName);
            return 0;
        }
    }
}
