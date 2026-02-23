using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace Output.Tests;

public sealed class AppModifierStateStoreTests
{
    [Fact]
    public async Task SetAndGetDraftAsync_ShouldPersistByDeviceAndAppScope()
    {
        var root = CreateTempRoot();
        try
        {
            var options = CreateOptions(root);
            var store = new AppModifierStateStore(options);

            await store.SetDraftAsync(
                "device-a",
                "accuweather",
                new AppConfigDraft
                {
                    Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["city"] = "Brasília",
                        ["units"] = "metric",
                    },
                });

            var draft = await store.GetDraftAsync("device-a", "accuweather");
            Assert.NotNull(draft);
            Assert.Equal("Brasília", draft!.Values["city"]);
            Assert.Equal("metric", draft.Values["units"]);

            var otherScope = await store.GetDraftAsync("device-b", "accuweather");
            Assert.Null(otherScope);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ShouldReadPersistedDrafts()
    {
        var root = CreateTempRoot();
        try
        {
            var options = CreateOptions(root);
            var store = new AppModifierStateStore(options);

            await store.SetDraftAsync(
                "device-x",
                "analogclock",
                new AppConfigDraft
                {
                    Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["format24h"] = "true",
                        ["fontColor"] = "cyan",
                    },
                });

            var reloaded = new AppModifierStateStore(options);
            await reloaded.LoadAsync();
            var draft = await reloaded.GetDraftAsync("device-x", "analogclock");

            Assert.NotNull(draft);
            Assert.Equal("true", draft!.Values["format24h"]);
            Assert.Equal("cyan", draft.Values["fontColor"]);

            await reloaded.ClearDraftAsync("device-x", "analogclock");
            Assert.Null(await reloaded.GetDraftAsync("device-x", "analogclock"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static IOptions<MicaAudioOptions> CreateOptions(string root)
    {
        return Options.Create(new MicaAudioOptions
        {
            AppDataRoot = root,
            AppsModifierStatePath = Path.Combine(root, "apps", "modifiers.json"),
        });
    }
}
