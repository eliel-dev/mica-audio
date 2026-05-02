using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Device.Protocol.Contracts;
using Device.Client.Remote;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class PanelRuntimeApiTests
{
    private const string AdminToken = "panel-runtime-test-token";

    [Fact]
    public async Task AdminPanelRuntime_ShouldRoundTripStateAndReturnStatus()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var state = new PanelRuntimeStateDocument
        {
            Enabled = true,
            PanelId = "panel-clock",
            TargetDeviceId = "device-clock",
            UpdatedAtUtc = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero),
        };

        using var putRequest = CreateAdminRequest(HttpMethod.Put, "/api/v1/admin/panels/runtime");
        putRequest.Content = JsonContent.Create(state);
        using var putResponse = await client.SendAsync(putRequest);
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        using var getRequest = CreateAdminRequest(HttpMethod.Get, "/api/v1/admin/panels/runtime");
        using var getResponse = await client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();
        var loaded = await getResponse.Content.ReadFromJsonAsync<PanelRuntimeStateDocument>();
        Assert.NotNull(loaded);
        Assert.True(loaded!.Enabled);
        Assert.Equal("panel-clock", loaded.PanelId);
        Assert.Equal("device-clock", loaded.TargetDeviceId);

        var status = await host.GetPanelRuntimeStatusAsync();
        Assert.False(status.Running);

        using var statusRequest = CreateAdminRequest(HttpMethod.Get, "/api/v1/admin/panels/runtime/status");
        using var statusResponse = await client.SendAsync(statusRequest);
        statusResponse.EnsureSuccessStatusCode();
        var loadedStatus = await statusResponse.Content.ReadFromJsonAsync<PanelRuntimeStatusDocument>();
        Assert.NotNull(loadedStatus);
        Assert.False(loadedStatus!.Running);
    }

    [Fact]
    public async Task RemoteDeviceServerClient_ShouldRoundTripPanelRuntime()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        await using var remote = new RemoteDeviceServerClient(
            httpClient,
            new RemoteDeviceServerClientOptions
            {
                BaseAddress = $"http://127.0.0.1:{port}",
                AdminToken = AdminToken,
            });

        await remote.SavePanelRuntimeStateAsync(new PanelRuntimeStateDocument
        {
            Enabled = true,
            PanelId = "panel-weather",
            TargetDeviceId = "device-weather",
        });

        var state = await remote.GetPanelRuntimeStateAsync();
        Assert.True(state.Enabled);
        Assert.Equal("panel-weather", state.PanelId);
        Assert.Equal("device-weather", state.TargetDeviceId);

        var status = await remote.GetPanelRuntimeStatusAsync();
        Assert.False(status.Running);
        Assert.Null(status.PanelId);
    }

    private static HttpRequestMessage CreateAdminRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AdminToken);
        return request;
    }
}
