# Project Gutenberg Archive Tool - Technical Specification

**Version:** 1.2  
**Date:** December 8, 2025  
**Project:** GutenbergSync

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Technology Stack](#2-technology-stack)
3. [Component Design](#3-component-design)
4. [Data Models](#4-data-models)
5. [Synchronization Strategy](#5-synchronization-strategy)
6. [API Design](#6-api-design)
7. [Configuration](#7-configuration)
8. [Error Handling](#8-error-handling)
9. [Testing Strategy](#9-testing-strategy)
10. [Deployment](#10-deployment)

---

## 1. Architecture Overview

### 1.1 High-Level Architecture

```
+---------------------------------------------------------------------+
|                        GutenbergSync CLI                            |
+---------------------------------------------------------------------+
|  +-------------+  +-------------+  +-------------+  +------------+  |
|  |   Sync      |  |  Metadata   |  |  Extractor  |  |  Catalog   |  |
|  |   Engine    |  |  Parser     |  |  Service    |  |  Query     |  |
|  +------+------+  +------+------+  +------+------+  +-----+------+  |
|         |                |                |                |        |
+---------|----------------|----------------|----------------|--------+
|                        Core Services Layer                          |
|  +-------------+  +-------------+  +-------------+  +------------+  |
|  |   Rsync     |  |   RDF       |  |   Text      |  |  SQLite    |  |
|  |   Wrapper   |  |   Parser    |  |   Processor |  |  Repository|  |
|  +------+------+  +------+------+  +------+------+  +-----+------+  |
|         |                |                |                |        |
+---------|----------------|----------------|----------------|--------+
|                        Infrastructure Layer                         |
|  +-------------+  +-------------+  +-------------+  +------------+  |
|  |  Process    |  |  File       |  |  Compression|  |  Database  |  |
|  |  Execution  |  |  System     |  |  (Zip/GZip) |  |  (SQLite)  |  |
|  +-------------+  +-------------+  +-------------+  +------------+  |
+---------------------------------------------------------------------+
                                    |
                                    v
+---------------------------------------------------------------------+
|                    External Systems                                 |
|  +---------------------+       +--------------------------------+   |
|  |  Project Gutenberg  |       |       Local File System        |   |
|  |  rsync Mirrors      |       |  +----------+  +------------+  |   |
|  |                     |       |  | Archive  |  |  Database  |  |   |
|  |  - aleph.gutenberg  |       |  |  Files   |  |   (.db)    |  |   |
|  |  - ftp.ibiblio.org  |       |  +----------+  +------------+  |   |
|  +---------------------+       +--------------------------------+   |
+---------------------------------------------------------------------+
```

### 1.2 Design Principles

1. **rsync-First Approach**: Leverage rsync's delta-transfer algorithm for efficient synchronization
2. **Separation of Concerns**: Clear boundaries between sync, metadata, extraction, and storage
3. **Configuration-Driven**: All behaviors configurable without code changes
4. **Idempotent Operations**: Safe to re-run any operation without side effects
5. **Cross-Platform**: Abstract platform-specific details (rsync availability with auto-detection)
6. **Metadata-First Strategy**: Build catalog from RDF files before syncing content, enabling checklist-based sync
7. **Concurrent Safety**: Support concurrent operations with verification and auditing mechanisms

---

## 2. Technology Stack

### 2.1 Runtime and Framework

| Component | Technology | Rationale |
|-----------|------------|-----------|
| Runtime | .NET 8.0 LTS | Long-term support, cross-platform, performance |
| Language | C# 12 | Modern language features, strong typing |
| Project Type | Console Application + Class Library | CLI tool with reusable library |

### 2.2 Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `System.CommandLine` | 2.0+ | CLI argument parsing |
| `Microsoft.Data.Sqlite` | 8.0+ | SQLite database access |
| `Dapper` | 2.1+ | Micro-ORM for database queries |
| `Serilog` | 3.1+ | Structured logging |
| `Serilog.Sinks.Console` | 5.0+ | Console output |
| `Serilog.Sinks.File` | 5.0+ | File logging |
| `System.IO.Compression` | (built-in) | Zip file handling |
| `SharpZipLib` | 1.4+ | Advanced compression support |
| `Microsoft.Extensions.Configuration` | 8.0+ | Configuration management |
| `Microsoft.Extensions.DependencyInjection` | 8.0+ | DI container |
| `Spectre.Console` | 0.48+ | Rich console output, progress bars |
| `Polly` | 8.0+ | Retry policies, resilience |

### 2.3 External Dependencies

| Tool | Requirement | Notes |
|------|-------------|-------|
| rsync | 3.0+ | Auto-detected: Linux/macOS native, Windows via WSL/Cygwin. Application provides installation instructions if missing. |

---

## 3. Component Design

### 3.1 Solution Structure

```
GutenbergSync/
├── src/
│   ├── GutenbergSync.Cli/              # Console application
│   │   ├── Commands/
│   │   │   ├── SyncCommand.cs
│   │   │   ├── CatalogCommand.cs
│   │   │   ├── ExtractCommand.cs
│   │   │   ├── ConfigCommand.cs
│   │   │   ├── HealthCommand.cs
│   │   │   └── DatabaseCommand.cs
│   │   ├── Program.cs
│   │   └── GutenbergSync.Cli.csproj
│   │
│   ├── GutenbergSync.Core/             # Core library
│   │   ├── Sync/
│   │   │   ├── IRsyncService.cs
│   │   │   ├── RsyncService.cs
│   │   │   ├── RsyncOptions.cs
│   │   │   ├── SyncProgress.cs
│   │   │   ├── SyncResult.cs
│   │   │   ├── IRsyncDiscoveryService.cs
│   │   │   ├── RsyncDiscoveryService.cs
│   │   │   ├── IAuditService.cs
│   │   │   └── AuditService.cs
│   │   ├── Metadata/
│   │   │   ├── IRdfParser.cs
│   │   │   ├── RdfParser.cs
│   │   │   ├── EbookMetadata.cs
│   │   │   ├── Author.cs
│   │   │   ├── ILanguageMapper.cs
│   │   │   └── LanguageMapper.cs
│   │   ├── Extraction/
│   │   │   ├── ITextExtractor.cs
│   │   │   ├── TextExtractor.cs
│   │   │   ├── GutenbergHeaderStripper.cs
│   │   │   ├── TextChunker.cs
│   │   │   ├── ChunkMetadata.cs
│   │   │   ├── IExtractionStateTracker.cs
│   │   │   ├── ExtractionStateTracker.cs
│   │   │   ├── IChunkValidator.cs
│   │   │   └── ChunkValidator.cs
│   │   ├── Catalog/
│   │   │   ├── ICatalogRepository.cs
│   │   │   ├── SqliteCatalogRepository.cs
│   │   │   ├── CatalogSearchOptions.cs
│   │   │   ├── IDatabaseMaintenanceService.cs
│   │   │   └── DatabaseMaintenanceService.cs
│   │   ├── Configuration/
│   │   │   ├── SyncConfiguration.cs
│   │   │   ├── MirrorEndpoint.cs
│   │   │   ├── IConfigurationValidator.cs
│   │   │   └── ConfigurationValidator.cs
│   │   └── GutenbergSync.Core.csproj
│   │
│   └── GutenbergSync.Tests/            # Unit tests
│       ├── Sync/
│       ├── Metadata/
│       ├── Extraction/
│       └── GutenbergSync.Tests.csproj
│
├── docs/
│   ├── REQUIREMENTS.md
│   └── TECHNICAL_SPEC.md
│
├── samples/
│   └── config.json
│
├── .gitignore
├── README.md
├── LICENSE
└── GutenbergSync.sln
```

### 3.2 Component Responsibilities

#### 3.2.1 RsyncService

Wraps rsync binary execution with .NET-friendly interface.

```csharp
public interface IRsyncService
{
    Task<SyncResult> SyncAsync(
        RsyncOptions options, 
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    Task<bool> IsAvailableAsync();
    
    Task<IReadOnlyList<FileInfo>> GetRemoteFileListAsync(
        string endpoint,
        CancellationToken cancellationToken = default);
}
```

**Key Implementation Details:**
- Uses `Process` class to invoke rsync
- Parses rsync stdout for progress information
- Auto-detects rsync via `RsyncDiscoveryService` (checks PATH, WSL, Cygwin on Windows)
- Provides platform-specific installation instructions if rsync is missing
- Implements retry logic via Polly for transient failures
- Supports concurrent-safe operations with file-level locking

#### 3.2.2 RdfParser

Parses Project Gutenberg RDF/XML metadata files.

```csharp
public interface IRdfParser
{
    Task<EbookMetadata> ParseFileAsync(string rdfFilePath);
    
    IAsyncEnumerable<EbookMetadata> ParseDirectoryAsync(
        string cacheEpubDirectory,
        CancellationToken cancellationToken = default);
    
    Task<EbookMetadata> ParseXmlAsync(Stream rdfStream);
}
```

**Key Implementation Details:**
- Uses `System.Xml.Linq` for XML parsing
- Handles RDF namespaces: `pgterms`, `dcterms`, `rdf`, `dcam`
- Extracts: id, title, authors, subjects, languages, formats, rights
- Maps language names to ISO 639-1 codes via `LanguageMapper`
- Accepts both language names and ISO codes, stores both in database

#### 3.2.3 TextExtractor

Extracts and normalizes text content from Gutenberg files.

```csharp
public interface ITextExtractor
{
    Task<ExtractionResult> ExtractAsync(
        string sourceFilePath,
        TextExtractionOptions options);
    
    Task ExtractBatchAsync(
        IEnumerable<string> sourceFiles,
        string outputDirectory,
        TextExtractionOptions options,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    Task<ExtractionPreview> PreviewExtractionAsync(
        IEnumerable<string> sourceFiles,
        TextExtractionOptions options,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<ExtractionResult>> ExtractSelectiveAsync(
        CatalogSearchOptions searchOptions,
        string outputDirectory,
        TextExtractionOptions extractionOptions,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
```

**Key Implementation Details:**
- Handles .txt, .zip (containing .txt), .html formats
- Strips Gutenberg header/footer markers
- Normalizes encoding to UTF-8
- Optional chunking for RAG preparation
- Each chunk includes full metadata: book ID, title, authors, language, subjects, chunk index, total chunks
- Output format supports JSON, Parquet, Arrow, compressed JSON (gzip/brotli)
- Incremental extraction: Checks extraction_history table to skip already-extracted files
- Selective extraction: Uses catalog queries to determine which files to extract
- Quality validation: Validates chunks (non-empty, reasonable length, encoding issues)
- Dry-run mode: Preview extraction without processing files

**Chunk Metadata Structure:**
```csharp
public sealed record TextChunk
{
    public required int ChunkIndex { get; init; }
    public required int TotalChunks { get; init; }
    public required BookMetadata BookMetadata { get; init; }
    public required string Text { get; init; }
}

public sealed record BookMetadata
{
    public required int GutenbergId { get; init; }
    public required string Title { get; init; }
    public IReadOnlyList<Author> Authors { get; init; } = [];
    public required string Language { get; init; }
    public string? LanguageIsoCode { get; init; }
    public IReadOnlyList<string> Subjects { get; init; } = [];
    public DateOnly? ReleaseDate { get; init; }
}
```

#### 3.2.4 RsyncDiscoveryService

Auto-detects rsync binary availability across platforms.

```csharp
public interface IRsyncDiscoveryService
{
    Task<RsyncDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default);
    string GetInstallationInstructions(Platform platform);
}

public sealed record RsyncDiscoveryResult
{
    public bool IsAvailable { get; init; }
    public string? ExecutablePath { get; init; }
    public Platform Platform { get; init; }
    public RsyncSource Source { get; init; }  // Native, WSL, Cygwin, PATH
    public string? Version { get; init; }
    public string? InstallationInstructions { get; init; }
}
```

**Key Implementation Details:**
- Checks PATH for rsync executable
- On Windows: Detects WSL (`wsl which rsync`) and Cygwin installations
- On Linux/macOS: Checks standard locations
- Provides platform-specific installation instructions when missing
- Returns executable path for use by RsyncService

#### 3.2.5 AuditService

Verifies file integrity and audits archive for issues.

```csharp
public interface IAuditService
{
    Task VerifyFileAsync(string filePath, int gutenbergId, CancellationToken ct);
    Task<IReadOnlyList<AuditIssue>> ScanForIssuesAsync(string archiveDirectory, CancellationToken ct);
    Task<bool> VerifyChecksumAsync(string filePath, string expectedChecksum);
    Task<AuditStatistics> GetStatisticsAsync(string archiveDirectory, CancellationToken ct);
}
```

**Key Implementation Details:**
- Calculates SHA-256 checksums for file verification
- Compares file sizes against catalog metadata
- Detects missing, corrupt, or orphaned files
- Logs audit results to database
- Supports background scanning for large archives

#### 3.2.6 LanguageMapper

Maps between language names and ISO 639-1 codes.

```csharp
public interface ILanguageMapper
{
    string? GetIsoCode(string languageName);
    string? GetLanguageName(string isoCode);
    bool TryMap(string input, out string isoCode, out string languageName);
}
```

**Key Implementation Details:**
- Maintains bidirectional mapping dictionary
- Handles common language variations
- Returns both name and code when available
- Used by RdfParser and CatalogRepository

#### 3.2.7 CatalogRepository

Persists and queries ebook metadata.

```csharp
public interface ICatalogRepository
{
    Task InitializeAsync();
    
    Task UpsertAsync(EbookMetadata metadata);
    Task UpsertBatchAsync(IEnumerable<EbookMetadata> items);
    
    Task<EbookMetadata?> GetByIdAsync(int gutenbergId);
    Task<IReadOnlyList<EbookMetadata>> SearchAsync(CatalogSearchOptions options);
    
    Task<CatalogStatistics> GetStatisticsAsync();
    
    Task ExportToCsvAsync(string outputPath);
    Task ExportToJsonAsync(string outputPath);
}
```

#### 3.2.8 ExtractionStateTracker

Tracks extraction history and enables incremental extraction.

```csharp
public interface IExtractionStateTracker
{
    Task<ExtractionState?> GetLastExtractionAsync(int gutenbergId, string outputDirectory, string parametersHash);
    Task RecordExtractionAsync(ExtractionRecord record);
    Task<IReadOnlyList<int>> GetExtractedBookIdsAsync(string outputDirectory);
    Task<bool> NeedsExtractionAsync(int gutenbergId, string sourcePath, string outputDirectory, TextExtractionOptions options);
}

public sealed record ExtractionRecord
{
    public required int GutenbergId { get; init; }
    public required string SourceFilePath { get; init; }
    public required string OutputDirectory { get; init; }
    public required string ParametersHash { get; init; }
    public required TextExtractionOptions Options { get; init; }
    public required int ChunksCreated { get; init; }
    public required long TotalCharsExtracted { get; init; }
    public double? QualityScore { get; init; }
}
```

**Key Implementation Details:**
- Stores extraction parameters hash to detect parameter changes
- Compares source file modification time with last extraction time
- Enables incremental extraction by skipping already-processed files
- Tracks quality scores for extraction validation

#### 3.2.9 ChunkValidator

Validates extracted chunks and calculates quality metrics.

```csharp
public interface IChunkValidator
{
    Task<ValidationResult> ValidateChunkAsync(TextChunk chunk);
    Task<QualityMetrics> CalculateQualityAsync(IEnumerable<TextChunk> chunks);
}

public sealed record ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Issues { get; init; } = [];
    public double QualityScore { get; init; }  // 0.0 to 1.0
}

public sealed record QualityMetrics
{
    public int TotalChunks { get; init; }
    public int ValidChunks { get; init; }
    public double AverageChunkLength { get; init; }
    public double AverageQualityScore { get; init; }
    public IReadOnlyList<string> CommonIssues { get; init; } = [];
}
```

#### 3.2.10 DatabaseMaintenanceService

Performs database maintenance operations.

```csharp
public interface IDatabaseMaintenanceService
{
    Task VacuumAsync();
    Task OptimizeAsync();
    Task BackupAsync(string backupPath);
    Task RestoreAsync(string backupPath);
    Task<DatabaseIntegrityResult> CheckIntegrityAsync();
    Task<DatabaseStatistics> GetStatisticsAsync();
}
```

#### 3.2.11 ConfigurationValidator

Validates configuration files on startup.

```csharp
public interface IConfigurationValidator
{
    Task<ValidationResult> ValidateAsync(SyncConfiguration config);
}

public sealed record ConfigurationValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];
    public IReadOnlyList<ValidationWarning> Warnings { get; init; } = [];
}

public sealed record ValidationError
{
    public required string Path { get; init; }
    public required string Message { get; init; }
    public string? SuggestedFix { get; init; }
}
```

---

## 4. Data Models

### 4.1 EbookMetadata

```csharp
public sealed record EbookMetadata
{
    public required int GutenbergId { get; init; }
    public required string Title { get; init; }
    public IReadOnlyList<Author> Authors { get; init; } = [];
    public IReadOnlyList<string> Subjects { get; init; } = [];
    public IReadOnlyList<string> Bookshelves { get; init; } = [];
    public required string Language { get; init; }
    public string? LanguageIsoCode { get; init; }  // ISO 639-1 code (e.g., "en", "fr")
    public IReadOnlyList<FileFormat> Formats { get; init; } = [];
    public DateOnly? ReleaseDate { get; init; }
    public string? Rights { get; init; }
    public int? Downloads { get; init; }
    
    // Local tracking
    public string? LocalPath { get; init; }
    public DateTime? LastSyncedUtc { get; init; }
    public long? LocalFileSizeBytes { get; init; }
}

public sealed record Author
{
    public int? GutenbergAgentId { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public int? BirthYear { get; init; }
    public int? DeathYear { get; init; }
    public string? WikipediaUrl { get; init; }
}

public sealed record FileFormat
{
    public required string Url { get; init; }
    public required string MediaType { get; init; }
    public long? SizeBytes { get; init; }
    public DateTime? ModifiedUtc { get; init; }
}
```

### 4.2 Database Schema (SQLite)

```sql
-- Core ebook table
CREATE TABLE IF NOT EXISTS ebooks (
    gutenberg_id INTEGER PRIMARY KEY,
    title TEXT NOT NULL,
    language TEXT NOT NULL,
    language_iso_code TEXT,  -- ISO 639-1 code (e.g., "en", "fr")
    release_date TEXT,
    rights TEXT,
    downloads INTEGER,
    local_path TEXT,
    last_synced_utc TEXT,
    local_file_size_bytes INTEGER,
    verified_utc TEXT,  -- Last verification timestamp
    checksum TEXT,  -- File checksum for integrity verification
    created_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Authors (many-to-many)
CREATE TABLE IF NOT EXISTS authors (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    gutenberg_agent_id INTEGER UNIQUE,
    name TEXT NOT NULL,
    birth_year INTEGER,
    death_year INTEGER,
    wikipedia_url TEXT
);

CREATE TABLE IF NOT EXISTS ebook_authors (
    ebook_id INTEGER NOT NULL REFERENCES ebooks(gutenberg_id) ON DELETE CASCADE,
    author_id INTEGER NOT NULL REFERENCES authors(id) ON DELETE CASCADE,
    PRIMARY KEY (ebook_id, author_id)
);

CREATE TABLE IF NOT EXISTS author_aliases (
    author_id INTEGER NOT NULL REFERENCES authors(id) ON DELETE CASCADE,
    alias TEXT NOT NULL,
    PRIMARY KEY (author_id, alias)
);

-- Subjects
CREATE TABLE IF NOT EXISTS ebook_subjects (
    ebook_id INTEGER NOT NULL REFERENCES ebooks(gutenberg_id) ON DELETE CASCADE,
    subject TEXT NOT NULL,
    PRIMARY KEY (ebook_id, subject)
);

-- Bookshelves
CREATE TABLE IF NOT EXISTS ebook_bookshelves (
    ebook_id INTEGER NOT NULL REFERENCES ebooks(gutenberg_id) ON DELETE CASCADE,
    bookshelf TEXT NOT NULL,
    PRIMARY KEY (ebook_id, bookshelf)
);

-- File formats
CREATE TABLE IF NOT EXISTS ebook_formats (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    ebook_id INTEGER NOT NULL REFERENCES ebooks(gutenberg_id) ON DELETE CASCADE,
    url TEXT NOT NULL,
    media_type TEXT NOT NULL,
    size_bytes INTEGER,
    modified_utc TEXT
);

-- Sync history
CREATE TABLE IF NOT EXISTS sync_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    started_utc TEXT NOT NULL,
    completed_utc TEXT,
    endpoint TEXT NOT NULL,
    files_checked INTEGER,
    files_downloaded INTEGER,
    bytes_transferred INTEGER,
    status TEXT NOT NULL, -- 'running', 'completed', 'failed'
    error_message TEXT
);

-- Audit log for file verification
CREATE TABLE IF NOT EXISTS audit_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    gutenberg_id INTEGER REFERENCES ebooks(gutenberg_id),
    file_path TEXT NOT NULL,
    operation TEXT NOT NULL, -- 'sync', 'extract', 'verify'
    status TEXT NOT NULL, -- 'success', 'failed', 'corrupt', 'missing'
    checksum_expected TEXT,
    checksum_actual TEXT,
    file_size_expected INTEGER,
    file_size_actual INTEGER,
    error_message TEXT,
    timestamp_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_audit_log_gutenberg_id ON audit_log(gutenberg_id);
CREATE INDEX IF NOT EXISTS idx_audit_log_status ON audit_log(status);
CREATE INDEX IF NOT EXISTS idx_audit_log_timestamp ON audit_log(timestamp_utc);

-- Extraction state tracking
CREATE TABLE IF NOT EXISTS extraction_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    gutenberg_id INTEGER NOT NULL REFERENCES ebooks(gutenberg_id) ON DELETE CASCADE,
    source_file_path TEXT NOT NULL,
    output_directory TEXT NOT NULL,
    extraction_parameters_hash TEXT NOT NULL,  -- Hash of chunk size, overlap, format, etc.
    chunk_size_words INTEGER,
    chunk_overlap_words INTEGER,
    output_format TEXT NOT NULL,  -- 'json', 'txt', 'parquet', 'arrow'
    chunks_created INTEGER,
    total_chars_extracted INTEGER,
    extraction_started_utc TEXT NOT NULL,
    extraction_completed_utc TEXT,
    status TEXT NOT NULL,  -- 'completed', 'failed', 'partial'
    error_message TEXT,
    quality_score REAL,  -- 0.0 to 1.0
    UNIQUE(gutenberg_id, output_directory, extraction_parameters_hash)
);

CREATE INDEX IF NOT EXISTS idx_extraction_history_gutenberg_id ON extraction_history(gutenberg_id);
CREATE INDEX IF NOT EXISTS idx_extraction_history_output_dir ON extraction_history(output_directory);
CREATE INDEX IF NOT EXISTS idx_extraction_history_completed ON extraction_history(extraction_completed_utc);

-- Indexes for common queries
CREATE INDEX IF NOT EXISTS idx_ebooks_language ON ebooks(language);
CREATE INDEX IF NOT EXISTS idx_ebooks_language_iso ON ebooks(language_iso_code);
CREATE INDEX IF NOT EXISTS idx_ebooks_title ON ebooks(title);
CREATE INDEX IF NOT EXISTS idx_authors_name ON authors(name);
CREATE INDEX IF NOT EXISTS idx_ebook_subjects_subject ON ebook_subjects(subject);
CREATE INDEX IF NOT EXISTS idx_ebooks_verified ON ebooks(verified_utc);

-- Full-text search
CREATE VIRTUAL TABLE IF NOT EXISTS ebooks_fts USING fts5(
    title, 
    content='ebooks', 
    content_rowid='gutenberg_id'
);

-- Triggers to keep FTS in sync
CREATE TRIGGER IF NOT EXISTS ebooks_ai AFTER INSERT ON ebooks BEGIN
    INSERT INTO ebooks_fts(rowid, title) VALUES (new.gutenberg_id, new.title);
END;

CREATE TRIGGER IF NOT EXISTS ebooks_ad AFTER DELETE ON ebooks BEGIN
    INSERT INTO ebooks_fts(ebooks_fts, rowid, title) VALUES('delete', old.gutenberg_id, old.title);
END;

CREATE TRIGGER IF NOT EXISTS ebooks_au AFTER UPDATE ON ebooks BEGIN
    INSERT INTO ebooks_fts(ebooks_fts, rowid, title) VALUES('delete', old.gutenberg_id, old.title);
    INSERT INTO ebooks_fts(rowid, title) VALUES (new.gutenberg_id, new.title);
END;
```

---

## 5. Synchronization Strategy

### 5.1 Metadata-First Sync Strategy

The application implements a two-phase synchronization approach:

**Phase 1: Metadata Sync**
1. Sync RDF files from `cache/epub/` directories first
2. Parse all RDF files and build catalog database
3. Catalog serves as checklist of available books

**Phase 2: Content Sync**
1. Use catalog to determine which content files to sync
2. Track sync progress per book in catalog
3. Verify downloaded files against catalog metadata
4. Update catalog with local file paths and verification status

**Benefits:**
- Early catalog availability for search/query
- Checklist-based sync ensures completeness
- Better progress tracking (books vs. files)
- Enables selective content sync based on catalog queries

### 5.2 rsync Command Construction

**Base Command:**
```bash
rsync -avHS --progress --timeout=600 \
    [--delete] \
    [--include/--exclude patterns] \
    [--bwlimit=KBPS] \
    <source>::<module> <destination>
```

**Flag Explanations:**
- `-a` (archive): Preserve permissions, timestamps, symlinks
- `-v` (verbose): Output file names for progress tracking
- `-H` (hard-links): Preserve hard links
- `-S` (sparse): Handle sparse files efficiently
- `--progress`: Per-file transfer progress
- `--timeout=600`: 10-minute timeout for stalled transfers
- `--delete`: Remove local files not present on server

### 5.3 Content Filtering Presets

```csharp
public static class SyncPresets
{
    // Text only - smallest footprint (~15GB)
    public static RsyncOptions TextOnly => new()
    {
        IncludePatterns = ["*/", "*.txt", "*.zip"],
        ExcludePatterns = ["*-h.zip", "*-8.zip", "*-0.zip", "*/old/*", "*.mp3", "*.ogg"],
        MaxFileSize = "10m"
    };
    
    // Text + EPUB - good for RAG (~50GB)
    public static RsyncOptions TextAndEpub => new()
    {
        IncludePatterns = ["*/", "*.txt", "*.epub", "*.zip"],
        ExcludePatterns = ["*-h.zip", "*/old/*", "*.mp3", "*.ogg", "*.m4b"],
        MaxFileSize = "50m"
    };
    
    // All text formats (~100GB)
    public static RsyncOptions AllTextFormats => new()
    {
        IncludePatterns = ["*/", "*.txt", "*.epub", "*.mobi", "*.html", "*.htm", "*.zip"],
        ExcludePatterns = ["*/old/*", "*.mp3", "*.ogg", "*.m4b", "*.wav"],
        MaxFileSize = "100m"
    };
    
    // Full archive including audio (~1TB)
    public static RsyncOptions FullArchive => new()
    {
        ExcludePatterns = ["*/old/*"]
    };
}
```

### 5.4 Progress Tracking

rsync output is parsed to extract:
- Current file name and path
- Transfer speed (bytes/second)
- Files transferred / total
- Bytes transferred / total

```csharp
public sealed record SyncProgress
{
    public string CurrentFile { get; init; } = "";
    public long BytesTransferred { get; init; }
    public long TotalBytes { get; init; }
    public int FilesTransferred { get; init; }
    public int TotalFiles { get; init; }
    public double TransferRateBytesPerSecond { get; init; }
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
    
    public double PercentComplete => TotalBytes > 0 
        ? (double)BytesTransferred / TotalBytes * 100 
        : 0;
}
```

### 5.5 Mirror Failover Strategy

```csharp
public class MirrorSelector
{
    private readonly IReadOnlyList<MirrorEndpoint> _mirrors = 
    [
        new("aleph.gutenberg.org", "gutenberg", Priority: 1, Region: "US"),
        new("aleph.gutenberg.org", "gutenberg-epub", Priority: 1, Region: "US"),
        new("ftp.ibiblio.org", "gutenberg", Priority: 2, Region: "US"),
        new("rsync.mirrorservice.org", "gutenberg.org", Priority: 3, Region: "UK")
    ];
    
    public async Task<MirrorEndpoint?> SelectBestMirrorAsync(CancellationToken ct)
    {
        // Test connectivity and latency, return fastest available
    }
}
```

### 5.6 Concurrent Operations and Verification

**Concurrency Model:**
- Sync and extract operations can run concurrently
- File-level locking prevents write conflicts
- Read operations are lock-free

**Verification System:**
```csharp
public interface IAuditService
{
    Task VerifyFileAsync(string filePath, int gutenbergId, CancellationToken ct);
    Task<IReadOnlyList<AuditIssue>> ScanForIssuesAsync(string archiveDirectory, CancellationToken ct);
    Task<bool> VerifyChecksumAsync(string filePath, string expectedChecksum);
}

public sealed record AuditIssue
{
    public int GutenbergId { get; init; }
    public string FilePath { get; init; } = "";
    public AuditIssueType Type { get; init; }  // Missing, Corrupt, SizeMismatch, ChecksumMismatch
    public string? ExpectedValue { get; init; }
    public string? ActualValue { get; init; }
}
```

**Post-Sync Verification:**
- Calculate checksums (SHA-256) for downloaded files
- Compare file sizes against catalog metadata
- Log verification results to audit_log table
- Automatic retry for failed verifications
- Background scanner detects missing/corrupt files

**Audit Operations:**
- Periodic full archive scan
- Verify files against catalog entries
- Detect orphaned files (not in catalog)
- Report integrity statistics

---

## 6. API Design

### 6.1 CLI Commands

```
gutenberg-sync [command] [options]

Commands:
  sync        Synchronize archive from Project Gutenberg mirrors
  catalog     Query and export the local catalog
  extract     Extract text content from downloaded files
  config      View or modify configuration
  health      Show system health and status
  database    Database maintenance operations

Global Options:
  -c, --config <path>    Path to configuration file
  -v, --verbose          Enable verbose output
  --log-file <path>      Write logs to file
  -h, --help             Show help

sync [options]:
  -t, --target <path>    Target directory for archive (required)
  -p, --preset <name>    Content preset: text-only, text-epub, all-text, full
  --include <pattern>    Include file pattern (can specify multiple)
  --exclude <pattern>    Exclude file pattern (can specify multiple)
  --mirror <url>         Specific mirror to use
  --bandwidth <kbps>     Bandwidth limit in KB/s
  --delete               Remove local files not on server
  --dry-run              Show what would be transferred
  -m, --metadata-only    Only sync metadata (RDF files) (Phase 1 of metadata-first strategy)
  --verify               Verify downloaded files after sync (checksums, sizes)
  --audit                Run audit scan to detect missing/corrupt files

catalog [subcommand] [options]:
  search <query>         Search catalog by title/author
    --language <code>    Filter by language
    --subject <text>     Filter by subject
    --author <name>      Filter by author
    --format <json|csv|table>  Output format
    --limit <n>          Maximum results
  
  stats                  Show catalog statistics
  
  export <path>          Export catalog to file
    --format <json|csv>  Export format
  
  rebuild                Rebuild catalog from downloaded RDF files

extract [options]:
  -s, --source <path>    Source archive directory
  -o, --output <path>    Output directory for extracted text
  --strip-headers        Remove Gutenberg headers/footers (default: true)
  --chunk-size <words>   Split into chunks of N words (for RAG)
  --chunk-overlap <words> Overlap between chunks
  --language <code>      Only extract specific language
  --format <txt|json|parquet|arrow>  Output format
  --compress             Compress output (gzip for JSON, built-in for Parquet/Arrow)
  --parallel <n>         Number of parallel extractions
  --incremental          Only extract new/changed files (default: true)
  --force                Force re-extraction of all files
  --dry-run              Preview what would be extracted without processing
  --book-ids <ids>       Extract specific book IDs (comma-separated)
  --author <name>        Extract books by author (catalog query)
  --subject <text>       Extract books by subject (catalog query)
  --date-range <start:end>  Extract books by release date range (YYYY-MM-DD:YYYY-MM-DD)
  --validate             Validate extracted chunks and report quality metrics

config [subcommand]:
  show                   Display current configuration
  init                   Create default configuration file
  set <key> <value>      Set configuration value
  validate               Validate configuration file

health [options]:
  --format <json|table>  Output format (default: table)
  --detailed             Show detailed information

database [subcommand]:
  vacuum                 Optimize database by reclaiming space
  optimize               Analyze and optimize database indexes
  backup <path>          Backup database to file
  restore <path>         Restore database from backup
  integrity              Check database integrity
  stats                  Show database statistics
```

**Health Command Output:**
The health command provides a quick overview of system status:
- Archive statistics (total size, file count, last sync time)
- Catalog statistics (total books, languages, authors)
- Extraction status (books extracted, last extraction time, output directories)
- Issues and warnings (missing files, integrity problems)
- Database status (size, last maintenance, integrity check)

**Example Output:**
```
Archive Status:
  Location: /data/gutenberg
  Total Size: 45.2 GB
  Files: 1,234,567
  Last Sync: 2025-12-08 04:00:00 UTC

Catalog Status:
  Total Books: 76,234
  Languages: 62
  Authors: 12,456
  Last Updated: 2025-12-08 04:15:00 UTC

Extraction Status:
  Books Extracted: 45,123
  Output Directories: 3
  Last Extraction: 2025-12-07 18:30:00 UTC

Issues: None
```

### 6.2 Library API Examples

```csharp
// Basic synchronization
var syncService = new RsyncService(logger);
var options = SyncPresets.TextAndEpub with 
{ 
    TargetDirectory = "/data/gutenberg",
    BandwidthLimitKbps = 10000 
};

var progress = new Progress<SyncProgress>(p => 
    Console.WriteLine($"{p.PercentComplete:F1}% - {p.CurrentFile}"));

var result = await syncService.SyncAsync(options, progress);
Console.WriteLine($"Downloaded {result.FilesTransferred} files ({result.BytesTransferred} bytes)");

// Catalog search
var catalog = new SqliteCatalogRepository("gutenberg.db");
var results = await catalog.SearchAsync(new CatalogSearchOptions
{
    TitleContains = "sherlock",
    Language = "en",
    Limit = 10
});

foreach (var book in results)
{
    Console.WriteLine($"[{book.GutenbergId}] {book.Title} by {book.Authors.FirstOrDefault()?.Name}");
}

// Text extraction
var extractor = new TextExtractor();
var extracted = await extractor.ExtractAsync(
    "/data/gutenberg/1/0/0/100/100.zip",
    new TextExtractionOptions
    {
        StripHeaders = true,
        ChunkSizeWords = 500,
        ChunkOverlapWords = 50
    });

foreach (var chunk in extracted.Chunks)
{
    // Each chunk includes full metadata:
    // chunk.BookMetadata (GutenbergId, Title, Authors, Language, Subjects)
    // chunk.ChunkIndex, chunk.TotalChunks
    // chunk.Text
    // Send to embedding service with metadata
}
```

**RAG Chunk Output Format:**

```json
{
  "chunkIndex": 0,
  "totalChunks": 45,
  "book": {
    "gutenbergId": 1342,
    "title": "Pride and Prejudice",
    "authors": [
      {
        "name": "Austen, Jane",
        "gutenbergAgentId": 68
      }
    ],
    "language": "English",
    "languageIsoCode": "en",
    "subjects": ["Fiction", "Love stories", "England -- Social life and customs -- Fiction"],
    "releaseDate": "2008-06-26"
  },
  "text": "It is a truth universally acknowledged..."
}
```

---

## 7. Configuration

### 7.1 Configuration File Schema

```json
{
  "$schema": "https://raw.githubusercontent.com/user/gutenberg-sync/main/config.schema.json",
  "sync": {
    "targetDirectory": "/data/gutenberg",
    "preset": "text-epub",
    "mirrors": [
      {
        "host": "aleph.gutenberg.org",
        "module": "gutenberg",
        "priority": 1
      },
      {
        "host": "aleph.gutenberg.org", 
        "module": "gutenberg-epub",
        "priority": 1
      }
    ],
    "include": ["*/", "*.txt", "*.epub", "*.zip"],
    "exclude": ["*/old/*", "*.mp3", "*.ogg"],
    "maxFileSizeMb": 50,
    "bandwidthLimitKbps": null,
    "deleteRemoved": false,
    "timeoutSeconds": 600
  },
  "catalog": {
    "databasePath": null,
    "autoRebuildOnSync": true,
    "verifyAfterSync": true,
    "auditScanIntervalDays": 7
  },
  "extraction": {
    "outputDirectory": "/data/gutenberg-text",
    "stripHeaders": true,
    "normalizeEncoding": true,
    "defaultChunkSizeWords": 500,
    "defaultChunkOverlapWords": 50,
    "incremental": true,
    "validateChunks": true,
    "defaultFormat": "json",
    "compressOutput": false
  },
  "logging": {
    "level": "Information",
    "filePath": "logs/gutenberg-sync.log",
    "retainDays": 30
  },
  "schedule": {
    "enabled": false,
    "cronExpression": "0 4 * * *"
  }
}
```

### 7.2 Database Path Resolution

The catalog database path is resolved as follows:

1. **Explicit configuration**: If `catalog.databasePath` is set (absolute or relative), use it
2. **Environment variable**: `GUTENBERG_CATALOG_DATABASE_PATH` overrides config
3. **Default**: If null/empty, default to `{sync.targetDirectory}/gutenberg.db`

**Examples:**
- Config: `"databasePath": null` → Uses `{targetDirectory}/gutenberg.db`
- Config: `"databasePath": "catalog.db"` → Relative to current working directory
- Config: `"databasePath": "/var/lib/gutenberg/catalog.db"` → Absolute path
- Env: `GUTENBERG_CATALOG_DATABASE_PATH=/custom/path.db` → Overrides config

### 7.4 Configuration Validation

Configuration is validated on startup:

**Validation Checks:**
- Target directory exists and is writable
- Database path directory exists (if absolute path)
- rsync is available (or installation instructions provided)
- All required fields are present
- File paths are valid for the platform
- Numeric values are within valid ranges
- Preset names are recognized

**Error Reporting:**
- Clear error messages with file path and line number
- Suggested fixes for common issues
- Warnings for non-critical issues (e.g., deprecated options)

```csharp
var validator = new ConfigurationValidator();
var result = await validator.ValidateAsync(config);

if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.Error.WriteLine($"Error at {error.Path}: {error.Message}");
        if (error.SuggestedFix != null)
            Console.Error.WriteLine($"  Suggestion: {error.SuggestedFix}");
    }
    Environment.Exit(1);
}
```

### 7.5 Environment Variable Overrides

All configuration values can be overridden via environment variables:

```
GUTENBERG_SYNC_TARGET_DIRECTORY=/data/gutenberg
GUTENBERG_SYNC_BANDWIDTH_LIMIT_KBPS=5000
GUTENBERG_CATALOG_DATABASE_PATH=/var/lib/gutenberg/catalog.db
```

---

## 8. Error Handling

### 8.1 Error Categories

| Category | Examples | Strategy |
|----------|----------|----------|
| Transient | Network timeout, mirror unavailable | Retry with exponential backoff |
| Configuration | Invalid path, missing rsync | Fail fast with clear message and installation instructions |
| Data | Corrupt RDF, encoding issues | Log warning, skip file, continue |
| Resource | Disk full, permission denied | Fail operation, preserve state |

### 8.2 Retry Policy (Polly)

```csharp
var retryPolicy = Policy
    .Handle<IOException>()
    .Or<TimeoutException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            _logger.LogWarning(exception, 
                "Retry {RetryCount} after {Delay}s", retryCount, timeSpan.TotalSeconds);
        });
```

### 8.3 Graceful Shutdown

- Handle SIGINT/SIGTERM signals
- Complete current file transfer before stopping
- Save sync progress to enable resume
- Write partial catalog updates to database
- Complete verification of in-progress files before exit

### 8.4 rsync Discovery and Installation Guidance

**Platform Detection:**
```csharp
public interface IRsyncDiscoveryService
{
    Task<RsyncDiscoveryResult> DiscoverAsync(CancellationToken ct);
    string GetInstallationInstructions(Platform platform);
}

public sealed record RsyncDiscoveryResult
{
    public bool IsAvailable { get; init; }
    public string? ExecutablePath { get; init; }
    public Platform Platform { get; init; }
    public RsyncSource Source { get; init; }  // Native, WSL, Cygwin, PATH
    public string? Version { get; init; }
}
```

**Windows Detection Strategy:**
1. Check PATH for `rsync.exe`
2. Check WSL: `wsl which rsync` or `wsl.exe which rsync`
3. Check Cygwin: Common installation paths
4. If not found, provide platform-specific instructions:
   - **WSL**: `wsl --install` then `sudo apt-get install rsync`
   - **Cygwin**: Download from cygwin.com, select rsync package
   - **Native Windows**: Link to cwRsync or similar

**Installation Instructions Format:**
- Platform-specific commands
- Package manager instructions (apt, brew, choco)
- Direct download links where applicable
- Verification command to test installation

---

## 9. Testing Strategy

### 9.1 Unit Tests

| Component | Test Coverage Areas |
|-----------|---------------------|
| RdfParser | Valid RDF, malformed XML, missing fields, namespaces |
| TextExtractor | Header stripping, encoding normalization, chunking, incremental extraction |
| CatalogRepository | CRUD operations, search queries, FTS |
| Configuration | Parsing, validation, environment overrides |
| ExtractionStateTracker | State tracking, incremental detection, parameter hashing |
| ChunkValidator | Quality metrics, validation rules, edge cases |
| ConfigurationValidator | Path validation, required fields, value ranges |
| DatabaseMaintenanceService | Vacuum, optimize, backup, restore, integrity checks |

### 9.2 Integration Tests

- End-to-end sync with test mirror subset
- Database migrations and schema changes
- CLI command execution and output parsing
- Incremental extraction workflow
- Selective extraction with catalog queries
- Health/status command output
- Database maintenance operations
- Configuration validation scenarios

### 9.3 Test Data

Include sample RDF files and book texts in test fixtures:
```
tests/
├── fixtures/
│   ├── rdf/
│   │   ├── pg100.rdf          # Shakespeare
│   │   ├── pg1342.rdf         # Pride and Prejudice
│   │   └── pg84.rdf           # Frankenstein
│   ├── texts/
│   │   ├── with-header.txt
│   │   ├── no-header.txt
│   │   └── various-encodings/
│   └── expected/
│       └── extracted/
```

---

## 10. Deployment

### 10.1 Build and Publish

```bash
# Build release
dotnet build -c Release

# Publish self-contained for Linux
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish/linux

# Publish self-contained for Windows  
dotnet publish -c Release -r win-x64 --self-contained -o ./publish/windows

# Publish framework-dependent (smaller)
dotnet publish -c Release -o ./publish/portable
```

### 10.2 NuGet Package (Library)

```xml
<PropertyGroup>
    <PackageId>GutenbergSync.Core</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>Library for synchronizing and querying Project Gutenberg archives</Description>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/user/gutenberg-sync</PackageProjectUrl>
    <RepositoryUrl>https://github.com/user/gutenberg-sync</RepositoryUrl>
    <PackageTags>gutenberg;ebooks;archive;sync;rsync;rag</PackageTags>
</PropertyGroup>
```

### 10.3 Docker Support

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
RUN apt-get update && apt-get install -y rsync && rm -rf /var/lib/apt/lists/*
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "src/GutenbergSync.Cli/GutenbergSync.Cli.csproj" \
    -c Release -o /app/publish

FROM base AS final
COPY --from=build /app/publish .
VOLUME ["/data", "/config"]
ENTRYPOINT ["dotnet", "GutenbergSync.Cli.dll"]
```

### 10.4 Scheduled Execution

**Linux (systemd timer):**
```ini
# /etc/systemd/system/gutenberg-sync.service
[Unit]
Description=Gutenberg Archive Sync

[Service]
Type=oneshot
ExecStart=/usr/local/bin/gutenberg-sync sync -c /etc/gutenberg/config.json
User=gutenberg

# /etc/systemd/system/gutenberg-sync.timer
[Unit]
Description=Daily Gutenberg Sync

[Timer]
OnCalendar=*-*-* 04:00:00
Persistent=true

[Install]
WantedBy=timers.target
```

**Windows (Task Scheduler):**
```powershell
$action = New-ScheduledTaskAction -Execute "C:\Tools\gutenberg-sync.exe" `
    -Argument "sync -c C:\Config\gutenberg.json"
$trigger = New-ScheduledTaskTrigger -Daily -At 4am
Register-ScheduledTask -TaskName "GutenbergSync" -Action $action -Trigger $trigger
```

---

## Appendix A: Gutenberg Header/Footer Patterns

```csharp
public static class GutenbergMarkers
{
    // Start markers (text begins AFTER this line)
    public static readonly string[] StartMarkers =
    [
        "*** START OF THIS PROJECT GUTENBERG EBOOK",
        "*** START OF THE PROJECT GUTENBERG EBOOK",
        "***START OF THIS PROJECT GUTENBERG EBOOK",
        "*END*THE SMALL PRINT!",
        "*** START OF THIS PROJECT GUTENBERG"
    ];
    
    // End markers (text ends BEFORE this line)
    public static readonly string[] EndMarkers =
    [
        "*** END OF THIS PROJECT GUTENBERG EBOOK",
        "*** END OF THE PROJECT GUTENBERG EBOOK",
        "***END OF THIS PROJECT GUTENBERG EBOOK",
        "End of Project Gutenberg",
        "End of the Project Gutenberg"
    ];
}
```

---

## Appendix B: Language Mapping

The application maintains a mapping between Project Gutenberg language names and ISO 639-1 codes:

```csharp
public static class LanguageMappings
{
    public static readonly Dictionary<string, string> NameToIso = new()
    {
        ["English"] = "en",
        ["French"] = "fr",
        ["German"] = "de",
        ["Spanish"] = "es",
        ["Italian"] = "it",
        ["Portuguese"] = "pt",
        ["Russian"] = "ru",
        ["Chinese"] = "zh",
        ["Japanese"] = "ja",
        ["Arabic"] = "ar",
        // ... additional mappings
    };
    
    public static readonly Dictionary<string, string> IsoToName = 
        NameToIso.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
}
```

**Mapping Strategy:**
- Parse language from RDF (may be name or code)
- If name, look up ISO code
- If ISO code, look up name
- Store both in database for flexibility
- Accept both in search/filter operations

## Appendix C: RDF Namespace Reference

```csharp
public static class RdfNamespaces
{
    public const string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    public const string Rdfs = "http://www.w3.org/2000/01/rdf-schema#";
    public const string DcTerms = "http://purl.org/dc/terms/";
    public const string DcAm = "http://purl.org/dc/dcam/";
    public const string PgTerms = "http://www.gutenberg.org/2009/pgterms/";
    public const string Cc = "http://web.resource.org/cc/";
    
    public static readonly XNamespace RdfNs = Rdf;
    public static readonly XNamespace PgTermsNs = PgTerms;
    public static readonly XNamespace DcTermsNs = DcTerms;
}
```

---

## Appendix E: Export Formats

### JSON Format (Default)
Standard JSON with embedded metadata in each chunk. Suitable for direct ingestion into vector databases.

### Parquet Format
Columnar format optimized for analytics and ML pipelines:
- Efficient compression
- Schema evolution support
- Fast columnar reads
- Compatible with pandas, Spark, Arrow

### Arrow Format
In-memory columnar format for high-performance data processing:
- Zero-copy reads
- Cross-language compatibility
- Optimized for analytics workloads

### Compressed JSON
JSON files compressed with gzip or brotli:
- Reduced storage footprint
- Maintains JSON structure
- Standard compression algorithms

**Format Selection:**
- Use JSON for direct RAG ingestion
- Use Parquet/Arrow for analytics or batch processing
- Use compressed JSON for storage optimization

## Appendix F: Extraction State Tracking

The extraction_history table tracks:
- Which books have been extracted
- When they were extracted
- What parameters were used (chunk size, format, etc.)
- Output location
- Quality scores

**Parameter Hashing:**
Extraction parameters are hashed to detect changes:
```csharp
var hash = HashCode.Combine(
    options.ChunkSizeWords,
    options.ChunkOverlapWords,
    options.OutputFormat,
    options.StripHeaders,
    options.NormalizeEncoding
);
```

**Incremental Detection:**
A file needs re-extraction if:
1. Never extracted before
2. Source file modified after last extraction
3. Extraction parameters changed (hash differs)
4. Output format changed

## Appendix G: Quality Metrics

**Chunk Validation:**
- Non-empty chunks
- Reasonable length (not too short, not too long)
- Valid UTF-8 encoding
- No excessive whitespace
- Metadata completeness

**Quality Score Calculation:**
```csharp
qualityScore = (
    (hasContent ? 0.3 : 0) +
    (validLength ? 0.3 : 0) +
    (validEncoding ? 0.2 : 0) +
    (completeMetadata ? 0.2 : 0)
)
```

**Reporting:**
- Per-book quality scores
- Aggregate statistics
- Common issues identified
- Recommendations for improvement

## Appendix D: Directory Structure Reference

### Main Collection (gutenberg module)
```
/1/2/3/4/12345/
├── 12345.txt           # Plain text (ASCII)
├── 12345-0.txt         # Plain text (UTF-8)
├── 12345-8.txt         # Plain text (ISO-8859-1)
├── 12345.zip           # Zipped plain text
├── 12345-h/            # HTML version
│   ├── 12345-h.htm
│   └── images/
├── 12345-h.zip         # Zipped HTML
└── 12345-readme.txt    # Book-specific notes
```

### Generated Collection (gutenberg-epub module)
```
/cache/epub/12345/
├── pg12345.rdf         # RDF metadata
├── pg12345.epub        # EPUB format
├── pg12345.epub.noimages
├── pg12345.mobi        # Kindle format
├── pg12345.txt.utf-8   # UTF-8 plain text
├── pg12345.html        # Generated HTML
└── pg12345-cover.png   # Cover image
```
