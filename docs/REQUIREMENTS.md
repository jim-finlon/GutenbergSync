# Project Gutenberg Archive Tool - Requirements Document

**Version:** 1.2  
**Date:** December 8, 2025  
**Project:** GutenbergSync

## Executive Summary

A .NET application to efficiently mirror and manage a local copy of the Project Gutenberg archive for AI/RAG ingestion purposes, adhering to Project Gutenberg's official mirroring policies and best practices.

## Background Research Summary

### Official Mirroring Policy
Project Gutenberg explicitly supports and encourages mirroring via their [Mirroring How-To](https://www.gutenberg.org/help/mirroring.html). Key points:

- **rsync is the recommended method** - wget/cURL are discouraged as they must check all files
- **Robot access to website is blocked** - Direct scraping results in IP bans
- **Daily sync frequency** - Mirrors should update daily via cron/scheduler
- **Two data modules available**:
  - `gutenberg` - Main collection (curated HTML, plain text, audio, zip archives)
  - `gutenberg-epub` - Generated content (EPUB, MOBI, Kindle formats)

### Archive Statistics
- **~76,000+ ebooks** as of 2024/2025
- **~2 million files** across all formats
- **60+ languages** (majority English)
- **Size estimates**:
  - Full archive with audio: 800GB - 1TB
  - Text-only (zipped): 8-15GB
  - Text-only (uncompressed): ~40GB
  - Generated formats (EPUB/MOBI): Additional 50-100GB

### Official rsync Endpoints
| Endpoint | Content | Structure |
|----------|---------|-----------|
| `aleph.gutenberg.org::gutenberg` | Main collection | Hierarchical (1/2/3/4/12345/) |
| `aleph.gutenberg.org::gutenberg-epub` | Generated formats | Flat (epub/12345/) |
| `ftp.ibiblio.org::gutenberg` | Mirror (main) | Hierarchical |
| `rsync.mirrorservice.org::gutenberg.org` | UK Mirror (full) | Mixed |

### Metadata Format
- **RDF/XML format** - Per-book .rdf files in cache/epub directories
- **Concatenated catalog** - Single large XML file available
- **CSV export** - Weekly Excel-compatible spreadsheet
- **MARC records** - Library catalog format via Free Ebook Foundation

---

## Functional Requirements

### FR-1: Archive Synchronization
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-1.1 | Support rsync-based synchronization with official Gutenberg endpoints | Must Have |
| FR-1.2 | Support incremental/delta updates (only download changed files) | Must Have |
| FR-1.3 | Allow filtering by file type (txt, html, epub, mobi, pdf, audio) | Must Have |
| FR-1.4 | Allow filtering by language (ISO language codes or language names with automatic mapping) | Should Have |
| FR-1.5 | Support multiple mirror endpoints with automatic failover | Should Have |
| FR-1.6 | Implement bandwidth throttling/rate limiting | Must Have |
| FR-1.7 | Support scheduled automatic synchronization | Should Have |
| FR-1.8 | Resume interrupted downloads without re-downloading completed files | Must Have |
| FR-1.9 | Auto-detect rsync binary on all platforms (Windows: WSL/Cygwin detection) | Must Have |
| FR-1.10 | Provide installation instructions when rsync is missing | Must Have |
| FR-1.11 | Implement metadata-first sync strategy (sync RDF files first, build catalog, then sync content) | Must Have |

### FR-2: Metadata Management
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-2.1 | Parse and store RDF/XML metadata for all ebooks | Must Have |
| FR-2.2 | Build searchable local catalog database | Must Have |
| FR-2.3 | Extract: title, author(s), language, subjects, publication date, formats | Must Have |
| FR-2.4 | Track file locations and sizes in local database | Must Have |
| FR-2.5 | Support catalog queries by title, author, subject, language, date range | Should Have |
| FR-2.6 | Export catalog to JSON/CSV for external tools | Should Have |
| FR-2.7 | Map language names to ISO 639-1 codes automatically | Must Have |
| FR-2.8 | Accept both language names and ISO codes in queries/filters | Must Have |

### FR-3: Content Extraction (RAG Preparation)
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-3.1 | Extract plain text from downloaded archives | Must Have |
| FR-3.2 | Strip Project Gutenberg headers/footers (license text) | Must Have |
| FR-3.3 | Normalize text encoding to UTF-8 | Must Have |
| FR-3.4 | Chunk text into configurable segments for RAG embedding | Should Have |
| FR-3.5 | Include full metadata (book ID, title, authors, language, subjects, chunk index) in RAG chunk output | Must Have |
| FR-3.6 | Generate extraction reports with statistics | Should Have |
| FR-3.7 | Support batch extraction with parallelization | Should Have |
| FR-3.8 | Support incremental extraction (only extract new/changed files or when parameters change) | Must Have |
| FR-3.9 | Support selective extraction based on catalog queries (author, subject, date range, book IDs) | Should Have |
| FR-3.10 | Track extraction state (which files extracted, when, with what parameters) | Must Have |
| FR-3.11 | Support dry-run mode for extraction (preview what would be extracted) | Should Have |
| FR-3.12 | Support additional export formats (Parquet, Arrow, compressed JSON) | Should Have |
| FR-3.13 | Validate extracted chunks and report quality metrics | Should Have |

### FR-4: Progress and Reporting
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-4.1 | Display real-time sync progress (files, bytes, speed) | Must Have |
| FR-4.2 | Generate sync completion reports | Should Have |
| FR-4.3 | Track and report errors/failures | Must Have |
| FR-4.4 | Maintain sync history log | Should Have |
| FR-4.5 | Implement audit/verification system for concurrent operations | Must Have |
| FR-4.6 | Verify file integrity after sync operations | Must Have |
| FR-4.7 | Detect and report missing or corrupt files | Should Have |
| FR-4.8 | Provide health/status command showing archive state, catalog stats, extraction status | Should Have |

### FR-5: Configuration
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-5.1 | Configure target directory for archive storage | Must Have |
| FR-5.2 | Configure which content types to sync | Must Have |
| FR-5.3 | Configure sync schedule (for scheduled mode) | Should Have |
| FR-5.4 | Support configuration via JSON/YAML file | Must Have |
| FR-5.5 | Support command-line argument overrides | Should Have |
| FR-5.6 | Configure catalog database path (default: {targetDirectory}/gutenberg.db) | Must Have |
| FR-5.7 | Support relative and absolute database paths | Must Have |
| FR-5.8 | Validate configuration file on startup with clear error messages | Must Have |
| FR-5.9 | Support database maintenance operations (vacuum, optimize, backup, restore) | Should Have |

---

## Non-Functional Requirements

### NFR-1: Performance
| ID | Requirement | Target |
|----|-------------|--------|
| NFR-1.1 | Maximize download throughput within rate limits | 80%+ of available bandwidth |
| NFR-1.2 | Minimize unnecessary file checks | Less than 5% overhead on incremental sync |
| NFR-1.3 | Database queries for 76K+ records | Less than 500ms response time |
| NFR-1.4 | Text extraction throughput | 100+ books/minute |

### NFR-2: Reliability
| ID | Requirement | Target |
|----|-------------|--------|
| NFR-2.1 | Handle network interruptions gracefully | Auto-retry with backoff |
| NFR-2.2 | Maintain data integrity on failure | No partial/corrupt files |
| NFR-2.3 | Support resume after application restart | Full state recovery |
| NFR-2.4 | Support concurrent sync and extract operations | Safe concurrent access with verification |
| NFR-2.5 | Verify file integrity after writes | Checksum validation, size verification |

### NFR-3: Compliance
| ID | Requirement | Notes |
|----|-------------|-------|
| NFR-3.1 | Respect Project Gutenberg's Terms of Use | Required |
| NFR-3.2 | No direct website scraping | Use mirrors/rsync only |
| NFR-3.3 | Implement respectful request patterns | Rate limiting, backoff |
| NFR-3.4 | Include appropriate attribution in outputs | Per PG license |

### NFR-4: Compatibility
| ID | Requirement | Target |
|----|-------------|--------|
| NFR-4.1 | .NET version | .NET 8.0+ (LTS) |
| NFR-4.2 | Cross-platform support | Windows, Linux, macOS |
| NFR-4.3 | rsync availability | Auto-detect: Native Linux/macOS, WSL/Cygwin on Windows |
| NFR-4.4 | rsync installation guidance | Provide platform-specific instructions when missing |

### NFR-5: Maintainability
| ID | Requirement | Target |
|----|-------------|--------|
| NFR-5.1 | Code documentation | XML docs on all public APIs |
| NFR-5.2 | Unit test coverage | Greater than 70% coverage |
| NFR-5.3 | Logging framework | Structured logging (Serilog) |

---

## User Stories

### US-1: Initial Archive Download
> As a researcher, I want to download the complete Project Gutenberg text archive so that I have local access to all public domain books for my RAG system.

**Acceptance Criteria:**
- Can specify target directory
- Can filter to text-only formats
- Metadata catalog is built first from RDF files
- Catalog serves as checklist for content file sync
- Download completes successfully
- All files verified for integrity

### US-2: Incremental Update
> As a researcher, I want to update my local archive with only new/changed files so that I stay current without re-downloading everything.

**Acceptance Criteria:**
- Only changed files are downloaded
- Deleted files are optionally removed locally
- Update completes in reasonable time (less than 1 hour for typical daily delta)

### US-3: Extract Text for RAG
> As a developer, I want to extract clean text from downloaded books so that I can create embeddings for my vector database.

**Acceptance Criteria:**
- Headers/footers removed
- Text normalized to UTF-8
- Output in configurable chunk sizes
- Each chunk includes full metadata (book ID, title, authors, language, subjects, chunk index)
- Metadata preserved for attribution
- Incremental extraction only processes new/changed files
- Can extract based on catalog queries (author, subject, date range, book IDs)
- Dry-run mode shows what would be extracted without processing

### US-4: Search Local Catalog
> As a researcher, I want to search my local catalog by author/title/subject so that I can find specific books without internet access.

**Acceptance Criteria:**
- Search by multiple fields
- Language filtering accepts both ISO codes (e.g., "en") and names (e.g., "English")
- Automatic mapping between language names and ISO codes
- Results show file location and formats available
- Fast response (less than 500ms)

### US-5: Health and Status Monitoring
> As an administrator, I want to quickly check the status of my archive, catalog, and extraction state so that I can monitor system health.

**Acceptance Criteria:**
- Single command shows archive size, catalog statistics, extraction status
- Displays last sync time and extraction time
- Shows number of books synced vs. extracted
- Indicates any issues or warnings

### US-6: Incremental Extraction
> As a developer, I want to re-run extraction with different parameters without re-processing all files so that I can experiment with chunk sizes efficiently.

**Acceptance Criteria:**
- Only extracts files that haven't been extracted yet
- Re-extracts if source file changed or parameters changed
- Tracks extraction history in database
- Significantly faster than full extraction

---

## Constraints

1. **rsync dependency** - Primary sync method requires rsync binary (native on Linux/macOS, requires WSL or Cygwin on Windows). Application must auto-detect and provide installation instructions if missing.
2. **Storage requirements** - Full archive requires 1TB+; text-only requires 50GB+
3. **Initial sync time** - First sync may take 12-72 hours depending on bandwidth and content selection. Metadata-first approach allows catalog building before full content sync.
4. **No website scraping** - Must use official mirror infrastructure only
5. **Concurrent operations** - Sync and extract operations can run concurrently but require verification/auditing to ensure data integrity

---

## Out of Scope

- Web UI for browsing archive (CLI/library only for v1)
- Direct integration with specific RAG frameworks (output files only)
- Hosting/redistribution features
- OCR or image processing
- Audio transcription

---

## Success Criteria

1. Successfully mirror 95%+ of targeted content types
2. Incremental updates complete in less than 10% of initial sync time
3. Metadata catalog accurately reflects downloaded content
4. Clean text extraction works for 99%+ of text files
5. Application runs without intervention for scheduled daily updates
6. Code quality suitable for open-source publication
7. Incremental extraction completes in less than 10% of full extraction time
8. Configuration validation catches errors before operations begin
9. Health/status command provides actionable information in under 1 second
