using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Shared.Models;

namespace FurniOx.SolidWorks.Core.Interfaces;

public interface ISolidWorksAdapter
{
    Task<ExecutionResult> ExecuteAsync(string operation, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default);
    bool CanHandle(string operation);
}
