using Microsoft.Extensions.Hosting;

namespace FurniOx.SolidWorks.MCP;

internal class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        var host = Host.CreateApplicationBuilder(args);
        host.AddSolidWorksJsonConfiguration();
        host.AddSolidWorksSerilog();
        host.Services.AddSolidWorksPublicServices(host.Configuration);
        host.Services.AddSolidWorksPublicMcp();

        var app = host.Build();
        SolidWorksPublicHostingExtensions.ValidatePublicConfiguration(app.Services);
        await app.RunAsync();
    }
}
