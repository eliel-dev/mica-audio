using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class DeviceServerHostLibraryApiTests
{
    private const string AdminToken = "library-admin-token";

    [Fact]
    public async Task AdminLibraryPanels_ShouldRoundTripDocument()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            AdminToken = AdminToken,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var document = new PanelLibraryDocument
        {
            LastSelectedPanelId = "panel-1",
            ActivePanels =
            [
                new PanelDeviceState
                {
                    DeviceId = "device-1",
                    ActivePanelId = "panel-1",
                    ActiveAppId = "panels-hub75",
                    LastServerOwnedPanelId = "panel-1",
                    UpdatedAtUtc = new DateTimeOffset(2026, 4, 29, 12, 0, 0, TimeSpan.Zero),
                },
            ],
            Panels =
            [
                new PanelLibraryItem
                {
                    PanelId = "panel-1",
                    Name = "Bancada",
                    Width = 128,
                    Height = 64,
                    IsEnabled = true,
                    Widgets =
                    [
                        new PanelWidgetItem
                        {
                            WidgetId = "widget-1",
                            AppId = "gifhub75",
                            DataSource = PanelWidgetDataSources.Server,
                            X = 0,
                            Y = 0,
                            Width = 128,
                            Height = 64,
                            ZIndex = 0,
                            ConfigValues = new Dictionary<string, string>
                            {
                                ["mediaId"] = "media-1",
                            },
                        },
                    ],
                },
            ],
        };

        using var putRequest = CreateAdminRequest(HttpMethod.Put, "/api/v1/admin/library/panels");
        putRequest.Content = JsonContent.Create(document);
        using var putResponse = await client.SendAsync(putRequest);
        putResponse.EnsureSuccessStatusCode();

        using var getRequest = CreateAdminRequest(HttpMethod.Get, "/api/v1/admin/library/panels");
        using var getResponse = await client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();
        var loaded = await getResponse.Content.ReadFromJsonAsync<PanelLibraryDocument>();

        Assert.NotNull(loaded);
        Assert.Equal("panel-1", loaded!.LastSelectedPanelId);
        var activePanel = Assert.Single(loaded.ActivePanels);
        Assert.Equal("device-1", activePanel.DeviceId);
        Assert.Equal("panel-1", activePanel.ActivePanelId);
        Assert.Equal("panels-hub75", activePanel.ActiveAppId);
        Assert.Equal("panel-1", activePanel.LastServerOwnedPanelId);
        var panel = Assert.Single(loaded.Panels);
        Assert.Equal("Bancada", panel.Name);
        var widget = Assert.Single(panel.Widgets);
        Assert.Equal("gifhub75", widget.AppId);
        Assert.Equal(PanelWidgetDataSources.Server, widget.DataSource);
        Assert.Equal("media-1", widget.ConfigValues["mediaId"]);
    }

    [Fact]
    public async Task AdminLibraryMedia_ShouldUploadDeduplicateDownloadAndDelete()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            AdminToken = AdminToken,
            MaxMediaUploadBytes = 1024,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var payload = "GIF89a-test"u8.ToArray();

        var first = await UploadAsync(client, "loop.gif", "image/gif", payload);
        var second = await UploadAsync(client, "copy.gif", "image/gif", payload);

        Assert.Equal(first.MediaId, second.MediaId);
        Assert.Equal(payload.LongLength, first.SizeBytes);
        Assert.Equal("image/gif", first.ContentType);
        Assert.Equal(".gif", first.Extension);

        using var downloadRequest = CreateAdminRequest(HttpMethod.Get, $"/api/v1/admin/library/media/{first.MediaId}");
        using var downloadResponse = await client.SendAsync(downloadRequest);
        downloadResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/gif", downloadResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(payload, await downloadResponse.Content.ReadAsByteArrayAsync());

        using var deleteRequest = CreateAdminRequest(HttpMethod.Delete, $"/api/v1/admin/library/media/{first.MediaId}");
        using var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var missingRequest = CreateAdminRequest(HttpMethod.Get, $"/api/v1/admin/library/media/{first.MediaId}");
        using var missingResponse = await client.SendAsync(missingRequest);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    private static async Task<MediaAssetInfo> UploadAsync(HttpClient client, string fileName, string contentType, byte[] payload)
    {
        using var request = CreateAdminRequest(
            HttpMethod.Post,
            $"/api/v1/admin/library/media?fileName={Uri.EscapeDataString(fileName)}");
        request.Content = new ByteArrayContent(payload);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var media = await response.Content.ReadFromJsonAsync<MediaAssetInfo>();
        Assert.NotNull(media);
        return media!;
    }

    private static HttpRequestMessage CreateAdminRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AdminToken);
        return request;
    }
}
