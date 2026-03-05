using App.Headless;
using App.Headless.Services;
using Device.Server.Hosting;
using Output.Led;

// DOCS: docs/wiki/modules/app-headless.md#programcs
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://127.0.0.1:5175");

// Device server host (ESP32 protocol on 0.0.0.0:5174)
builder.Services.AddSingleton<DeviceServerHost>();
builder.Services.AddSingleton<IDeviceServerHost>(sp => sp.GetRequiredService<DeviceServerHost>());

// LED outputs
builder.Services.AddSingleton<SimulatorLedOutput>();
builder.Services.AddSingleton<Esp32S3LedOutput>();
builder.Services.AddSingleton<NullLedOutput>();

// Device registry (DPAPI for token, separate from WinUI)
builder.Services.AddSingleton<HeadlessDeviceRegistryStore>();

// Audio pipeline
builder.Services.AddSingleton<HeadlessVisualizerSettingsStore>();
builder.Services.AddSingleton<HeadlessVisualizerSettingsDomainService>();
builder.Services.AddSingleton<HeadlessHub75SessionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HeadlessHub75SessionService>());
builder.Services.AddSingleton<HeadlessAudioPipeline>();

// Hosted services
builder.Services.AddHostedService<DeviceServerService>();
builder.Services.AddHostedService<AudioPipelineHostedService>();
builder.Services.AddSingleton<DeviceStateService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DeviceStateService>());

// Preview frame broadcaster
builder.Services.AddSingleton<PreviewBroadcaster>();
builder.Services.AddSingleton<IUiRealtimePublisher>(sp => sp.GetRequiredService<PreviewBroadcaster>());

// Onboarding (USB + esptool)
builder.Services.AddSingleton<ISerialPortCatalogService, SerialPortCatalogService>();
builder.Services.AddSingleton<IEspToolFlashService, EspToolFlashService>();
builder.Services.AddSingleton<IFirmwareBinaryResolver, FirmwareBinaryResolver>();
builder.Services.AddSingleton<IUsbOnboardingService, UsbOnboardingService>();

var app = builder.Build();

// Serve static files from Web.Headless/dist
var webDistPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
if (!Directory.Exists(webDistPath))
{
    // Fallback: dev-time path relative to project
    var devPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Web.Headless", "dist"));
    if (Directory.Exists(devPath))
    {
        webDistPath = devPath;
    }
}

if (Directory.Exists(webDistPath))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webDistPath),
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webDistPath),
    });
}

// WebSocket support
app.UseWebSockets();

// Map REST API and WebSocket endpoints
app.MapUiApiEndpoints();

// SPA fallback
app.MapFallback(async context =>
{
    var indexPath = Path.Combine(webDistPath, "index.html");
    if (File.Exists(indexPath))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(indexPath);
    }
    else
    {
        context.Response.StatusCode = 404;
    }
});

app.Run();
