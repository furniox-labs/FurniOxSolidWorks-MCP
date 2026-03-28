using FurniOx.SolidWorks.Core.Intelligence;
using Microsoft.Extensions.Logging.Abstractions;

namespace FurniOx.SolidWorks.Core.Tests;

internal static class PerformanceMonitorTestSupport
{
    public static PerformanceMonitor CreateMonitor()
    {
        return new PerformanceMonitor(NullLogger<PerformanceMonitor>.Instance);
    }
}
