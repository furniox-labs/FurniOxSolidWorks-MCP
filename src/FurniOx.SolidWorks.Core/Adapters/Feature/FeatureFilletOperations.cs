using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;

namespace FurniOx.SolidWorks.Core.Adapters.Features;

public sealed class FeatureFilletOperations : OperationHandlerBase
{
    public FeatureFilletOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<FeatureFilletOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        var app = _connection.Application;
        if (app == null) return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null) return Task.FromResult(ExecutionResult.Failure("No active document"));

        var modelExt = model.Extension;
        if (modelExt == null) return Task.FromResult(ExecutionResult.Failure("Failed to get model extension"));

        var radius = GetDoubleParam(parameters, "Radius", 2.0);
        var filletType = GetIntParam(parameters, "Type", 0);
        var asymmetricRadius = GetDoubleParam(parameters, "AsymmetricRadius", 2.0);
        var rho = GetDoubleParam(parameters, "Rho", 0.5);
        var options = GetIntParam(parameters, "Options", 195);
        var overflowType = GetIntParam(parameters, "OverflowType", 0);
        var profileType = GetIntParam(parameters, "ProfileType", 0);
        var edgeNames = FeatureSupport.GetStringArrayParam(parameters, "EdgeNames");
        var faceSet1Names = FeatureSupport.GetStringArrayParam(parameters, "FaceSet1Names");
        var faceSet2Names = FeatureSupport.GetStringArrayParam(parameters, "FaceSet2Names");

        if (radius <= 0)
        {
            return Task.FromResult(ExecutionResult.Failure("Radius must be greater than 0"));
        }

        if (filletType < 0 || filletType > 3)
        {
            return Task.FromResult(ExecutionResult.Failure("FilletType must be 0-3 (ConstantRadius, VariableRadius, FaceFillet, FullRound)"));
        }

        if (profileType != 0 && (rho < 0.05 || rho > 0.95))
        {
            return Task.FromResult(ExecutionResult.Failure("Rho must be between 0.05 and 0.95 for conic profiles"));
        }

        if (filletType == 0 && (edgeNames == null || edgeNames.Length == 0))
        {
            return Task.FromResult(ExecutionResult.Failure("EdgeNames is required for constant radius edge fillets"));
        }

        if (filletType == 2)
        {
            if (faceSet1Names == null || faceSet1Names.Length == 0)
            {
                return Task.FromResult(ExecutionResult.Failure("FaceSet1Names is required for face fillets"));
            }

            if (faceSet2Names == null || faceSet2Names.Length == 0)
            {
                return Task.FromResult(ExecutionResult.Failure("FaceSet2Names is required for face fillets"));
            }
        }

        var primaryRadius = MmToMeters(radius);
        var secondaryRadius = MmToMeters(asymmetricRadius);

        try
        {
            model.ClearSelection2(true);

            if (filletType == 0 || filletType == 1)
            {
                if (edgeNames != null)
                {
                    for (var i = 0; i < edgeNames.Length; i++)
                    {
                        var selected = modelExt.SelectByID2(edgeNames[i], "EDGE", 0, 0, 0, i > 0, -1, null, 0);
                        if (!selected)
                        {
                            return Task.FromResult(ExecutionResult.Failure($"Failed to select edge: {edgeNames[i]}"));
                        }
                    }
                }
            }

            if (filletType == 2)
            {
                if (faceSet1Names != null)
                {
                    for (var i = 0; i < faceSet1Names.Length; i++)
                    {
                        var append = i > 0 || (edgeNames != null && edgeNames.Length > 0);
                        var selected = modelExt.SelectByID2(faceSet1Names[i], "FACE", 0, 0, 0, append, 2, null, 0);
                        if (!selected)
                        {
                            return Task.FromResult(ExecutionResult.Failure($"Failed to select face set 1: {faceSet1Names[i]}"));
                        }
                    }
                }

                if (faceSet2Names != null)
                {
                    for (var i = 0; i < faceSet2Names.Length; i++)
                    {
                        var selected = modelExt.SelectByID2(faceSet2Names[i], "FACE", 0, 0, 0, true, 4, null, 0);
                        if (!selected)
                        {
                            return Task.FromResult(ExecutionResult.Failure($"Failed to select face set 2: {faceSet2Names[i]}"));
                        }
                    }
                }
            }

            const double radiiInit = 0.0;
            const double dist2Init = 0.0;
            const double rhoInit = 0.0;
            const double setbackInit = 0.0;
            const double pointRadiusInit = 0.0;
            const double pointDist2Init = 0.0;
            const double pointRhoInit = 0.0;

            var feature = (Feature?)model.FeatureManager.FeatureFillet3(
                options,
                primaryRadius,
                secondaryRadius,
                rho,
                filletType,
                overflowType,
                profileType,
                radiiInit,
                dist2Init,
                rhoInit,
                setbackInit,
                pointRadiusInit,
                pointDist2Init,
                pointRhoInit);

            if (feature == null)
            {
                return Task.FromResult(ExecutionResult.Failure(
                    "Failed to create fillet feature. Common causes: radius too large for the geometry, self-intersection, missing edge references, or invalid face-fillet selection marks."));
            }

            if (string.IsNullOrEmpty(feature.Name))
            {
                return Task.FromResult(ExecutionResult.Failure(
                    "Fillet feature was created but appears invalid (empty name). This may indicate geometry errors."));
            }

            var rebuildResult = model.ForceRebuild3(false);
            if (!rebuildResult)
            {
                _logger.LogWarning("Fillet feature created but rebuild returned warnings. Feature may have errors.");
            }

            var errorCode = feature.GetErrorCode();
            if (errorCode != 0)
            {
                return Task.FromResult(ExecutionResult.Failure(
                    $"Fillet feature created but has error code {errorCode}. The feature may be suppressed or have geometric issues."));
            }

            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                FeatureName = feature.Name,
                FeatureType = "Fillet",
                FilletSubtype = ((FilletType)filletType).ToString(),
                Parameters = new
                {
                    Radius = radius,
                    FilletType = ((FilletType)filletType).ToString(),
                    Options = options,
                    OptionsFlagsSet = FeatureSupport.GetFilletOptionsDescription(options),
                    OverflowType = ((FilletOverflowType)overflowType).ToString(),
                    ProfileType = ((FilletProfileType)profileType).ToString(),
                    AsymmetricRadius = (options & 0x4000) != 0 ? (double?)asymmetricRadius : null,
                    Rho = profileType != 0 ? (double?)rho : null,
                    EdgeCount = edgeNames?.Length ?? 0,
                    FaceSet1Count = faceSet1Names?.Length ?? 0,
                    FaceSet2Count = faceSet2Names?.Length ?? 0
                }
            }));
        }
        finally
        {
            model.ClearSelection2(true);
        }
    }
}

