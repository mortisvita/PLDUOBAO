/******************
感谢Hawthorn给我的游戏下载码字
******************/
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease;

namespace PLDUOBAO.Services;

internal sealed class PEGameDownloadService
{
    private const string VersionListRequestBody = "{\"setting_name\":\"pc_sdkclient_full_patchlist\"}";
    private const string VersionListRequestPath = "/interconn/web/pack-setting/get-for-mcstudio";
    private const string PatchManifestSuffix = "/patch.json";
    private const long UnknownSizeFallbackBytes = 4L * 1024 * 1024 * 1024;
    private const long MinimumExtractBytes = 2L * 1024 * 1024 * 1024;
    private const long SafetyBufferBytes = 512L * 1024 * 1024;
    private const int BufferSize = 81920;
    private static readonly HttpClient DownloadClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    public async Task<IReadOnlyList<GameDownloadVersionInfo>> GetAvailableVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureLoggedIn();
        cancellationToken.ThrowIfCancellationRequested();

        string? response = await NetworkLocalAPI.X19PostAsync(
            NetworkLocalAPI.PostMode.Client,
            VersionListRequestBody,
            VersionListRequestPath);

        if (string.IsNullOrWhiteSpace(response))
            throw new InvalidOperationException("版本列表响应为空");

        using var outerDocument = JsonDocument.Parse(response);
        JsonElement outerRoot = outerDocument.RootElement;

