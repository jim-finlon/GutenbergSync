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

        var hasTimeout = options.TimeoutSeconds > 0;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (hasTimeout)
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
            // Use ConfigureAwait(false) to allow progress updates from background thread
            var progressTask = Task.Run(async () =>
            {
                if (progress != null && options.ShowProgress)
                {
                    await ParseProgressAsync(process, progress, cts.Token).ConfigureAwait(false);
                }
            }, cts.Token);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Kill the rsync process if cancelled
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        await process.WaitForExitAsync();
                    }
                }
                catch
                {
                    // Ignore errors killing the process
                }
                throw;
            }
            finally
            {
                await progressTask;
            }

            // Extract stats from progress (if available)
            var finalProgress = new SyncProgress();
            // Note: Progress stats would need to be captured from ParseProgressAsync
            // For now, we'll rely on rsync's exit code and error messages

            var result = new SyncResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                ErrorMessage = process.ExitCode != 0
                    ? await process.StandardError.ReadToEndAsync()
                    : null,
                Duration = DateTime.UtcNow - startTime,
                WasCancelled = false // Set by catch block if cancelled
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
            // Check if cancellation was due to timeout (timeout triggered but not manual cancellation)
            // If timeout was set and the original cancellation token wasn't cancelled, it's a timeout
            var isTimeout = hasTimeout && !cancellationToken.IsCancellationRequested;
            
            if (isTimeout)
            {
                _logger.Warning("Rsync operation timed out after {TimeoutSeconds}s - partial files preserved for resume", options.TimeoutSeconds);
                return new SyncResult
                {
                    Success = false,
                    ExitCode = -1,
                    ErrorMessage = $"Operation timed out after {options.TimeoutSeconds} seconds. Run the same command again to resume.",
                    Duration = DateTime.UtcNow - startTime,
                    WasCancelled = false // Timeout is not a cancellation - should retry
                };
            }
            
            _logger.Information("Rsync operation was cancelled - partial files preserved for resume");
            return new SyncResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = "Operation was cancelled. Run the same command again to resume.",
                Duration = DateTime.UtcNow - startTime,
                WasCancelled = true
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
        args.Add("--archive"); // -a: archive mode (preserves permissions, timestamps, etc.)
        args.Add("--verbose"); // -v: verbose
        args.Add("--human-readable"); // -h: human-readable sizes
        args.Add("--partial"); // Keep partial files for resume
        args.Add("--partial-dir=.rsync-partial"); // Store partial files in hidden directory
        // Note: rsync's default delta-transfer algorithm handles resume for static files
        // --append-verify is for growing files (logs), not needed for static ebooks

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
        // Regex to match rsync progress lines
        // Format: "1234567  45%  1.23MB/s    0:00:05  filename.txt"
        // Also matches: "1234567  45%  12345678  1.23MB/s    0:00:05  filename.txt" (with total size)
        var progressRegex = new Regex(
            @"(\d+)\s+(\d+)%\s+(\d+)?\s*([\d.]+[KMGT]?B/s)?\s*(\d+:\d+:\d+)?\s*(.+)",
            RegexOptions.Compiled);

        long totalBytes = 0;
        long bytesTransferred = 0;
        long filesTransferred = 0;
        var startTime = DateTime.UtcNow;

        while (!process.HasExited && !cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line == null)
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Ignore read errors, continue
                continue;
            }

            // Parse progress line
            var match = progressRegex.Match(line);
            if (match.Success)
            {
                if (long.TryParse(match.Groups[1].Value, out var bytes))
                {
                    bytesTransferred = bytes;
                }

                var currentFile = match.Groups[5].Value.Trim();
                var elapsed = DateTime.UtcNow - startTime;
                
                progress.Report(new SyncProgress
                {
                    BytesTransferred = bytesTransferred,
                    TotalBytes = totalBytes,
                    CurrentFile = currentFile,
                    FilesTransferred = filesTransferred++,
                    SpeedBytesPerSecond = elapsed.TotalSeconds > 0 
                        ? (long)(bytesTransferred / elapsed.TotalSeconds) 
                        : null
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
            else if (line.StartsWith("receiving file list") || line.Contains("files to consider") || 
                     line.Contains("building file list") || line.StartsWith("sending incremental"))
            {
                // Initial phase - rsync is building file list
                // Report periodically to show activity
                progress.Report(new SyncProgress
                {
                    CurrentFile = "Building file list...",
                    FilesTransferred = 0,
                    BytesTransferred = 0
                });
            }
            else if (line.Contains("files...") || line.Contains("to transfer"))
            {
                // Extract file count from lines like "12345 files to consider"
                var fileMatch = Regex.Match(line, @"(\d+)\s+files");
                if (fileMatch.Success && long.TryParse(fileMatch.Groups[1].Value, out var totalFiles))
                {
                    progress.Report(new SyncProgress
                    {
                        TotalFiles = totalFiles,
                        CurrentFile = $"Scanning files... ({totalFiles} found)",
                        FilesTransferred = 0,
                        BytesTransferred = 0
                    });
                }
            }
            else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("total size"))
            {
                // Report any other non-empty lines to show activity
                // This helps show that rsync is working even if we can't parse the line
                if (filesTransferred == 0 && bytesTransferred == 0)
                {
                    progress.Report(new SyncProgress
                    {
                        CurrentFile = line.Trim(),
                        FilesTransferred = 0,
                        BytesTransferred = 0
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

