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
- ✅ Resume interrupted downloads
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
- ✅ **Incremental extraction** (only processes new/changed files)
- ✅ **Selective extraction** (by author, subject, date range, book IDs)
- ✅ **Multiple export formats** (JSON, Parquet, Arrow, compressed JSON)
- ✅ **Quality validation** and metrics reporting
- ✅ **Dry-run mode** to preview extractions
- ✅ Batch extraction with parallelization

### Additional Features
- ✅ Real-time progress tracking
- ✅ Concurrent sync and extract operations (with verification)
- ✅ **Health/status monitoring** command
- ✅ **Configuration validation** on startup
- ✅ **Database maintenance** (vacuum, optimize, backup, restore)
- ✅ Comprehensive error handling and retry logic
- ✅ Structured logging
- ✅ Configuration via JSON/YAML with environment variable overrides

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

```bash
git clone <repository-url>
cd GutenbergSync
dotnet build -c Release
dotnet publish -c Release -o ./publish
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
  "sync": {
    "targetDirectory": "/data/gutenberg"
  },
  "catalog": {
    "databasePath": null
  }
}
```

### 2. Sync Raw Content

Download the Project Gutenberg archive:

```bash
# Text-only (smallest, ~15GB)
gutenberg-sync sync -t /data/gutenberg -p text-only

# Text + EPUB (recommended for RAG, ~50GB)
gutenberg-sync sync -t /data/gutenberg -p text-epub

# Full archive (~1TB)
gutenberg-sync sync -t /data/gutenberg -p full
```

The sync process uses a **metadata-first strategy**:
- **Phase 1**: Downloads RDF metadata files and builds the catalog
- **Phase 2**: Downloads content files using the catalog as a checklist

### 3. Extract RAG-Ready Chunks

Process the downloaded files into RAG-ready chunks:

```bash
# Basic extraction
gutenberg-sync extract \
  -s /data/gutenberg \
  -o /data/gutenberg-rag \
  --chunk-size 500 \
  --chunk-overlap 50 \
  --format json \
  --strip-headers
```

**Incremental Extraction**: By default, only new/changed files are extracted. Re-run with different parameters to experiment without re-processing everything.

**Preview Before Extracting**: Use `--dry-run` to see what would be extracted:

```bash
gutenberg-sync extract \
  -s /data/gutenberg \
  -o /data/gutenberg-rag \
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

# Export catalog
gutenberg-sync catalog export catalog.json --format json
```

## Configuration

### Configuration File

Create a `config.json` file:

```json
{
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
  }
}
```

### Database Path

The catalog database path is resolved as:
1. Explicit config: `catalog.databasePath` (absolute or relative)
2. Environment variable: `GUTENBERG_CATALOG_DATABASE_PATH`
3. Default: `{sync.targetDirectory}/gutenberg.db`

### Environment Variables

All configuration values can be overridden:

```bash
export GUTENBERG_SYNC_TARGET_DIRECTORY=/data/gutenberg
export GUTENBERG_SYNC_BANDWIDTH_LIMIT_KBPS=5000
export GUTENBERG_CATALOG_DATABASE_PATH=/var/lib/gutenberg/catalog.db
```

## Usage Examples

### Sync Operations

```bash
# Initial sync with text-only preset
gutenberg-sync sync -t /data/gutenberg -p text-only

# Incremental update
gutenberg-sync sync -t /data/gutenberg

# Sync with bandwidth limit
gutenberg-sync sync -t /data/gutenberg --bandwidth 5000

# Metadata-only sync (Phase 1 only)
gutenberg-sync sync -t /data/gutenberg --metadata-only

# Dry run (see what would be transferred)
gutenberg-sync sync -t /data/gutenberg --dry-run

# Verify files after sync
gutenberg-sync sync -t /data/gutenberg --verify
```

### Extraction Operations

```bash
# Extract with default settings (incremental by default)
gutenberg-sync extract -s /data/gutenberg -o /data/rag-output

# Extract with custom chunking
gutenberg-sync extract \
  -s /data/gutenberg \
  -o /data/rag-output \
  --chunk-size 1000 \
  --chunk-overlap 100

# Extract only English books
gutenberg-sync extract \
  -s /data/gutenberg \
  -o /data/rag-output \
  --language en

# Selective extraction by author
gutenberg-sync extract \
  -s /data/gutenberg \
  -o /data/rag-output \
  --author "Shakespeare"

# Extract specific book IDs
gutenberg-sync extract \
  -s /data/gutenberg \
  -o /data/rag-output \
  --book-ids 100,1342,84

# Extract by date range
gutenberg-sync extract \
  -s /data/gutenberg \
  -o /data/rag-output \
  --date-range 2000-01-01:2020-12-31

# Extract to Parquet format (for analytics/ML)
gutenberg-sync extract \
  -s /data/gutenberg \
  -o /data/rag-output \
  --format parquet \
  --compress

# Dry-run to preview what would be extracted
gutenberg-sync extract \
  -s /data/gutenberg \
  -o /data/rag-output \
  --dry-run

# Force re-extraction of all files
gutenberg-sync extract \
  -s /data/gutenberg \
  -o /data/rag-output \
  --force

# Extract with quality validation
gutenberg-sync extract \
  -s /data/gutenberg \
  -o /data/rag-output \
  --validate

# Parallel extraction (faster)
gutenberg-sync extract \
  -s /data/gutenberg \
  -o /data/rag-output \
  --parallel 8
```

