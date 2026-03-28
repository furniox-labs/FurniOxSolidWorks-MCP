using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Core.Extensions;
using FurniOx.SolidWorks.Core.Operations;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters;

public sealed class AssemblyBrowserOperations : OperationHandlerBase
{
    public AssemblyBrowserOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<AssemblyBrowserOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return operation switch
        {
            var op when op == AssemblyBrowserOperationNames.ListAssemblyComponents => ListAssemblyComponents(parameters),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown assembly browser operation: {operation}"))
        };
    }

    private Task<ExecutionResult> ListAssemblyComponents(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return Task.FromResult(ExecutionResult.Failure("No active document"));
        }

        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            return Task.FromResult(ExecutionResult.Failure("Active document must be an assembly"));
        }

        var assembly = (IAssemblyDoc)model;
        var topLevelOnly = GetBoolParam(parameters, "TopLevelOnly");
        var includePaths = GetBoolParam(parameters, "IncludePaths");

        var componentObjects = assembly.GetComponents(topLevelOnly).ToObjectArraySafe() ?? Array.Empty<object>();
        var components = componentObjects
            .OfType<IComponent2>()
            .Select(component => new
            {
                Name = component.Name2 ?? string.Empty,
                Path = includePaths ? component.GetPathName() : null,
                IsSuppressed = component.IsSuppressed(),
                IsVirtual = component.IsVirtual,
                SelectByIdString = $"{component.Name2}@{model.GetTitle()}"
            })
            .OrderBy(component => component.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            DocumentTitle = model.GetTitle(),
            TopLevelOnly = topLevelOnly,
            IncludePaths = includePaths,
            TotalComponents = components.Count,
            Components = components
        }));
    }
}
