# GutenbergSync

A .NET application to efficiently mirror and manage a local copy of the Project Gutenberg archive for AI/RAG ingestion purposes, adhering to Project Gutenberg's official mirroring policies and best practices.

## Overview

GutenbergSync provides a two-phase workflow for working with Project Gutenberg content:

1. **Mirror Raw Content**: Download and maintain a complete local mirror of the Project Gutenberg archive using rsync
2. **Create RAG-Ready Chunks**: Process the downloaded files into clean, chunked text with embedded metadata for RAG/LLM ingestion

The application uses a **metadata-first synchronization strategy** that builds a searchable catalog from RDF files before downloading content, enabling better progress tracking and selective syncing.

## Features

### Archive Synchronization
- ✅ rsync-based synchronization with official Project Gutenberg mirrors
- ✅ Incremental/delta updates (only downloads changed files)
- ✅ Auto-detection of rsync binary (Windows: WSL/Cygwin support)
- ✅ Multiple mirror endpoints with automatic failover
- ✅ Bandwidth throttling and rate limiting
- ✅ Content filtering by file type and language
- ✅ Metadata-first sync strategy (RDF → catalog → content)
- ✅ Resume interrupted downloads (automatic via rsync)
- ✅ Auto-retry on failure (built-in CLI option or wrapper script)
- ✅ File integrity verification and auditing

### Metadata Management
- ✅ Parse and store RDF/XML metadata for all ebooks
- ✅ Searchable local SQLite catalog database
- ✅ Language mapping (names ↔ ISO 639-1 codes)
- ✅ Full-text search across titles and authors
- ✅ Export catalog to JSON/CSV

### RAG Preparation
- ✅ Extract plain text from downloaded archives
- ✅ Strip Project Gutenberg headers/footers
- ✅ Normalize text encoding to UTF-8
- ✅ Chunk text into configurable segments
- ✅ **Full metadata embedded in each chunk** (book ID, title, authors, language, subjects, chunk index)
- ✅ **Dry-run mode** to preview extractions
- ✅ Multiple output formats (JSON, TXT)

### Additional Features
- ✅ Real-time progress tracking
- ✅ **Health/status monitoring** command
- ✅ **Configuration validation** on startup
- ✅ **File integrity auditing** (scan and verify)
- ✅ Comprehensive error handling and retry logic
- ✅ Structured logging
- ✅ Configuration via JSON with environment variable overrides

## Requirements

- **.NET 9.0** or later
- **rsync 3.0+** (auto-detected on Linux/macOS, WSL/Cygwin on Windows)
- **Storage**: 
  - Text-only (zipped): 8-15GB
  - Text + EPUB: ~50GB
  - Full archive: 800GB - 1TB

## Installation

### Install .NET 9.0

**Linux/macOS:**
```bash
# Visit https://dotnet.microsoft.com/download
# Or use package manager:
# Ubuntu/Debian
wget https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh
bash dotnet-install.sh --channel 9.0

# macOS
brew install --cask dotnet
```

**Windows:**
Download and install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)

### Install rsync

**Linux:**
```bash
sudo apt-get install rsync  # Debian/Ubuntu
sudo yum install rsync      # RHEL/CentOS
```

**macOS:**
```bash
brew install rsync
```

