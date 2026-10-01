using System.Text.Json;
using Xunit;

namespace LocalDictate.Placement.Tests;

public class AppSettingsTests
{
    [Fact]
    public void PlaySoundCues_defaults_to_false()
    {
        Assert.False(new AppSettings().PlaySoundCues);
    }

    [Fact]
    public void Missing_playSoundCues_stays_false()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"offlineOnly":true}""");
        Assert.NotNull(settings);
        Assert.False(settings!.PlaySoundCues);
    }

    [Fact]
    public void Explicit_playSoundCues_true_is_kept()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"playSoundCues":true}""");
        Assert.NotNull(settings);
        Assert.True(settings!.PlaySoundCues);
    }
}
