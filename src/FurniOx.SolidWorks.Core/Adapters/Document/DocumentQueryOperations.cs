using System;
using System.Collections.Generic;
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

namespace FurniOx.SolidWorks.Core.Adapters.Document;

public sealed class DocumentQueryOperations : OperationHandlerBase
{
    public DocumentQueryOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<DocumentQueryOperations> logger)
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
            var op when op == DocumentOperationNames.GetDocumentInfo => GetDocumentInfoAsync(),
            var op when op == DocumentOperationNames.GetAllOpenDocuments => GetAllOpenDocumentsAsync(parameters),
            var op when op == DocumentOperationNames.GetDocumentCount => GetDocumentCountAsync(),
            _ => Task.FromResult(ExecutionResult.Failure($"Unknown document query operation: {operation}"))
        };
    }

    private Task<ExecutionResult> GetDocumentInfoAsync()
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

        var lengthUnit = 0;
        try
        {
            var units = model.GetUnits().ToObjectArraySafe();
            if (units != null && units.Length > 0)
            {
                var unitValue = units[0];
                lengthUnit = unitValue switch
                {
                    int intValue => intValue,
                    long longValue => (int)longValue,
                    double doubleValue => (int)doubleValue,
                    _ => Convert.ToInt32(unitValue)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get units, defaulting to Millimeters");
            lengthUnit = 2;
        }

        var lengthUnitName = lengthUnit switch
        {
            0 => "Meters",
            1 => "Centimeters",
            2 => "Millimeters",
            3 => "Inches",
            4 => "Feet",
            5 => "Feet and inches",
            _ => "Unknown"
        };

        var docType = ((IModelDoc2)model).GetType();
        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Path = model.GetPathName(),
            Title = model.GetTitle(),
            Type = docType,
            TypeName = ((swDocumentTypes_e)docType).ToString(),
            HasUnsavedChanges = model.GetSaveFlag(),
            LengthUnit = lengthUnit,
            LengthUnitName = lengthUnitName
        }));
    }

    private Task<ExecutionResult> GetAllOpenDocumentsAsync(IDictionary<string, object?> parameters)
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var visibleOnly = GetBoolParam(parameters, "VisibleOnly", false);
        var documents = app.GetDocuments().ToObjectArraySafe();

        if (documents == null || documents.Length == 0)
        {
            return Task.FromResult(ExecutionResult.SuccessResult(new
            {
                Count = 0,
                Documents = Array.Empty<object>(),
                VisibleOnly = visibleOnly
            }));
        }

        var documentList = new List<object>();
        foreach (var docObj in documents)
        {
            if (docObj is not ModelDoc2 doc)
            {
                continue;
            }

            var isVisible = doc.Visible;
            if (visibleOnly && !isVisible)
            {
                continue;
            }

            var docType = ((IModelDoc2)doc).GetType();
            documentList.Add(new
            {
                Title = doc.GetTitle(),
                Path = doc.GetPathName(),
                Type = docType,
                TypeName = ((swDocumentTypes_e)docType).ToString(),
                HasUnsavedChanges = doc.GetSaveFlag(),
                Visible = isVisible
            });
        }

        return Task.FromResult(ExecutionResult.SuccessResult(new
        {
            Count = documentList.Count,
            Documents = documentList,
            VisibleOnly = visibleOnly
        }));
    }

    private Task<ExecutionResult> GetDocumentCountAsync()
    {
        var app = _connection.Application;
        if (app == null)
        {
            return Task.FromResult(ExecutionResult.Failure("Not connected to SolidWorks"));
        }

        var count = app.GetDocumentCount();
        return Task.FromResult(ExecutionResult.SuccessResult(new { Count = count }));
    }
}