**Windows:**
GutenbergSync will auto-detect rsync in:
- Windows Subsystem for Linux (WSL): Install with `wsl --install` then `sudo apt-get install rsync`
- Cygwin: Download from [cygwin.com](https://www.cygwin.com/) and select rsync package
- Native Windows: Use [cwRsync](https://www.itefix.net/cwrsync) or similar

If rsync is not found, the application will provide platform-specific installation instructions.

### Build GutenbergSync

**Option 1: Using the publish script (recommended)**
```bash
git clone <repository-url>
cd GutenbergSync
./publish.sh
```

The script builds a self-contained deployment by default. To customize:
```bash
# Framework-dependent (requires .NET runtime installed)
SELF_CONTAINED=false ./publish.sh

# Different runtime
RUNTIME=win-x64 ./publish.sh
RUNTIME=osx-x64 ./publish.sh
```

**Option 2: Manual publish**

Self-contained (includes .NET runtime, ~146MB):
```bash
dotnet publish src/GutenbergSync.Cli/GutenbergSync.Cli.csproj \
    -c Release \
    -o ./publish \
    --self-contained true \
    -r linux-x64
```

Framework-dependent (requires .NET 9.0 runtime, ~75KB):
```bash
dotnet publish src/GutenbergSync.Cli/GutenbergSync.Cli.csproj \
    -c Release \
    -o ./publish \
    --self-contained false
```

**Run the application:**
```bash
./publish/gutenberg-sync --help
```

## Quick Start

### 1. Initial Setup

Create a configuration file:

```bash
gutenberg-sync config init
```

This creates a default `config.json` file. Edit it to set your target directory:

```json
{
  "Sync": {
    "TargetDirectory": "/mnt/workspace/gutenberg"
  },
  "Catalog": {
    "DatabasePath": null
  }
}
```

### 2. Sync Raw Content

Download the Project Gutenberg archive:

```bash
# Text-only (smallest, ~15GB)
gutenberg-sync sync -t /mnt/workspace/gutenberg -p text-only

# Text + EPUB (recommended for RAG, ~50GB)
gutenberg-sync sync -t /mnt/workspace/gutenberg -p text-epub

# Full archive (~1TB)
gutenberg-sync sync -t /mnt/workspace/gutenberg -p full
```

The sync process uses a **metadata-first strategy**:
- **Phase 1**: Downloads RDF metadata files and builds the catalog
- **Phase 2**: Downloads content files using the catalog as a checklist

**Auto-Retry on Interruption**: If the sync is interrupted (network error, system crash, etc.), you can use auto-retry to automatically resume:

```bash
# Auto-retry with infinite retries (recommended for long downloads)
gutenberg-sync sync -t /mnt/workspace/gutenberg -p text-epub --auto-retry

# Auto-retry with maximum retry limit
gutenberg-sync sync -t /mnt/workspace/gutenberg -p text-epub --auto-retry --max-retries 10 --retry-delay 30

# Or use the wrapper script
./sync-with-retry.sh -t /mnt/workspace/gutenberg -p text-epub
```

The sync automatically resumes from where it stopped - rsync's built-in resume handles partial files efficiently. Auto-retry ensures the process restarts automatically if interrupted.

**Running in Background**: For long syncs that need to survive screen locks or terminal disconnections, see the [Running in Background](#running-in-background) section below.

**Important**: Content sync now has **no timeout by default** (runs indefinitely). Use `--timeout <seconds>` if you need to set a timeout.

### 3. Extract RAG-Ready Chunks

Process the downloaded files into RAG-ready chunks:

```bash
# Basic extraction
gutenberg-sync extract \
  -i /mnt/workspace/gutenberg \
  -o /mnt/workspace/gutenberg-rag \
  --chunk-size 500 \
  --chunk-overlap 50 \
  --format json
```

**Preview Before Extracting**: Use `--dry-run` to see what would be extracted:

```bash
gutenberg-sync extract \
  -i /mnt/workspace/gutenberg \
  -o /mnt/workspace/gutenberg-rag \
  --dry-run
```

This creates JSON files with embedded metadata in each chunk:

```json
{
  "chunkIndex": 0,
  "totalChunks": 45,
  "book": {
    "gutenbergId": 1342,
    "title": "Pride and Prejudice",
    "authors": [{"name": "Austen, Jane"}],
    "language": "English",
    "languageIsoCode": "en",
    "subjects": ["Fiction", "Love stories"]
  },
  "text": "It is a truth universally acknowledged..."
}
```

### 4. Search the Catalog

Query your local catalog:

```bash
# Search by title
gutenberg-sync catalog search "sherlock" --limit 10

# Filter by language (accepts both names and ISO codes)
gutenberg-sync catalog search --language en
gutenberg-sync catalog search --language "English"

# Filter by author
gutenberg-sync catalog search --author "Shakespeare"

# Filter by subject
gutenberg-sync catalog search --subject "Fiction"
```

### 5. Web Application

GutenbergSync includes a web UI for browsing the catalog, monitoring sync progress, and copying EPUBs.

**Starting the Web Server:**

```bash
# From the project root directory
cd /home/jfinlon/Documents/Projects/GutenbergSync
dotnet run --project src/GutenbergSync.Web/GutenbergSync.Web.csproj --urls "http://localhost:5001"
```

The web application will be available at `http://localhost:5001`.

**Important: Killing Old Processes Before Restarting**

If you encounter "address already in use" errors or the server isn't responding, kill any existing processes first:

```bash
# Kill all GutenbergSync processes
pkill -9 -f "dotnet.*GutenbergSync"

# Wait a moment for processes to terminate
sleep 2

# Then start the server
cd /home/jfinlon/Documents/Projects/GutenbergSync
dotnet run --project src/GutenbergSync.Web/GutenbergSync.Web.csproj --urls "http://localhost:5001"
```

**One-liner to kill and restart:**

```bash
pkill -9 -f "dotnet.*GutenbergSync" 2>/dev/null; pkill -9 -f "GutenbergSync.Web" 2>/dev/null; pkill -9 -f "Gutenberg" 2>/dev/null; sleep 3; cd /home/jfinlon/Documents/Projects/GutenbergSync && dotnet run --project src/GutenbergSync.Web/GutenbergSync.Web.csproj --urls "http://localhost:5001"
```

**Troubleshooting "address already in use" errors:**

If you still get "address already in use" errors, try these steps in order:

1. **Kill all related processes (including compiled executables):**
```bash
pkill -9 -f "dotnet.*GutenbergSync" 2>/dev/null
pkill -9 -f "GutenbergSync.Web" 2>/dev/null
pkill -9 -f "Gutenberg" 2>/dev/null
pkill -9 -f "dotnet" 2>/dev/null
sleep 3
```

2. **Check what's using port 5001:**
```bash
lsof -i :5001 || netstat -tulpn | grep :5001 || ss -tulpn | grep :5001
```

3. **Kill the specific process using port 5001 (if found):**
```bash
# Replace PID with the process ID from step 2
kill -9 <PID>
# Or use fuser to kill by port (if available):
fuser -k 5001/tcp 2>/dev/null
sleep 2
```

4. **Then start the server:**
```bash
cd /home/jfinlon/Documents/Projects/GutenbergSync
dotnet run --project src/GutenbergSync.Web/GutenbergSync.Web.csproj --urls "http://localhost:5001"
```

**Alternative: Use a different port**

If port 5001 is persistently unavailable, you can use a different port:
```bash
dotnet run --project src/GutenbergSync.Web/GutenbergSync.Web.csproj --urls "http://localhost:5002"
```

**Web Application Features:**

- **Search Catalog**: Search ebooks by title, author, language, or subject
- **Sync Status**: Monitor sync progress in real-time with progress bars and file counts
- **Copy EPUBs**: Select multiple books from search results and copy them to a chosen folder with formatted filenames (`{Author Name}-{Book Title}.epub`)
- **Statistics**: View catalog statistics (total books, authors, languages, subjects)

The web application automatically finds `config.json` by searching:
1. Current working directory
2. Parent directories (useful when running from `bin/Debug/net9.0`)
3. Hardcoded absolute path as fallback

## Configuration

### Configuration File

Create a `config.json` file:

```json
{
  "Sync": {
    "TargetDirectory": "/mnt/workspace/gutenberg",
    "Preset": "text-epub",
    "Mirrors": [
      {
        "Host": "aleph.gutenberg.org",
        "Module": "gutenberg",
        "Priority": 1,
        "Region": "US"
      },
      {
        "Host": "aleph.gutenberg.org",
        "Module": "gutenberg-epub",
        "Priority": 1,
        "Region": "US"
      }
    ],
    "Include": ["*/", "*.txt", "*.epub", "*.zip"],
    "Exclude": ["*/old/*", "*.mp3", "*.ogg"],
    "MaxFileSizeMb": 50,
    "BandwidthLimitKbps": null,
    "DeleteRemoved": false,
    "TimeoutSeconds": 600
  },
  "Catalog": {
    "DatabasePath": null,
    "AutoRebuildOnSync": true,
    "VerifyAfterSync": true,
    "AuditScanIntervalDays": 7
  },
  "Extraction": {
    "OutputDirectory": null,
    "StripHeaders": true,
    "NormalizeEncoding": true,
    "DefaultChunkSizeWords": 500,
    "DefaultChunkOverlapWords": 50,
    "Incremental": true,
    "ValidateChunks": true,
    "DefaultFormat": "json",
    "CompressOutput": false
  },
  "Logging": {
    "Level": "Information",
    "FilePath": null,
    "RetainDays": 30
  }
}
```

### Database Path

The catalog database path is resolved as:
1. Explicit config: `Catalog.DatabasePath` (absolute or relative)
2. Environment variable: `GUTENBERG_CATALOG_DATABASE_PATH`
3. Default: `{Sync.TargetDirectory}/gutenberg.db`

### Environment Variables

All configuration values can be overridden:

```bash
export GUTENBERG_SYNC_TARGET_DIRECTORY=/mnt/workspace/gutenberg
export GUTENBERG_SYNC_BANDWIDTH_LIMIT_KBPS=5000
export GUTENBERG_CATALOG_DATABASE_PATH=/var/lib/gutenberg/catalog.db
```

## Usage Examples

### Sync Operations

```bash
# Initial sync with text-only preset
gutenberg-sync sync -t /mnt/workspace/gutenberg -p text-only
# Initial sync with text-epub preset
gutenberg-sync sync -t /mnt/workspace/gutenberg -p text-epub

dotnet run --project src/GutenbergSync.Cli/GutenbergSync.Cli.csproj -- sync --preset text-epub --target-dir /mnt/workspace/gutenberg --auto-retry

# Incremental update
gutenberg-sync sync -t /mnt/workspace/gutenberg

# Metadata-only sync (Phase 1 only)
gutenberg-sync sync -t /mnt/workspace/gutenberg --metadata-only

# Dry run (see what would be transferred)
gutenberg-sync sync -t /mnt/workspace/gutenberg --dry-run

# Verify files after sync
gutenberg-sync sync -t /mnt/workspace/gutenberg --verify
```

### Extraction Operations

```bash
# Extract with default settings
gutenberg-sync extract -i /mnt/workspace/gutenberg -o /data/rag-output

# Extract with custom chunking
gutenberg-sync extract \
  -i /mnt/workspace/gutenberg \
  -o /data/rag-output \
  --chunk-size 1000 \
  --chunk-overlap 100

# Extract to TXT format
gutenberg-sync extract \
  -i /mnt/workspace/gutenberg \
  -o /data/rag-output \
  --format txt

# Dry-run to preview what would be extracted
gutenberg-sync extract \
  -i /mnt/workspace/gutenberg \
  -o /data/rag-output \
  --dry-run
```

### Catalog Operations

```bash
# Search catalog
gutenberg-sync catalog search --query "pride and prejudice"

# Search with filters
gutenberg-sync catalog search --query "sherlock" --author "Doyle" --language en --limit 10

# Show statistics
gutenberg-sync catalog stats
```

### Health and Status

```bash
# Check system health
gutenberg-sync health
```

Shows:
- rsync availability and version
- Catalog database status (book count, author count)

### Configuration Management

```bash
# Initialize default configuration file
gutenberg-sync config init

# Initialize with custom path
gutenberg-sync config init --path ./my-config.json

# Validate configuration file
gutenberg-sync config validate --config ./config.json
```

### Audit Operations

```bash
# Scan directory for missing or corrupt files
gutenberg-sync audit scan --directory /mnt/workspace/gutenberg

# Verify files against catalog
gutenberg-sync audit verify
```

## Architecture

```
┌─────────────────────────────────────────┐
│         GutenbergSync CLI                │
├─────────────────────────────────────────┤
│  Sync  │  Metadata  │  Extract  │ Catalog│
└────┬───────┬───────────┬───────────┬─────┘
     │       │           │           │
┌────┴───────┴───────────┴───────────┴─────┐
│         Core Services Layer              │
│  Rsync │  RDF Parser │  Text Proc │ SQLite│
└────┬───────┬───────────┬───────────┬─────┘
     │       │           │           │
┌────┴─────────────────────────────────────┐
│      Infrastructure Layer                 │
│  Process │  File Sys │  Compression │ DB │
└──────────────────────────────────────────┘
```

### Key Components

- **RsyncService**: Wraps rsync binary with progress tracking and retry logic
- **RsyncDiscoveryService**: Auto-detects rsync on all platforms
- **RdfParser**: Parses Project Gutenberg RDF/XML metadata
- **LanguageMapper**: Maps language names to ISO 639-1 codes
- **TextExtractor**: Extracts and chunks text with metadata
- **CatalogRepository**: SQLite-based catalog with full-text search
- **ConfigurationValidator**: Validates configuration on startup
- **AuditService**: Verifies file integrity and detects issues

## Project Statistics

- **~76,000+ ebooks** as of 2024/2025
- **~2 million files** across all formats
- **60+ languages** (majority English)
- **Size estimates**:
  - Full archive with audio: 800GB - 1TB
  - Text-only (zipped): 8-15GB
  - Text + EPUB: ~50GB

## Official rsync Endpoints

| Endpoint | Content | Structure |
|----------|---------|-----------|
| `aleph.gutenberg.org::gutenberg` | Main collection | Hierarchical (1/2/3/4/12345/) |
| `aleph.gutenberg.org::gutenberg-epub` | Generated formats | Flat (epub/12345/) |
| `ftp.ibiblio.org::gutenberg` | Mirror (main) | Hierarchical |
| `rsync.mirrorservice.org::gutenberg.org` | UK Mirror (full) | Mixed |

## Two-Phase Workflow Explained

### Phase 1: Mirror Raw Content

The `sync` command downloads the complete Project Gutenberg archive to your local filesystem:

1. **Metadata Sync**: Downloads RDF files from `cache/epub/` directories
2. **Catalog Building**: Parses RDF files and builds searchable SQLite catalog
3. **Content Sync**: Downloads actual book files (txt, epub, html, etc.) using catalog as checklist

**Benefits:**
- Complete local archive for offline access
- Original file formats preserved
- Incremental updates (only changed files)
- Catalog available early for search/query

### Phase 2: Create RAG-Ready Chunks

The `extract` command processes the raw files into RAG-ready format:

1. **Text Extraction**: Reads raw files, handles zip archives
2. **Header/Footer Stripping**: Removes Project Gutenberg license text
3. **Encoding Normalization**: Converts to UTF-8
4. **Chunking**: Splits text into configurable segments
5. **Metadata Embedding**: Adds full book metadata to each chunk

**Output Formats:**
- **JSON** (default): Standard JSON with embedded metadata, ready for vector databases
- **TXT**: Plain text format

**Benefits:**
- Clean text without headers/footers
- Configurable chunk sizes for optimal embedding
- Full metadata for attribution and filtering

## Running in Background

When running long sync operations, you want the process to continue even if:
- Your screen locks
- Your terminal disconnects
- Your SSH session ends
- Your laptop goes to sleep (if on a server)

### Solution 1: screen (Recommended)

Use `screen` to create a detachable session:

```bash
# Start a new screen session
screen -S gutenberg-sync

# Run your sync command
cd /home/jfinlon/Documents/Projects/GutenbergSync
dotnet run --project src/GutenbergSync.Cli/GutenbergSync.Cli.csproj -- sync --preset text-epub --target-dir /mnt/workspace/gutenberg --auto-retry

# Detach: Press Ctrl+A, then D
# Reattach: screen -r gutenberg-sync
```

**Useful screen commands:**
```bash
# List sessions
screen -ls

# Attach to session
screen -r gutenberg-sync

# Create new session with name
screen -S my-sync

# Detach: Ctrl+A then D
# Kill session: Ctrl+A then K (or exit normally)
```

**Or start detached:**
```bash
cd /home/jfinlon/Documents/Projects/GutenbergSync && screen -dmS gutenberg-sync bash -c "dotnet run --project src/GutenbergSync.Cli/GutenbergSync.Cli.csproj -- sync --preset text-epub --target-dir /mnt/workspace/gutenberg --auto-retry; exec bash"
```

### Solution 2: nohup (Simplest)

Run the command with `nohup` to ignore hangup signals:

```bash
# Run with nohup and redirect output
nohup dotnet run --project src/GutenbergSync.Cli/GutenbergSync.Cli.csproj -- sync --preset text-epub --target-dir /mnt/workspace/gutenberg --auto-retry > sync.log 2>&1 &

# Check progress
tail -f sync.log

# Check if still running
ps aux | grep GutenbergSync
```

### Solution 3: tmux (Advanced)

Similar to screen but more powerful:

```bash
# Start tmux session
tmux new -s gutenberg-sync

# Run your command
dotnet run --project src/GutenbergSync.Cli/GutenbergSync.Cli.csproj -- sync --preset text-epub --target-dir /mnt/workspace/gutenberg --auto-retry

# Detach: Ctrl+B then D
# Reattach: tmux attach -t gutenberg-sync
```

### Important Notes

1. **Use `--auto-retry`**: This ensures the sync restarts if interrupted
2. **No timeout by default**: Content sync has no timeout (runs indefinitely)
3. **Resume is automatic**: rsync automatically resumes from where it stopped

## Resume Behavior

GutenbergSync supports smart resume! When you restart a sync after a failure, rsync automatically:

### Incremental Sync (Built-in)
- **Skips files that already exist** and are up-to-date (same size and modification time)
- Only transfers **new files** or **changed files**
- This is rsync's default behavior - no re-downloading of completed files

### Partial File Resume
- **Resumes interrupted file transfers** using `--partial` flag
- rsync's **delta-transfer algorithm** automatically handles resume:
  - Compares checksums of partial file with source
  - Only transfers the missing portions
  - More efficient than re-transferring entire files
- Partial files are stored in `.rsync-partial/` directory
- On resume, rsync **continues from where it left off** for partially downloaded files

### How It Works

**First sync:**
```bash
dotnet run --project src/GutenbergSync.Cli/GutenbergSync.Cli.csproj -- sync --preset text-epub --target-dir /mnt/workspace/gutenberg
# Downloads all files
```

**If interrupted (Ctrl+C, network error, etc.):**
- Files already downloaded are **kept**
- Partially downloaded files are saved in `.rsync-partial/`

**Resume (run the same command again):**
```bash
dotnet run --project src/GutenbergSync.Cli/GutenbergSync.Cli.csproj -- sync --preset text-epub --target-dir /mnt/workspace/gutenberg
# Only downloads:
#   - Files that weren't downloaded yet
#   - Files that were partially downloaded (resumes from where it stopped)
#   - Files that changed on the server
# Skips files that are already complete and up-to-date
```

### Best Practices

- **Safe to interrupt**: You can stop a sync at any time (Ctrl+C) and resume later
- **Safe to restart**: Running the same sync command multiple times is safe and efficient
- **No manual cleanup needed**: Partial files are automatically handled
- **Network interruptions**: Automatically handled - just restart the sync

## Troubleshooting

### Permission Issues

The application needs write access to the target directory to create the database file (`gutenberg.db`).

**Quick Fix:**
```bash
# Create the directory with proper permissions
sudo mkdir -p /mnt/workspace/gutenberg && \
sudo chown -R $USER:$USER /mnt/workspace/gutenberg && \
sudo chmod -R 755 /mnt/workspace/gutenberg
```

**Verify Permissions:**
```bash
# Test write access
touch /mnt/workspace/gutenberg/test.txt && rm /mnt/workspace/gutenberg/test.txt && echo "✓ Write access OK"
```

**Alternative: Use a Different Database Location**

If you want to keep the sync directory at `/mnt/workspace/gutenberg` but put the database elsewhere:

```bash
# Create config
dotnet run --project src/GutenbergSync.Cli/GutenbergSync.Cli.csproj -- config init

# Edit config.json to add:
{
  "Sync": {
    "TargetDirectory": "/mnt/workspace/gutenberg"
  },
  "Catalog": {
    "DatabasePath": "/home/$USER/.gutenberg-sync/gutenberg.db"
  }
}

# Create the database directory
mkdir -p ~/.gutenberg-sync
```

### Web Server Issues

**"Address already in use" errors:**

1. Kill all related processes:
```bash
pkill -9 -f "dotnet.*GutenbergSync" 2>/dev/null
pkill -9 -f "GutenbergSync.Web" 2>/dev/null
sleep 3
```

2. Check what's using the port:
```bash
lsof -i :5001 || netstat -tulpn | grep :5001 || ss -tulpn | grep :5001
```

3. Kill the specific process or use a different port:
```bash
# Use different port
dotnet run --project src/GutenbergSync.Web/GutenbergSync.Web.csproj --urls "http://localhost:5002"
```

### Sync Issues

**Process keeps stopping:**
- Check system limits: `ulimit -a`
- Check disk space: `df -h`
- Check network connection
- Use `--auto-retry` to automatically restart on failure

**Can't reattach to screen:**
```bash
# List all screen sessions
screen -ls

# Force attach (if session is attached elsewhere)
screen -r -d gutenberg-sync

# Kill stuck session
screen -X -S gutenberg-sync quit
```

## Compliance

- ✅ Respects Project Gutenberg's Terms of Use
- ✅ Uses official mirror infrastructure only (no website scraping)
- ✅ Implements respectful request patterns (rate limiting, backoff)
- ✅ Includes appropriate attribution in outputs

## Web API Reference

The web application provides the following API endpoints:

- `GET /api/Api/statistics` - Get catalog statistics (total books, authors, languages, subjects)
- `POST /api/Api/search` - Search the catalog (request body: `{ query, author, language, limit, offset }`)
- `POST /api/Api/sync/start` - Start a sync operation
- `POST /api/Api/epub/copy` - Copy an EPUB file (request body: `{ bookId, destinationPath }`)
- `GET /api/Api/browse?path=<directory>` - Browse directories (limited to `/home/jfinlon`)

**SignalR Hub:**
- `/hubs/sync` - Real-time sync progress updates
  - `ProgressUpdate` - Progress information
  - `SyncComplete` - Sync completion notification
  - `SyncError` - Error notifications

## Documentation

- [Requirements Document](docs/REQUIREMENTS.md) - Detailed functional and non-functional requirements
- [Technical Specification](docs/TECHNICAL_SPEC.md) - Architecture, API design, and implementation details

## License

[To be determined - specify license]

## Contributing

[To be added - contribution guidelines]

## Support

For issues, questions, or contributions, please [open an issue](link-to-issues) or [start a discussion](link-to-discussions).

---

**Note**: This project adheres to Project Gutenberg's official mirroring policies. See their [Mirroring How-To](https://www.gutenberg.org/help/mirroring.html) for more information.