        if (!IsSuccessResponse(outerRoot, out string message))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? "获取游戏版本列表失败"
                : $"获取游戏版本列表失败: {message}");
        }

        if (!TryGetPropertyIgnoreCase(outerRoot, "entity", out JsonElement entityElement) ||
            !TryGetPropertyIgnoreCase(entityElement, "setting_value", out JsonElement settingValueElement))
        {
            throw new InvalidOperationException("版本列表缺少 setting_value 数据");
        }

        List<GameDownloadVersionInfo> versions;
        if (settingValueElement.ValueKind == JsonValueKind.Object)
        {
            versions = ParseVersionList(settingValueElement);
        }
        else if (TryGetString(settingValueElement, out string settingValue) &&
                 !string.IsNullOrWhiteSpace(settingValue))
        {
            using var innerDocument = JsonDocument.Parse(settingValue);
            versions = ParseVersionList(innerDocument.RootElement);
        }
        else
        {
            throw new InvalidOperationException("版本列表 setting_value 数据无效");
        }

        if (versions.Count == 0)
            throw new InvalidOperationException("未获取到可下载的游戏版本");

        return versions;
    }

    public async Task<GameDownloadResult> DownloadAndInstallAsync(
        GameDownloadVersionInfo version,
        string installDirectory,
        IProgress<GameInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        EnsureLoggedIn();

        if (string.IsNullOrWhiteSpace(version.PackageUrl))
            throw new InvalidOperationException("选中的版本缺少安装包地址");

        if (string.IsNullOrWhiteSpace(installDirectory))
            throw new ArgumentException("游戏下载路径不能为空", nameof(installDirectory));

        string fullDownloadDirectory = Path.GetFullPath(installDirectory);
        string versionDirectory = Path.Combine(fullDownloadDirectory, SanitizeFileName(version.Version));
        ValidateVersionDirectory(versionDirectory);

        Report(progress, GameInstallStage.Initializing, -1, $"正在准备安装 {version.Version}...");

        Report(progress, GameInstallStage.FetchingMetadata, -1, "正在获取安装包元数据...");
        PackageMetadata metadata = await ResolvePackageMetadataAsync(version, cancellationToken);

        Report(
            progress,
            GameInstallStage.CheckingDiskSpace,
            -1,
            metadata.PackageSizeBytes.HasValue
                ? $"正在检查磁盘空间，安装包约 {FormatBytes(metadata.PackageSizeBytes.Value)}..."
                : $"未获取到安装包大小，按 {FormatBytes(UnknownSizeFallbackBytes)} 预留空间检查...");
        EnsureEnoughDiskSpace(fullDownloadDirectory, metadata.PackageSizeBytes);

        Directory.CreateDirectory(fullDownloadDirectory);
        string stagingDirectory = Path.Combine(
            fullDownloadDirectory,
            $".download-{SanitizeFileName(version.Version)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);
        string archivePath = Path.Combine(fullDownloadDirectory, BuildArchiveFileName(version));

        try
        {
            var downloadStopwatch = System.Diagnostics.Stopwatch.StartNew();
            await DownloadPackageAsync(
                version.PackageUrl,
                archivePath,
                metadata.PackageSizeBytes,
                progress,
                cancellationToken);
            downloadStopwatch.Stop();
            var downloadElapsed = downloadStopwatch.Elapsed;

            if (!string.IsNullOrWhiteSpace(metadata.HashValue) &&
                !string.IsNullOrWhiteSpace(metadata.HashAlgorithm))
            {
                Report(
                    progress,
                    GameInstallStage.Verifying,
                    -1,
                    $"正在校验安装包 ({metadata.HashAlgorithm.ToUpperInvariant()})...");
                await VerifyDownloadedPackageAsync(archivePath, metadata, cancellationToken);
                Report(progress, GameInstallStage.Verifying, 100, "安装包校验通过");
            }
            else
            {
                Report(progress, GameInstallStage.Verifying, 100, "未获取到校验信息，已跳过安装包校验");
            }

            var extractStopwatch = System.Diagnostics.Stopwatch.StartNew();
            await ExtractArchiveAsync(archivePath, stagingDirectory, progress, cancellationToken);
            extractStopwatch.Stop();
            var extractElapsed = extractStopwatch.Elapsed;

            Report(progress, GameInstallStage.Finalizing, -1, "正在整理版本目录...");
            string gameDirectory = NormalizeInstalledVersionDirectory(stagingDirectory, versionDirectory);

            Report(progress, GameInstallStage.Completed, 100, $"安装完成: {version.Version}");
            return new GameDownloadResult
            {
                GameDirectory = gameDirectory,
                DownloadElapsed = downloadElapsed,
                ExtractElapsed = extractElapsed
            };
        }
        finally
        {
            DeleteFileIfExists(archivePath);
            DeleteDirectoryIfExists(stagingDirectory);
        }
    }

    private static List<GameDownloadVersionInfo> ParseVersionList(JsonElement root)
    {
        var versions = new Dictionary<string, GameDownloadVersionInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (IsReservedVersionKey(property.Name))
                continue;

            if (TryCreateVersionInfo(property.Name, property.Value, out GameDownloadVersionInfo? versionInfo))
                versions[property.Name] = versionInfo!;
        }

        if (TryGetPropertyIgnoreCase(root, "entities", out JsonElement entitiesElement) &&
            entitiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in entitiesElement.EnumerateObject())
            {
                if (TryCreateVersionInfo(property.Name, property.Value, out GameDownloadVersionInfo? versionInfo))
                    versions[property.Name] = versionInfo!;
            }
        }

        List<GameDownloadVersionInfo> standardVersions = versions.Values
            .Where(static value => value.IsStandardVersion)
            .ToList();
        standardVersions.Sort(static (left, right) => CompareStandardVersions(right.Version, left.Version));

        List<GameDownloadVersionInfo> specialVersions = versions.Values
            .Where(static value => !value.IsStandardVersion)
            .OrderBy(static value => value.Version, StringComparer.Ordinal)
            .ToList();

        standardVersions.AddRange(specialVersions);
        return standardVersions;
    }

    private static bool TryCreateVersionInfo(
        string version,
        JsonElement element,
        out GameDownloadVersionInfo? versionInfo)
    {
        versionInfo = null;
        if (string.IsNullOrWhiteSpace(version))
            return false;

        string? rawUrl = ExtractUrl(element);
        if (!IsHttpUrl(rawUrl))
            return false;

        string packageUrl = rawUrl!;
        string? manifestUrl = null;
        if (packageUrl.EndsWith(PatchManifestSuffix, StringComparison.OrdinalIgnoreCase))
        {
            manifestUrl = packageUrl;
            packageUrl = packageUrl[..^PatchManifestSuffix.Length] + ".zip";
        }

        versionInfo = new GameDownloadVersionInfo
        {
            Version = version,
            PackageUrl = packageUrl,
            ManifestUrl = manifestUrl,
            IsStandardVersion = IsStandardVersion(version)
        };
        return true;
    }

    private static string? ExtractUrl(JsonElement element)
    {
        if (TryGetString(element, out string directValue) && IsHttpUrl(directValue))
            return directValue;

        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if ((property.Name.Equals("url", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("download_url", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.Equals("package_url", StringComparison.OrdinalIgnoreCase)) &&
                TryGetString(property.Value, out string value) &&
                IsHttpUrl(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsReservedVersionKey(string key)
    {
        return key.Equals("entities", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("beta", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("stable", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("test", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStandardVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        string[] parts = version.Split('.', StringSplitOptions.None);
        return parts.Length > 0 && parts.All(static part => int.TryParse(part, out _));
    }

    private static int CompareStandardVersions(string left, string right)
    {
        string[] leftParts = left.Split('.', StringSplitOptions.None);
        string[] rightParts = right.Split('.', StringSplitOptions.None);
        int length = Math.Max(leftParts.Length, rightParts.Length);

        for (int index = 0; index < length; index++)
        {
            int leftValue = index < leftParts.Length && int.TryParse(leftParts[index], out int parsedLeft)
                ? parsedLeft
                : 0;
            int rightValue = index < rightParts.Length && int.TryParse(rightParts[index], out int parsedRight)
                ? parsedRight
                : 0;
            if (leftValue != rightValue)
                return leftValue.CompareTo(rightValue);
        }

        return 0;
    }

    private static async Task<PackageMetadata> ResolvePackageMetadataAsync(
        GameDownloadVersionInfo version,
        CancellationToken cancellationToken)
    {
        var metadata = new PackageMetadata();

        if (!string.IsNullOrWhiteSpace(version.ManifestUrl))
        {
            try
            {
                using var response = await DownloadClient.GetAsync(version.ManifestUrl, cancellationToken);
                response.EnsureSuccessStatusCode();
                string manifestContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(manifestContent))
                {
                    using var manifestDocument = JsonDocument.Parse(manifestContent);
                    ApplyManifestMetadata(manifestDocument.RootElement, version.PackageUrl, metadata);
                }
            }
            catch
            {
                // Manifest is optional; fallback to headers when it is unavailable.
            }
        }

        if (!metadata.PackageSizeBytes.HasValue)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, version.PackageUrl);
                using var response = await DownloadClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (response.IsSuccessStatusCode)
                    metadata.PackageSizeBytes = response.Content.Headers.ContentLength;
            }
            catch
            {
                // Some CDN endpoints do not support HEAD.
            }
        }

        return metadata;
    }

    private static void ApplyManifestMetadata(JsonElement root, string packageUrl, PackageMetadata target)
    {
        List<ManifestCandidate> candidates = [];
        CollectManifestCandidates(root, packageUrl, candidates);
        ManifestCandidate? bestCandidate = candidates
            .OrderByDescending(static candidate => candidate.IsPackageUrlMatch)
            .ThenByDescending(static candidate => candidate.HasFullMetadata)
            .ThenByDescending(static candidate => candidate.SizeBytes.HasValue)
            .ThenByDescending(static candidate => !string.IsNullOrWhiteSpace(candidate.HashValue))
            .FirstOrDefault();

        if (bestCandidate == null)
            return;

        if (bestCandidate.SizeBytes.HasValue)
            target.PackageSizeBytes = bestCandidate.SizeBytes;

        if (!string.IsNullOrWhiteSpace(bestCandidate.HashValue) &&
            !string.IsNullOrWhiteSpace(bestCandidate.HashAlgorithm))
        {
            target.HashValue = bestCandidate.HashValue;
            target.HashAlgorithm = bestCandidate.HashAlgorithm;
        }
    }

    private static void CollectManifestCandidates(
        JsonElement element,
        string packageUrl,
        List<ManifestCandidate> candidates)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var candidate = new ManifestCandidate();

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string propertyName = property.Name.ToLowerInvariant();
                    if ((propertyName == "url" ||
                         propertyName == "download_url" ||
                         propertyName == "package_url") &&
                        TryGetString(property.Value, out string urlValue) &&
                        IsHttpUrl(urlValue))
                    {
                        candidate.Url = urlValue;
                    }
                    else if ((propertyName == "size" ||
                              propertyName == "package_size" ||
                              propertyName == "download_size") &&
                             TryGetInt64(property.Value, out long sizeBytes))
                    {
                        candidate.SizeBytes = sizeBytes;
                    }
                    else if (propertyName == "md5" &&
                             TryGetString(property.Value, out string md5Value) &&
                             !string.IsNullOrWhiteSpace(md5Value))
                    {
                        candidate.HashValue = md5Value;
                        candidate.HashAlgorithm = "md5";
                    }
                    else if ((propertyName == "sha1" || propertyName == "sha_1") &&
                             TryGetString(property.Value, out string sha1Value) &&
                             !string.IsNullOrWhiteSpace(sha1Value))
                    {
                        candidate.HashValue = sha1Value;
                        candidate.HashAlgorithm = "sha1";
                    }
                    else if ((propertyName == "hash" || propertyName == "checksum") &&
                             TryGetString(property.Value, out string hashValue) &&
                             !string.IsNullOrWhiteSpace(hashValue))
                    {
                        candidate.HashValue = hashValue;
                        candidate.HashAlgorithm ??= DetectHashAlgorithm(hashValue);
                    }
                    else if ((propertyName == "hash_type" ||
                              propertyName == "hash_algorithm" ||
                              propertyName == "algorithm") &&
                             TryGetString(property.Value, out string algorithmValue) &&
                             !string.IsNullOrWhiteSpace(algorithmValue))
                    {
                        candidate.HashAlgorithm = NormalizeHashAlgorithm(algorithmValue);
                    }
                }

                if (candidate.HasUsefulMetadata)
                {
                    candidate.IsPackageUrlMatch = AreEquivalentUrls(candidate.Url, packageUrl);
                    candidates.Add(candidate);
                }

                foreach (JsonProperty property in element.EnumerateObject())
                    CollectManifestCandidates(property.Value, packageUrl, candidates);
                break;

            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                    CollectManifestCandidates(item, packageUrl, candidates);
                break;
        }
    }

    private static void EnsureEnoughDiskSpace(string installDirectory, long? packageSizeBytes)
    {
        string? root = Path.GetPathRoot(installDirectory);
        if (string.IsNullOrWhiteSpace(root))
            throw new IOException("无法确定安装目录所在磁盘");

        var drive = new DriveInfo(root);
        long requiredBytes = packageSizeBytes.HasValue
            ? packageSizeBytes.Value + Math.Max(packageSizeBytes.Value * 3, MinimumExtractBytes) + SafetyBufferBytes
            : UnknownSizeFallbackBytes + SafetyBufferBytes;

        if (drive.AvailableFreeSpace < requiredBytes)
        {
            throw new IOException(
                $"磁盘空间不足，需要约 {FormatBytes(requiredBytes)} 可用空间，当前仅剩 {FormatBytes(drive.AvailableFreeSpace)}");
        }
    }

    private static void ValidateVersionDirectory(string versionDirectory)
    {
        if (Directory.Exists(versionDirectory) && Directory.EnumerateFileSystemEntries(versionDirectory).Any())
            throw new IOException($"版本目录已存在且不是空目录: {versionDirectory}");
    }

    private static string NormalizeInstalledVersionDirectory(string stagingDirectory, string versionDirectory)
    {
        string extractedGameDirectory = GamePathScannerService.FindInstalledGameDirectory(stagingDirectory);

        if (Directory.Exists(versionDirectory))
        {
            if (Directory.EnumerateFileSystemEntries(versionDirectory).Any())
                throw new IOException($"版本目录已存在且不是空目录: {versionDirectory}");

            Directory.Delete(versionDirectory);
        }

        string? parentDirectory = Path.GetDirectoryName(versionDirectory);
        if (!string.IsNullOrWhiteSpace(parentDirectory))
            Directory.CreateDirectory(parentDirectory);

        Directory.Move(extractedGameDirectory, versionDirectory);

        if (!GamePathScannerService.ValidatePath(versionDirectory))
            throw new InvalidDataException("整理后的游戏目录无效");

        return versionDirectory;
    }

    private static async Task DownloadPackageAsync(
        string packageUrl,
        string archivePath,
        long? expectedTotalBytes,
        IProgress<GameInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, packageUrl);
        using var response = await DownloadClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength ?? expectedTotalBytes;
        await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(
            archivePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            true);

        var buffer = new byte[BufferSize];
        long totalBytesRead = 0;
        DateTime lastReportTime = DateTime.UtcNow;

        while (true)
        {
            int bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead == 0)
                break;

            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            DateTime now = DateTime.UtcNow;
            if ((now - lastReportTime).TotalMilliseconds >= 250)
            {
                Report(
                    progress,
                    GameInstallStage.Downloading,
                    CalculateProgress(totalBytesRead, totalBytes),
                    BuildDownloadStatus(totalBytesRead, totalBytes),
                    totalBytesRead,
                    totalBytes);
                lastReportTime = now;
            }
        }

        Report(
            progress,
            GameInstallStage.Downloading,
            100,
            BuildDownloadStatus(totalBytesRead, totalBytes),
            totalBytesRead,
            totalBytes);

        if (totalBytes.HasValue && totalBytesRead != totalBytes.Value)
        {
            throw new InvalidDataException(
                $"安装包下载不完整，期望 {FormatBytes(totalBytes.Value)}，实际 {FormatBytes(totalBytesRead)}。");
        }
    }

    private static async Task VerifyDownloadedPackageAsync(
        string archivePath,
        PackageMetadata metadata,
        CancellationToken cancellationToken)
    {
        HashAlgorithm hashAlgorithm = metadata.HashAlgorithm!.Equals("sha1", StringComparison.OrdinalIgnoreCase)
            ? SHA1.Create()
            : MD5.Create();

        using (hashAlgorithm)
        await using (var stream = new FileStream(
                         archivePath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read,
                         BufferSize,
                         true))
        {
            byte[] hashBytes = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
            string actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            if (!actualHash.Equals(metadata.HashValue, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"安装包校验失败，期望 {metadata.HashAlgorithm.ToUpperInvariant()} 为 {metadata.HashValue}，实际为 {actualHash}");
            }
        }
    }

    private static async Task ExtractArchiveAsync(
        string archivePath,
        string extractDirectory,
        IProgress<GameInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        long totalBytes = archive.Entries.Sum(static entry => entry.Length);
        long extractedBytes = 0;
        int extractedEntries = 0;
        DateTime lastReportTime = DateTime.UtcNow;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string destinationPath = GetSafeExtractionPath(extractDirectory, entry.FullName);
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            long writtenBytes;
            await using (Stream entryStream = entry.Open())
            await using (var fileStream = new FileStream(
                             destinationPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             BufferSize,
                             true))
            {
                writtenBytes = await CopyArchiveEntryAsync(entryStream, fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
            }

            if (writtenBytes != entry.Length)
            {
                throw new InvalidDataException(
                    $"解压文件大小不一致: {entry.FullName}，期望 {entry.Length}，实际 {writtenBytes}。");
            }

            if (entry.LastWriteTime != default)
                File.SetLastWriteTime(destinationPath, entry.LastWriteTime.LocalDateTime);

            extractedBytes += writtenBytes;
            extractedEntries++;

            DateTime now = DateTime.UtcNow;
            if ((now - lastReportTime).TotalMilliseconds >= 250)
            {
                Report(progress, GameInstallStage.Extracting,
                    CalculateExtractProgress(extractedBytes, extractedEntries, totalBytes, archive.Entries.Count),
                    BuildExtractStatus(extractedBytes, extractedEntries, totalBytes, archive.Entries.Count));
                lastReportTime = now;
            }
        }

        Report(progress, GameInstallStage.Extracting, 100, "游戏文件解压完成");
    }

    private static async Task<long> CopyArchiveEntryAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        long totalBytes = 0;

        while (true)
        {
            int bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead == 0)
                return totalBytes;

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytes += bytesRead;
        }
    }

    private static async Task ExtractArchiveLegacyParallelAsync(
        string archivePath,
        string extractDirectory,
        IProgress<GameInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        // 第一步：收集所有条目信息（ZipArchive 非线程安全，必须在单线程中读取）
        var entryInfos = new List<(string DestinationPath, long Offset, long CompressedLength, long Length, DateTimeOffset LastWriteTime)>();

        using (var archive = ZipFile.OpenRead(archivePath))
        {
            long currentOffset = 0;
            foreach (var entry in archive.Entries)
            {
                string destinationPath = GetSafeExtractionPath(extractDirectory, entry.FullName);
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                string? destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                entryInfos.Add((destinationPath, currentOffset, entry.CompressedLength, entry.Length, entry.LastWriteTime));
                currentOffset++;
            }
        }

        long totalBytes = entryInfos.Sum(e => e.Length);
        long extractedBytes = 0;
        int extractedEntries = 0;
        DateTime lastReportTime = DateTime.UtcNow;
        object progressLock = new();

        // 第二步：并行解压（每个 entry 独立打开 archive 读取对应 offset 的数据）
        int maxDegree = Math.Min(Environment.ProcessorCount, 8);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegree,
            CancellationToken = cancellationToken
        };

        // 按顺序分块处理，每个 chunk 内部顺序解压以保证磁盘 IO 不会过度随机
        int chunkSize = Math.Max(1, entryInfos.Count / maxDegree);
        var chunks = Enumerable.Range(0, (entryInfos.Count + chunkSize - 1) / chunkSize)
            .Select(i => entryInfos.Skip(i * chunkSize).Take(chunkSize).ToList())
            .ToList();

        await Task.Run(() =>
        {
            Parallel.ForEach(chunks, parallelOptions, chunk =>
            {
                using var chunkArchive = ZipFile.OpenRead(archivePath);
                var entries = chunkArchive.Entries;

                foreach (var (destinationPath, offset, _, length, lastWriteTime) in chunk)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (offset >= entries.Count) continue;
                    var entry = entries[(int)offset];

                    using Stream entryStream = entry.Open();
                    using var fileStream = new FileStream(
                        destinationPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        BufferSize,
                        false);

                    var buffer = new byte[BufferSize];
                    while (true)
                    {
                        int bytesRead = entryStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;
                        fileStream.Write(buffer, 0, bytesRead);
                    }

                    if (lastWriteTime != default)
                        File.SetLastWriteTime(destinationPath, lastWriteTime.LocalDateTime);

                    Interlocked.Add(ref extractedBytes, length);
                    Interlocked.Increment(ref extractedEntries);

                    DateTime now = DateTime.UtcNow;
                    bool shouldReport = false;
                    lock (progressLock)
                    {
                        if ((now - lastReportTime).TotalMilliseconds >= 250)
                        {
                            lastReportTime = now;
                            shouldReport = true;
                        }
                    }

                    if (shouldReport)
                    {
                        int curEntries, curCount;
                        long curBytes, curTotal;
                        lock (progressLock)
                        {
                            curEntries = Volatile.Read(ref extractedEntries);
                            curCount = entryInfos.Count;
                            curBytes = Volatile.Read(ref extractedBytes);
                            curTotal = totalBytes;
                        }
                        Report(progress, GameInstallStage.Extracting,
                            CalculateExtractProgress(curBytes, curEntries, curTotal, curCount),
                            BuildExtractStatus(curBytes, curEntries, curTotal, curCount));
                    }
                }
            });
        }, cancellationToken);

        Report(progress, GameInstallStage.Extracting, 100, "游戏文件解压完成");
    }

    private static string BuildArchiveFileName(GameDownloadVersionInfo version)
    {
        return $"minecraft-{SanitizeFileName(version.Version)}.zip";
    }

    private static string GetSafeExtractionPath(string baseDirectory, string relativePath)
    {
        string fullBaseDirectory = Path.GetFullPath(baseDirectory);
        if (!fullBaseDirectory.EndsWith(Path.DirectorySeparatorChar))
            fullBaseDirectory += Path.DirectorySeparatorChar;

        string destinationPath = Path.GetFullPath(Path.Combine(fullBaseDirectory, relativePath));
        if (!destinationPath.StartsWith(fullBaseDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("压缩包包含非法路径，已中止解压");

        return destinationPath;
    }

    private static string SanitizeFileName(string value)
    {
        var chars = value.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray();
        string sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "package" : sanitized;
    }

    private static void EnsureLoggedIn()
    {
        var user = NetworkLocalAPI.CurrentAvaliableUser;
        if (user == null ||
            string.IsNullOrWhiteSpace(user.entity_id) ||
            string.IsNullOrWhiteSpace(user.token))
        {
            throw new InvalidOperationException("请先登录 PE 账号后再下载游戏");
        }
    }

    private static bool IsSuccessResponse(JsonElement root, out string message)
    {
        message = TryGetPropertyIgnoreCase(root, "message", out JsonElement messageElement) &&
                  TryGetString(messageElement, out string parsedMessage)
            ? parsedMessage
            : string.Empty;

        if (!TryGetPropertyIgnoreCase(root, "code", out JsonElement codeElement))
            return false;

        return codeElement.ValueKind switch
        {
            JsonValueKind.Number => codeElement.TryGetInt32(out int code) && code == 0,
            JsonValueKind.String => string.Equals(codeElement.GetString(), "0", StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement propertyValue)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    propertyValue = property.Value;
                    return true;
                }
            }
        }

        propertyValue = default;
        return false;
    }

    private static bool TryGetString(JsonElement element, out string value)
    {
        value = string.Empty;
        if (element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        if (element.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            value = element.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static bool TryGetInt64(JsonElement element, out long value)
    {
        if (element.ValueKind == JsonValueKind.Number)
            return element.TryGetInt64(out value);

        if (element.ValueKind == JsonValueKind.String)
            return long.TryParse(element.GetString(), out value);

        value = 0;
        return false;
    }

    private static bool IsHttpUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool AreEquivalentUrls(string? left, string right)
    {
        if (!IsHttpUrl(left) || !IsHttpUrl(right))
            return false;

        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static string? DetectHashAlgorithm(string hashValue)
    {
        string trimmed = hashValue.Trim();
        return trimmed.Length switch
        {
            32 => "md5",
            40 => "sha1",
            _ => null
        };
    }

    private static string? NormalizeHashAlgorithm(string algorithm)
    {
        string value = algorithm.Trim().ToLowerInvariant();
        return value switch
        {
            "md5" => "md5",
            "sha1" => "sha1",
            "sha-1" => "sha1",
            _ => null
        };
    }

    private static double CalculateProgress(long completedBytes, long? totalBytes)
    {
        if (!totalBytes.HasValue || totalBytes.Value <= 0)
            return -1;

        return Math.Clamp((double)completedBytes / totalBytes.Value * 100d, 0d, 100d);
    }

    private static double CalculateExtractProgress(
        long extractedBytes,
        int extractedEntries,
        long totalBytes,
        int totalEntries)
    {
        if (totalBytes > 0)
            return Math.Clamp((double)extractedBytes / totalBytes * 100d, 0d, 100d);

        if (totalEntries <= 0)
            return -1;

        return Math.Clamp((double)extractedEntries / totalEntries * 100d, 0d, 100d);
    }

    private static string BuildDownloadStatus(long completedBytes, long? totalBytes)
    {
        return totalBytes.HasValue && totalBytes.Value > 0
            ? $"正在下载游戏安装包... {FormatBytes(completedBytes)} / {FormatBytes(totalBytes.Value)}"
            : $"正在下载游戏安装包... 已下载 {FormatBytes(completedBytes)}";
    }

    private static string BuildExtractStatus(
        long extractedBytes,
        int extractedEntries,
        long totalBytes,
        int totalEntries)
    {
        if (totalBytes > 0)
            return $"正在解压游戏文件... {FormatBytes(extractedBytes)} / {FormatBytes(totalBytes)}";

        if (totalEntries > 0)
            return $"正在解压游戏文件... {extractedEntries} / {totalEntries}";

        return "正在解压游戏文件...";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024d;
            unitIndex++;
        }

        return $"{size:F1} {units[unitIndex]}";
    }

    private static void Report(
        IProgress<GameInstallProgress>? progress,
        GameInstallStage stage,
        double percentage,
        string status,
        long bytesReceived = 0,
        long? totalBytes = null)
    {
        progress?.Report(new GameInstallProgress
        {
            Stage = stage,
            ProgressPercentage = percentage,
            StatusMessage = status,
            BytesReceived = bytesReceived,
            TotalBytes = totalBytes
        });
    }

    private static void DeleteFileIfExists(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Temporary cleanup failure should not hide the real operation result.
        }
    }

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
                Directory.Delete(directoryPath, true);
        }
        catch
        {
            // Temporary cleanup failure should not hide the real operation result.
        }
    }

    private sealed class PackageMetadata
    {
        public long? PackageSizeBytes { get; set; }

        public string? HashValue { get; set; }

        public string? HashAlgorithm { get; set; }
    }

    private sealed class ManifestCandidate
    {
        public string? Url { get; set; }

        public long? SizeBytes { get; set; }

        public string? HashValue { get; set; }

        public string? HashAlgorithm { get; set; }

        public bool IsPackageUrlMatch { get; set; }

        public bool HasUsefulMetadata => SizeBytes.HasValue || !string.IsNullOrWhiteSpace(HashValue);

        public bool HasFullMetadata => SizeBytes.HasValue &&
                                       !string.IsNullOrWhiteSpace(HashValue) &&
                                       !string.IsNullOrWhiteSpace(HashAlgorithm);
    }
}

