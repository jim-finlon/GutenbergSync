using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GutenbergSync.Core.Infrastructure;

/// <summary>
/// Service for discovering rsync binary availability
/// </summary>
public sealed class RsyncDiscoveryService : IRsyncDiscoveryService
{
    /// <inheritdoc/>
    public async Task<RsyncDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var platform = PlatformDetector.GetCurrentPlatform();
        
        // Try to find rsync
        var (executablePath, source) = await FindRsyncAsync(platform, cancellationToken);
        
        if (executablePath == null)
        {
            return new RsyncDiscoveryResult
            {
                IsAvailable = false,
                Platform = platform,
                Source = RsyncSource.NotFound,
                InstallationInstructions = GetInstallationInstructions(platform)
            };
        }

        // Get version
        var version = await GetRsyncVersionAsync(executablePath, cancellationToken);

        return new RsyncDiscoveryResult
        {
            IsAvailable = true,
            ExecutablePath = executablePath,
            Platform = platform,
            Source = source,
            Version = version
        };
    }

    /// <inheritdoc/>
    public string GetInstallationInstructions(Platform platform) => platform switch
    {
        Platform.Linux => GetLinuxInstallationInstructions(),
        Platform.MacOS => GetMacOSInstallationInstructions(),
        Platform.Windows => GetWindowsInstallationInstructions(),
        _ => "Please install rsync 3.0 or later for your platform."
    };

    private static async Task<(string? Path, RsyncSource Source)> FindRsyncAsync(Platform platform, CancellationToken cancellationToken)
    {
        // First, try to find in PATH
        var pathResult = await FindInPathAsync(cancellationToken);
        if (pathResult != null)
        {
            return (pathResult, RsyncSource.Path);
        }

        // Platform-specific discovery
        return platform switch
        {
            Platform.Windows => await FindOnWindowsAsync(cancellationToken),
            Platform.Linux => await FindOnLinuxAsync(cancellationToken),
            Platform.MacOS => await FindOnMacOSAsync(cancellationToken),
            _ => (null, RsyncSource.NotFound)
        };
    }

    private static async Task<string?> FindInPathAsync(CancellationToken cancellationToken)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "rsync",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // On Windows, use "where" instead of "which"
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                processStartInfo.FileName = "where";
                processStartInfo.Arguments = "rsync";
            }

            using var process = Process.Start(processStartInfo);
            if (process == null)
                return null;

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                var path = output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    return path;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private static async Task<(string? Path, RsyncSource Source)> FindOnWindowsAsync(CancellationToken cancellationToken)
    {
        // Try WSL
        var wslPath = await FindInWSLAsync(cancellationToken);
        if (wslPath != null)
        {
            return (wslPath, RsyncSource.WSL);
        }

        // Try Cygwin
        var cygwinPath = FindInCygwin();
        if (cygwinPath != null)
        {
            return (cygwinPath, RsyncSource.Cygwin);
        }

        return (null, RsyncSource.NotFound);
    }

    private static async Task<string?> FindInWSLAsync(CancellationToken cancellationToken)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = "which rsync",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
                return null;

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                var path = output.Trim();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    // Return WSL command to invoke rsync
                    return $"wsl rsync";
                }
            }
        }
        catch
        {
            // WSL not available or rsync not installed in WSL
        }

        return null;
    }

    private static string? FindInCygwin()
    {
        // Common Cygwin installation paths
        var commonPaths = new[]
        {
            @"C:\cygwin64\bin\rsync.exe",
            @"C:\cygwin\bin\rsync.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "cygwin64", "bin", "rsync.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "cygwin", "bin", "rsync.exe")
        };

        return commonPaths.FirstOrDefault(File.Exists);
    }

    private static async Task<(string? Path, RsyncSource Source)> FindOnLinuxAsync(CancellationToken cancellationToken)
    {
        // On Linux, rsync should be in standard locations
        var standardPaths = new[]
        {
            "/usr/bin/rsync",
            "/bin/rsync",
            "/usr/local/bin/rsync"
        };

        foreach (var path in standardPaths)
        {
            if (File.Exists(path))
            {
                return (path, RsyncSource.Native);
            }
        }

        // Try which as fallback
        var whichResult = await FindInPathAsync(cancellationToken);
        if (whichResult != null)
        {
            return (whichResult, RsyncSource.Native);
        }

        return (null, RsyncSource.NotFound);
    }

    private static async Task<(string? Path, RsyncSource Source)> FindOnMacOSAsync(CancellationToken cancellationToken)
    {
        // On macOS, rsync should be in standard locations
        var standardPaths = new[]
        {
            "/usr/bin/rsync",
            "/usr/local/bin/rsync",
            "/opt/homebrew/bin/rsync" // Apple Silicon Homebrew
        };

        foreach (var path in standardPaths)
        {
            if (File.Exists(path))
            {
                return (path, RsyncSource.Native);
            }
        }

        // Try which as fallback
        var whichResult = await FindInPathAsync(cancellationToken);
        if (whichResult != null)
        {
            return (whichResult, RsyncSource.Native);
        }

        return (null, RsyncSource.NotFound);
    }

    private static async Task<string?> GetRsyncVersionAsync(string executablePath, CancellationToken cancellationToken)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
                return null;

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                // Extract version from first line (e.g., "rsync  version 3.2.7")
                var firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return firstLine?.Trim();
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private static string GetLinuxInstallationInstructions() => @"
Install rsync on Linux:

Debian/Ubuntu:
  sudo apt-get update
  sudo apt-get install rsync

RHEL/CentOS/Fedora:
  sudo yum install rsync
  # or on newer versions:
  sudo dnf install rsync

Arch Linux:
  sudo pacman -S rsync

Verify installation:
  rsync --version
";

    private static string GetMacOSInstallationInstructions() => @"
Install rsync on macOS:

Using Homebrew (recommended):
  brew install rsync

Using MacPorts:
  sudo port install rsync

Verify installation:
  rsync --version
";

    private static string GetWindowsInstallationInstructions() => @"
Install rsync on Windows:

Option 1: Windows Subsystem for Linux (WSL) - Recommended
  1. Install WSL:
     wsl --install
  2. After WSL is installed, open WSL and run:
     sudo apt-get update
     sudo apt-get install rsync
  3. Verify installation:
     wsl rsync --version

Option 2: Cygwin
  1. Download Cygwin from https://www.cygwin.com/
  2. During installation, search for 'rsync' and select it
  3. Add Cygwin bin directory to your PATH
  4. Verify installation:
     rsync --version

Option 3: Native Windows (cwRsync)
  1. Download from https://www.itefix.net/cwrsync
  2. Extract and add to PATH
  3. Verify installation:
     rsync --version
";
}
