using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Core.Connection;
using FurniOx.SolidWorks.Shared.Configuration;
using FurniOx.SolidWorks.Shared.Models;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace FurniOx.SolidWorks.Core.Adapters.Sorting;

public sealed class SortingInspectionOperations : OperationHandlerBase
{
    public SortingInspectionOperations(
        SolidWorksConnection connection,
        SolidWorksSettings settings,
        ILogger<SortingInspectionOperations> logger)
        : base(connection, settings, logger)
    {
    }

    public override Task<ExecutionResult> ExecuteAsync(
        string operation,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(ListComponentFolders());
    }

    private ExecutionResult ListComponentFolders()
    {
        var app = _connection.Application;
        if (app == null)
        {
            return ExecutionResult.Failure("Not connected to SolidWorks");
        }

        var model = (ModelDoc2?)app.ActiveDoc;
        if (model == null)
        {
            return ExecutionResult.Failure("No active document");
        }

        if (model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
        {
            return ExecutionResult.Failure("Active document must be an assembly");
        }

        try
        {
            var items = SortingComponentSupport.GetTopLevelComponentItemsInTreeOrder(model);
            var folders = items
                .Where(item => item.IsFolder && !string.IsNullOrEmpty(item.FolderPath))
                .Select((item, index) => new SortingFolderListItem(item.DisplayName, item.FolderPath ?? item.DisplayName, index + 1))
                .ToList();

            return ExecutionResult.SuccessResult(new
            {
                TotalTopLevelItems = items.Count,
                FolderCount = folders.Count,
                Folders = folders
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list component folders");
            return ExecutionResult.Failure($"Failed to list component folders: {ex.Message}");
        }
    }
}