internal sealed class GameDownloadResult
{
    public string GameDirectory { get; init; } = string.Empty;
    public TimeSpan DownloadElapsed { get; init; }
    public TimeSpan ExtractElapsed { get; init; }
}

internal sealed class GameDownloadVersionInfo
{
    public string Version { get; init; } = string.Empty;

    public string PackageUrl { get; init; } = string.Empty;

    public string? ManifestUrl { get; init; }

    public bool IsStandardVersion { get; init; }

    public override string ToString()
    {
        return Version;
    }
}

internal sealed class GameInstallProgress
{
    public GameInstallStage Stage { get; init; }

    public double ProgressPercentage { get; init; }

    public string StatusMessage { get; init; } = string.Empty;

    public long BytesReceived { get; init; }

    public long? TotalBytes { get; init; }
}

internal enum GameInstallStage
{
    Initializing,
    FetchingMetadata,
    CheckingDiskSpace,
    Downloading,
    Verifying,
    Extracting,
    Finalizing,
    Completed
}

internal static class GamePathScannerService
{
    private const string GameExecutableName = "Minecraft.Windows.exe";

    public static bool ValidatePath(string directoryPath)
    {
        return !string.IsNullOrWhiteSpace(directoryPath) &&
               Directory.Exists(directoryPath) &&
               File.Exists(Path.Combine(directoryPath, GameExecutableName));
    }

    public static string FindInstalledGameDirectory(string extractDirectory)
    {
        if (ValidatePath(extractDirectory))
            return extractDirectory;

        string? gameExecutable = Directory
            .EnumerateFiles(extractDirectory, GameExecutableName, SearchOption.AllDirectories)
            .OrderBy(static path => path.Length)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(gameExecutable))
            throw new InvalidDataException("未在解压目录中找到有效的游戏安装目录");

        string? gameDirectory = Path.GetDirectoryName(gameExecutable);
        if (string.IsNullOrWhiteSpace(gameDirectory) || !ValidatePath(gameDirectory))
            throw new InvalidDataException("解压后的游戏目录无效");

        return gameDirectory;
    }
}
