using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LocalDictate.Services;

/// <summary>
/// Stable GitHub Releases channel: tags vMAJOR.MINOR.PATCH only.
/// Prereleases, drafts, and non-semver tags are ignored.
/// </summary>
public static class ReleaseChannel
{
    public const string Repository = "Jommmain/LocalDictate";

    public static string ReleasesUrl =>
        $"https://api.github.com/repos/{Repository}/releases?per_page=20";

    public static Version CurrentVersion()
    {
        var version = typeof(ReleaseChannel).Assembly.GetName().Version ?? new Version(0, 0, 0);
        return Normalize(version);
    }

    public static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0));

    public static Version? ParseStableTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        if (!Regex.IsMatch(text, @"^\d+\.\d+\.\d+$"))
        {
            return null;
        }

        return Version.Parse(text);
    }

    public static IReadOnlyList<ReleaseInfo> ParseReleases(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ReleaseInfo>();
        }

        var list = new List<ReleaseInfo>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var assets = new List<ReleaseAsset>();
            if (item.TryGetProperty("assets", out var assetArray) && assetArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetArray.EnumerateArray())
                {
                    assets.Add(new ReleaseAsset(
                        Name: ReadString(asset, "name"),
                        DownloadUrl: ReadString(asset, "browser_download_url"),
                        Digest: asset.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String
                            ? digest.GetString()
                            : null,
                        Size: asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : 0));
                }
            }

            list.Add(new ReleaseInfo(
                Tag: ReadString(item, "tag_name"),
                Draft: item.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True,
                Prerelease: item.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True,
                Assets: assets));
        }

        return list;
    }

    public static ReleaseOffer? ChooseNewer(IEnumerable<ReleaseInfo> releases, Version current)
    {
        ReleaseOffer? best = null;
        foreach (var release in releases)
        {
            if (release.Draft || release.Prerelease)
            {
                continue;
            }

            var version = ParseStableTag(release.Tag);
            if (version is null || version <= current)
            {
                continue;
            }

            var asset = PickWindowsZip(release.Assets, release.Tag);
            if (asset is null)
            {
                continue;
            }

            if (best is null || version > best.Version)
            {
                best = new ReleaseOffer(version, release.Tag, asset);
            }
        }

        return best;
    }

    public static ReleaseAsset? PickWindowsZip(IReadOnlyList<ReleaseAsset> assets, string tag)
    {
        var zips = assets
            .Where(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                && asset.DownloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                && asset.Name.Contains("source", StringComparison.OrdinalIgnoreCase) == false)
            .ToList();
        if (zips.Count == 0)
        {
            return null;
        }

        return zips.FirstOrDefault(asset => asset.Name.Contains(tag, StringComparison.OrdinalIgnoreCase))
            ?? zips.FirstOrDefault(asset => asset.Name.Contains("win", StringComparison.OrdinalIgnoreCase))
            ?? zips.OrderByDescending(asset => asset.Size).First();
    }

    public static bool DigestMatches(string? digest, string sha256Hex)
    {
        if (string.IsNullOrWhiteSpace(digest) || string.IsNullOrWhiteSpace(sha256Hex))
        {
            return false;
        }

        const string prefix = "sha256:";
        var hex = digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? digest[prefix.Length..]
            : digest;
        return hex.Equals(sha256Hex.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSafeInstallDirectory(string installDir, string dataRoot)
    {
        var install = TrimPath(Path.GetFullPath(installDir));
        var data = TrimPath(Path.GetFullPath(dataRoot));
        if (install.Equals(data, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var installPrefix = install + Path.DirectorySeparatorChar;
        return !data.StartsWith(installPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDevOutput(string installDir)
    {
        var full = Path.GetFullPath(installDir);
        return full.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || full.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolvePayloadRoot(string extractedDir)
    {
        if (File.Exists(Path.Combine(extractedDir, "LocalDictate.exe")))
        {
            return extractedDir;
        }

        var children = Directory.GetDirectories(extractedDir);
        if (children.Length == 1 && File.Exists(Path.Combine(children[0], "LocalDictate.exe")))
        {
            return children[0];
        }

        return extractedDir;
    }

    public static bool PayloadHasRuntime(string payloadRoot)
    {
        if (!File.Exists(Path.Combine(payloadRoot, "LocalDictate.exe")))
        {
            return false;
        }

        return Directory.EnumerateFiles(payloadRoot, "whisper.dll", SearchOption.AllDirectories).Any()
            || Directory.EnumerateFiles(payloadRoot, "ggml-whisper.dll", SearchOption.AllDirectories).Any();
    }

    public static string BuildApplyScript(int pid, string source, string target, string exe)
    {
        RejectUnsafePath(source);
        RejectUnsafePath(target);
        RejectUnsafePath(exe);
        if (pid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pid));
        }

        return string.Join("\r\n", new[]
        {
            "@echo off",
            "setlocal",
            "chcp 65001 >nul",
            $"set PID={pid.ToString(CultureInfo.InvariantCulture)}",
            $"set SOURCE={source}",
            $"set TARGET={target}",
            $"set EXE={exe}",
            "set /a TRIES=0",
            ":wait",
            "tasklist /FI \"PID eq %PID%\" | find \"%PID%\" >nul",
            "if errorlevel 1 goto copy",
            "set /a TRIES+=1",
            "if %TRIES% GEQ 90 goto copy",
            "ping -n 2 127.0.0.1 >nul",
            "goto wait",
            ":copy",
            "robocopy \"%SOURCE%\" \"%TARGET%\" /E /MIR /R:3 /W:1 /NFL /NDL /NJH /NJS",
            "set RC=%ERRORLEVEL%",
            "if %RC% GEQ 8 exit /b %RC%",
            "start \"\" \"%EXE%\"",
            "exit /b 0",
            "",
        });
    }

    private static void RejectUnsafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(['"', '\r', '\n', '%']) >= 0)
        {
            throw new InvalidOperationException("Небезопасный путь обновления.");
        }
    }

    private static string TrimPath(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}

public sealed record ReleaseAsset(string Name, string DownloadUrl, string? Digest, long Size);

public sealed record ReleaseInfo(string Tag, bool Draft, bool Prerelease, IReadOnlyList<ReleaseAsset> Assets);

public sealed record ReleaseOffer(Version Version, string Tag, ReleaseAsset Asset);
