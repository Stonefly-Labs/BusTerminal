using BusTerminal.Api.Features.Health;
using BusTerminal.Api.Infrastructure.Authentication;
using BusTerminal.Api.Infrastructure.Configuration;
using BusTerminal.Api.Infrastructure.Observability;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddBusTerminalKeyVault(builder.Environment);

builder.AddBusTerminalTelemetry();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

builder.Services.AddBusTerminalAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddBusTerminalHealthChecks(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddRouting();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    // Reserved for future Swagger UI / API explorer wiring.
}

app.UseAuthentication();
app.UseAuthorization();

app.MapBusTerminalHealthEndpoints();

try
{
    Log.Information("Starting BusTerminal API on port 8080 ({Environment})", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
