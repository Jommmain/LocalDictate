using LocalDictate.Services;
using Xunit;

namespace LocalDictate.Placement.Tests;

public class ReleaseChannelTests
{
    [Fact]
    public void StableChannel_SkipsPrereleaseAndPicksNewerZip()
    {
        const string json = """
        [
          {
            "tag_name": "v0.3.0-rc.1",
            "draft": false,
            "prerelease": true,
            "assets": [
              {"name": "LocalDictate-v0.3.0-rc.1.zip", "browser_download_url": "https://example.com/rc.zip", "digest": "sha256:aa", "size": 10}
            ]
          },
          {
            "tag_name": "v0.2.5",
            "draft": false,
            "prerelease": false,
            "assets": [
              {"name": "LocalDictate-v0.2.5.zip", "browser_download_url": "https://example.com/025.zip", "digest": "sha256:bb", "size": 20}
            ]
          },
          {
            "tag_name": "v0.2.7",
            "draft": false,
            "prerelease": false,
            "assets": [
              {"name": "notes.txt", "browser_download_url": "https://example.com/n.txt", "digest": "sha256:cc", "size": 1},
              {"name": "LocalDictate-v0.2.7.zip", "browser_download_url": "https://example.com/027.zip", "digest": "sha256:ABCDEF", "size": 30}
            ]
          }
        ]
        """;

        var chosen = ReleaseChannel.ChooseNewer(ReleaseChannel.ParseReleases(json), new Version(0, 2, 6));

        Assert.NotNull(chosen);
        Assert.Equal("v0.2.7", chosen!.Tag);
        Assert.Equal("LocalDictate-v0.2.7.zip", chosen.Asset.Name);
        Assert.True(ReleaseChannel.DigestMatches(chosen.Asset.Digest, "abcdef"));
    }

    [Fact]
    public void SameStableVersion_IsNotAnUpgrade()
    {
        var releases = new[]
        {
            new ReleaseInfo("v0.2.6", false, false, new[]
            {
                new ReleaseAsset("LocalDictate-v0.2.6.zip", "https://example.com/a.zip", "sha256:aa", 1),
            }),
        };

        Assert.Null(ReleaseChannel.ChooseNewer(releases, ReleaseChannel.Normalize(new Version(0, 2, 6, 0))));
    }

    [Fact]
    public void MissingDigest_DoesNotMatch()
    {
        Assert.False(ReleaseChannel.DigestMatches(null, "aa"));
        Assert.False(ReleaseChannel.DigestMatches("sha256:", "aa"));
    }

    [Fact]
    public void InstallDir_MustNotContainUserData()
    {
        var root = Path.Combine(Path.GetTempPath(), "ld-update-tests");
        var data = Path.Combine(root, "LocalDictate");
        var programs = Path.Combine(root, "Programs", "LocalDictate");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(programs);

        Assert.True(ReleaseChannel.IsSafeInstallDirectory(programs, data));
        Assert.False(ReleaseChannel.IsSafeInstallDirectory(data, data));
        Assert.False(ReleaseChannel.IsSafeInstallDirectory(root, data));
    }

    [Fact]
    public void PayloadRoot_AcceptsFlatOrSingleFolderZip()
    {
        var root = Path.Combine(Path.GetTempPath(), "ld-payload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "LocalDictate.exe"), "exe");
        Assert.Equal(root, ReleaseChannel.ResolvePayloadRoot(root));

        var wrapped = Path.Combine(Path.GetTempPath(), "ld-wrapped-" + Guid.NewGuid().ToString("N"));
        var inner = Path.Combine(wrapped, "LocalDictate");
        Directory.CreateDirectory(inner);
        File.WriteAllText(Path.Combine(inner, "LocalDictate.exe"), "exe");
        Assert.Equal(inner, ReleaseChannel.ResolvePayloadRoot(wrapped));
    }

    [Fact]
    public void ApplyScript_MirrorsWholeFolderAfterProcessExits()
    {
        var script = ReleaseChannel.BuildApplyScript(4242, @"C:\Apps\staged", @"C:\Users\A\Programs\LocalDictate", @"C:\Users\A\Programs\LocalDictate\LocalDictate.exe");

        Assert.Contains("robocopy", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/MIR", script, StringComparison.Ordinal);
        Assert.Contains("4242", script, StringComparison.Ordinal);
        Assert.Contains("LocalDictate.exe", script, StringComparison.Ordinal);
        Assert.DoesNotContain("%LocalAppData%\\LocalDictate\\models", script, StringComparison.Ordinal);
    }
}
