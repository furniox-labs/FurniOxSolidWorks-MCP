using System;
using System.Collections.Generic;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Analysis;

internal static class AnalysisExtractionSupport
{
    public static Dictionary<string, string> ExtractCustomProperties(ModelDoc2 model, ILogger? logger)
    {
        var properties = new Dictionary<string, string>();

        try
        {
            var customPropertyManager = model.Extension.CustomPropertyManager[""];
            if (customPropertyManager == null)
            {
                return properties;
            }

            var propertyNames = customPropertyManager.GetNames().ToStringArraySafe();
            if (propertyNames.Length == 0)
            {
                return properties;
            }

            foreach (var propertyName in propertyNames)
            {
                string valueOut = string.Empty;
                string resolvedValueOut = string.Empty;
                var wasResolved = false;
                var linkToProperty = false;

                var result = customPropertyManager.Get6(
                    propertyName,
                    false,
                    out valueOut,
                    out resolvedValueOut,
                    out wasResolved,
                    out linkToProperty);

                if (result != 1)
                {
                    properties[propertyName] = resolvedValueOut ?? valueOut;
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to extract custom properties");
        }

        return properties;
    }

    public static DocumentSummaryInfo? ExtractSummaryInfo(ModelDoc2 model, ILogger? logger)
    {
        try
        {
            return new DocumentSummaryInfo
            {
                Title = model.SummaryInfo[(int)swSummInfoField_e.swSumInfoTitle] ?? string.Empty,
                Subject = model.SummaryInfo[(int)swSummInfoField_e.swSumInfoSubject] ?? string.Empty,
                Author = model.SummaryInfo[(int)swSummInfoField_e.swSumInfoAuthor] ?? string.Empty,
                Keywords = model.SummaryInfo[(int)swSummInfoField_e.swSumInfoKeywords] ?? string.Empty,
                Comments = model.SummaryInfo[(int)swSummInfoField_e.swSumInfoComment] ?? string.Empty,
                SavedBy = model.SummaryInfo[(int)swSummInfoField_e.swSumInfoSavedBy] ?? string.Empty,
                CreateDate = model.SummaryInfo[(int)swSummInfoField_e.swSumInfoCreateDate2] ?? string.Empty,
                SaveDate = model.SummaryInfo[(int)swSummInfoField_e.swSumInfoSaveDate2] ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to extract summary info");
            return null;
        }
    }

    public static Dictionary<string, Dictionary<string, string>> ExtractConfigurationCustomProperties(
        ModelDoc2 model, ILogger? logger)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();

        try
        {
            var configNames = model.GetConfigurationNames().ToStringArraySafe();
            foreach (var configName in configNames)
            {
                if (string.IsNullOrEmpty(configName))
                {
                    continue;
                }

                var propMgr = (ICustomPropertyManager?)model.Extension.CustomPropertyManager[configName];
                if (propMgr == null)
                {
                    continue;
                }

                var names = propMgr.GetNames().ToStringArraySafe();
                if (names.Length == 0)
                {
                    continue;
                }

                var configProps = new Dictionary<string, string>();
                foreach (var name in names)
                {
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    string valOut = string.Empty, resolvedValOut = string.Empty;
                    var wasResolved = false;
                    var linkToProperty = false;
                    var getResult = propMgr.Get6(
                        name,
                        false,
                        out valOut,
                        out resolvedValOut,
                        out wasResolved,
                        out linkToProperty);

                    if (getResult != 1)
                    {
                        configProps[name] = resolvedValOut ?? valOut;
                    }
                }

                if (configProps.Count > 0)
                {
                    result[configName] = configProps;
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to extract configuration custom properties");
        }

        return result;
    }

    public static List<PartFeature> ExtractFeatures(ModelDoc2 model)
    {
        var features = new List<PartFeature>();
        var feature = model.FirstFeature() as IFeature;
        var activeConfiguration = model.ConfigurationManager.ActiveConfiguration.Name;
        string[] configurationNames = [activeConfiguration];

        while (feature != null)
        {
            var featureType = feature.GetTypeName2();
            var suppressionObject = feature.IsSuppressed2(1, configurationNames);
            var suppressedStates = (bool[])suppressionObject;
            var suppressed = suppressedStates.Length > 0 && suppressedStates[0];
            var errorCode = feature.GetErrorCode2(out _);

            features.Add(new PartFeature
            {
                Name = feature.Name,
                Type = featureType,
                TypeName = AnalysisNamingSupport.GetFeatureTypeName(featureType),
                Suppressed = suppressed,
                ErrorState = errorCode,
                ChildFeatureCount = feature.IGetChildCount()
            });

            feature = feature.GetNextFeature() as IFeature;
        }

        return features;
    }

    public static List<PartFeature> ExtractFeaturesMinimal(ModelDoc2 model)
    {
        var features = new List<PartFeature>();
        var feature = model.FirstFeature() as IFeature;

        while (feature != null)
        {
            var featureType = feature.GetTypeName2();
            features.Add(new PartFeature
            {
                Name = feature.Name,
                Type = featureType,
                TypeName = AnalysisNamingSupport.GetFeatureTypeName(featureType)
            });

            feature = feature.GetNextFeature() as IFeature;
        }

        return features;
    }

    public static int CountFeatures(ModelDoc2 model)
    {
        var count = 0;
        var feature = model.FirstFeature() as IFeature;

        while (feature != null)
        {
            count++;
            feature = feature.GetNextFeature() as IFeature;
        }

        return count;
    }

    public static PartMassProperties? ExtractMassProperties(ModelDoc2 model, ILogger? logger)
    {
        try
        {
            var massProperties = model.Extension.CreateMassProperty() as IMassProperty;
            if (massProperties == null)
            {
                return null;
            }

            var centerOfMassObject = massProperties.CenterOfMass;
            var centerOfMass = centerOfMassObject.ToDoubleArraySafe();
            if (centerOfMass == null || centerOfMass.Length < 3)
            {
                return null;
            }

            var momentOfInertiaObject = massProperties.GetMomentOfInertia((int)swMassPropertyMoment_e.swMassPropertyMomentAboutCenterOfMass);
            var momentOfInertia = momentOfInertiaObject.ToDoubleArraySafe();

            const double CubicMetersToCubicMillimeters = 1000000000.0;
            const double SquareMetersToSquareMillimeters = 1000000.0;

            return new PartMassProperties
            {
                Mass = massProperties.Mass,
                Volume = massProperties.Volume * CubicMetersToCubicMillimeters,
                SurfaceArea = massProperties.SurfaceArea * SquareMetersToSquareMillimeters,
                CenterOfMass = new Point3D
                {
                    X = AnalysisTransformSupport.MetersToMm(centerOfMass[0]),
                    Y = AnalysisTransformSupport.MetersToMm(centerOfMass[1]),
                    Z = AnalysisTransformSupport.MetersToMm(centerOfMass[2])
                },
                MomentOfInertia = momentOfInertia != null && momentOfInertia.Length >= 6
                    ? new MomentOfInertia
                    {
                        Ixx = momentOfInertia[0],
                        Iyy = momentOfInertia[1],
                        Izz = momentOfInertia[2],
                        Ixy = momentOfInertia[3],
                        Iyz = momentOfInertia[4],
                        Ixz = momentOfInertia[5]
                    }
                    : null
            };
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to extract mass properties");
            return null;
        }
    }
}