### Catalog Operations

```bash
# Search catalog
gutenberg-sync catalog search "pride and prejudice"

# Show statistics
gutenberg-sync catalog stats

# Export to CSV
gutenberg-sync catalog export catalog.csv --format csv

# Rebuild catalog from RDF files
gutenberg-sync catalog rebuild
```

### Health and Status

```bash
# Quick system status overview
gutenberg-sync health

# Detailed status with JSON output
gutenberg-sync health --detailed --format json
```

Shows:
- Archive statistics (size, file count, last sync)
- Catalog statistics (books, languages, authors)
- Extraction status (books extracted, output directories)
- Issues and warnings

### Configuration Validation

```bash
# Validate configuration file
gutenberg-sync config validate

# Configuration is automatically validated on startup
gutenberg-sync sync -c config.json  # Will fail fast if config is invalid
```

### Database Maintenance

```bash
# Optimize database (vacuum and optimize indexes)
gutenberg-sync database vacuum
gutenberg-sync database optimize

# Backup and restore
gutenberg-sync database backup /backup/gutenberg.db
gutenberg-sync database restore /backup/gutenberg.db

# Check database integrity
gutenberg-sync database integrity

# Show database statistics
gutenberg-sync database stats
```

### Audit Operations

```bash
# Run audit scan
gutenberg-sync sync --audit

# Verify specific files
gutenberg-sync sync -t /data/gutenberg --verify
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
- **ExtractionStateTracker**: Tracks extraction history for incremental extraction
- **ChunkValidator**: Validates chunks and calculates quality metrics
- **CatalogRepository**: SQLite-based catalog with full-text search
- **DatabaseMaintenanceService**: Database optimization and maintenance operations
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
- **Parquet**: Columnar format optimized for analytics and ML pipelines
- **Arrow**: In-memory columnar format for high-performance processing
- **Compressed JSON**: Gzip/brotli compressed JSON for storage optimization

**Incremental Extraction:**
- Tracks which files have been extracted and with what parameters
- Only re-extracts if source file changed or parameters changed
- Significantly faster than full extraction (typically <10% of full time)
- Enables experimentation with different chunk sizes without full re-processing

**Selective Extraction:**
- Extract based on catalog queries (author, subject, date range)
- Extract specific book IDs
- Combine filters for targeted datasets
- Perfect for creating focused RAG datasets

**Quality Validation:**
- Validates extracted chunks (non-empty, reasonable length, encoding)
- Calculates quality scores per book
- Reports aggregate statistics and common issues
- Helps ensure high-quality RAG datasets

**Benefits:**
- Clean text without headers/footers
- Configurable chunk sizes for optimal embedding
- Full metadata for attribution and filtering
- Incremental extraction saves time on large archives
- Multiple formats for different use cases
- Quality metrics ensure data integrity

## Advanced Features

### Incremental Extraction

GutenbergSync tracks extraction history in the database, enabling efficient incremental extraction:

- **Automatic Detection**: Only extracts files that haven't been extracted or have changed
- **Parameter Tracking**: Re-extracts if chunk size, format, or other parameters changed
- **Fast Updates**: Typically completes in <10% of full extraction time
- **Experiment Freely**: Try different chunk sizes without full re-processing

### Selective Extraction

Extract specific subsets of books using catalog queries:

```bash
# Extract all books by a specific author
gutenberg-sync extract --author "Austen, Jane" -s /data/gutenberg -o /output

# Extract books from a specific time period
gutenberg-sync extract --date-range 1800-01-01:1900-12-31 -s /data/gutenberg -o /output

# Extract books with specific subjects
gutenberg-sync extract --subject "Fiction" --language en -s /data/gutenberg -o /output

# Extract specific book IDs
gutenberg-sync extract --book-ids 100,1342,84 -s /data/gutenberg -o /output
```

### Export Formats

Choose the best format for your use case:

- **JSON**: Direct ingestion into vector databases (Pinecone, Weaviate, etc.)
- **Parquet**: Analytics, ML pipelines, Spark processing
- **Arrow**: High-performance in-memory processing
- **Compressed JSON**: Storage optimization while maintaining JSON structure

### Quality Metrics

Extraction includes quality validation:

- Chunk validation (non-empty, reasonable length, valid encoding)
- Quality scores per book (0.0 to 1.0)
- Aggregate statistics and common issues
- Recommendations for improvement

### Concurrent Operations

GutenbergSync supports running sync and extract operations concurrently:

- **File-level locking** prevents write conflicts
- **Verification system** ensures data integrity
- **Audit logging** tracks all operations
- **Automatic retry** for failed verifications

## Compliance

- ✅ Respects Project Gutenberg's Terms of Use
- ✅ Uses official mirror infrastructure only (no website scraping)
- ✅ Implements respectful request patterns (rate limiting, backoff)
- ✅ Includes appropriate attribution in outputs

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

