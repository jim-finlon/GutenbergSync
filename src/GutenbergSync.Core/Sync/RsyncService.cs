using System.Diagnostics;
using System.Text.RegularExpressions;
using GutenbergSync.Core.Infrastructure;
using Serilog;

namespace GutenbergSync.Core.Sync;

/// <summary>
/// Service for executing rsync operations
/// </summary>
public sealed class RsyncService : IRsyncService
{
    private readonly IRsyncDiscoveryService _discoveryService;
    private readonly ILogger _logger;

    public RsyncService(IRsyncDiscoveryService discoveryService, ILogger logger)
    {
        _discoveryService = discoveryService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SyncResult> SyncAsync(
        string endpoint,
        string targetDirectory,
        RsyncOptions options,
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Discover rsync
        var discoveryResult = await _discoveryService.DiscoverAsync(cancellationToken);
        if (!discoveryResult.IsAvailable)
        {
            return new SyncResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = $"rsync is not available. {discoveryResult.InstallationInstructions}",
                Duration = DateTime.UtcNow - startTime
            };
        }

        var rsyncPath = discoveryResult.ExecutablePath!;
        _logger.Information("Starting rsync from {Endpoint} to {TargetDirectory}", endpoint, targetDirectory);

        // Build rsync arguments
        var arguments = BuildRsyncArguments(endpoint, targetDirectory, options);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (options.TimeoutSeconds > 0)
            {
                cts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = rsyncPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Handle WSL
            if (discoveryResult.Source == RsyncSource.WSL)
            {
                processStartInfo.FileName = "wsl";
                processStartInfo.Arguments = $"rsync {arguments}";
            }

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                return new SyncResult
                {
                    Success = false,
                    ExitCode = -1,
                    ErrorMessage = "Failed to start rsync process",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Parse output for progress
            var progressTask = Task.Run(async () =>
            {
                if (progress != null && options.ShowProgress)
                {
                    await ParseProgressAsync(process, progress, cts.Token);
                }
            }, cts.Token);

            await process.WaitForExitAsync(cts.Token);
            await progressTask;

            var result = new SyncResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                ErrorMessage = process.ExitCode != 0
                    ? await process.StandardError.ReadToEndAsync(cts.Token)
                    : null,
                Duration = DateTime.UtcNow - startTime
            };

            if (result.Success)
            {
                _logger.Information("Rsync completed successfully in {Duration}", result.Duration);
            }
            else
            {
                _logger.Warning("Rsync failed with exit code {ExitCode}: {Error}", result.ExitCode, result.ErrorMessage);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Warning("Rsync operation was cancelled");
            return new SyncResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = "Operation was cancelled or timed out",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error executing rsync");
            return new SyncResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var result = await _discoveryService.DiscoverAsync(cancellationToken);
        return result.IsAvailable;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RemoteFileInfo>> GetRemoteFileListAsync(
        string endpoint,
        string? pattern = null,
        CancellationToken cancellationToken = default)
    {
        // Use rsync --list-only for dry-run file listing
        var options = new RsyncOptions
        {
            DryRun = true,
            Verbose = true
        };

        var discoveryResult = await _discoveryService.DiscoverAsync(cancellationToken);
        if (!discoveryResult.IsAvailable)
        {
            return [];
        }

        var rsyncPath = discoveryResult.ExecutablePath!;
        var arguments = BuildRsyncArguments(endpoint, "/dev/null", options, listOnly: true);

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = rsyncPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (discoveryResult.Source == RsyncSource.WSL)
            {
                processStartInfo.FileName = "wsl";
                processStartInfo.Arguments = $"rsync {arguments}";
            }

            using var process = Process.Start(processStartInfo);
            if (process == null)
                return [];

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
                return [];

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            return ParseFileList(output);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error listing remote files");
            return [];
        }
    }

    private static string BuildRsyncArguments(
        string endpoint,
        string targetDirectory,
        RsyncOptions options,
        bool listOnly = false)
    {
        var args = new List<string>();

        // Basic options
        args.Add("--archive"); // -a: archive mode
        args.Add("--verbose"); // -v: verbose
        args.Add("--human-readable"); // -h: human-readable sizes

        if (options.ShowProgress && !listOnly)
        {
            args.Add("--progress");
        }

        if (options.DryRun || listOnly)
        {
            args.Add("--dry-run");
        }

        // Include/exclude patterns
        foreach (var include in options.Include)
        {
            args.Add($"--include={include}");
        }

        foreach (var exclude in options.Exclude)
        {
            args.Add($"--exclude={exclude}");
        }

        // Max file size
        if (options.MaxFileSizeMb.HasValue)
        {
            args.Add($"--max-size={options.MaxFileSizeMb}M");
        }

        // Bandwidth limit
        if (options.BandwidthLimitKbps.HasValue)
        {
            args.Add($"--bwlimit={options.BandwidthLimitKbps}");
        }

        // Delete removed files
        if (options.DeleteRemoved)
        {
            args.Add("--delete");
        }

        // Source and destination
        args.Add(endpoint);
        args.Add(targetDirectory);

        return string.Join(" ", args);
    }

    private static async Task ParseProgressAsync(
        Process process,
        IProgress<SyncProgress> progress,
        CancellationToken cancellationToken)
    {
        var progressRegex = new Regex(
            @"(\d+)\s+(\d+%)\s+([\d.]+[KMGT]?B/s)\s+(\d+:\d+:\d+)\s+(.+)",
            RegexOptions.Compiled);

        long totalBytes = 0;
        long bytesTransferred = 0;
        long filesTransferred = 0;

        while (!process.HasExited && !cancellationToken.IsCancellationRequested)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (line == null)
                break;

            // Parse progress line: "1234567  45%  1.23MB/s    0:00:05  filename.txt"
            var match = progressRegex.Match(line);
            if (match.Success)
            {
                if (long.TryParse(match.Groups[1].Value, out var bytes))
                {
                    bytesTransferred = bytes;
                }

                var currentFile = match.Groups[5].Value.Trim();
                progress.Report(new SyncProgress
                {
                    BytesTransferred = bytesTransferred,
                    CurrentFile = currentFile,
                    FilesTransferred = filesTransferred++
                });
            }
            else if (line.Contains("total size is"))
            {
                // Extract total size: "total size is 123456789  speedup is 1.23"
                var sizeMatch = Regex.Match(line, @"total size is (\d+)");
                if (sizeMatch.Success && long.TryParse(sizeMatch.Groups[1].Value, out totalBytes))
                {
                    progress.Report(new SyncProgress
                    {
                        TotalBytes = totalBytes,
                        BytesTransferred = bytesTransferred
                    });
                }
            }
        }
    }

    private static IReadOnlyList<RemoteFileInfo> ParseFileList(string output)
    {
        var files = new List<RemoteFileInfo>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Parse rsync --list-only output format
            // Format: permissions size date time path
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 5)
            {
                if (long.TryParse(parts[1], out var size))
                {
                    var path = string.Join(" ", parts.Skip(4));
                    files.Add(new RemoteFileInfo
                    {
                        Path = path,
                        SizeBytes = size
                    });
                }
            }
        }

        return files;
    }
}

