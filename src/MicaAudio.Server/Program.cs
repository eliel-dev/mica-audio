using MicaAudio.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables(MicaAudioServerOptions.EnvironmentVariablePrefix);
MicaAudioServerBootstrap.ConfigureServices(builder.Services, builder.Configuration);

using var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
