# Build and Run Instructions

## Building from Project Root

If you're in the project root directory (`/home/jfinlon/Documents/Projects/Gutenberg Archive`):

### Quick Build (Self-Contained)
```bash
./publish.sh
```

### Manual Build
```bash
# Self-contained (includes .NET runtime, ~146MB)
dotnet publish src/GutenbergSync.Cli/GutenbergSync.Cli.csproj \
    -c Release \
    -o ./publish \
    --self-contained true \
    -r linux-x64
```

## Running from Publish Directory

If you're in the `publish` directory:

### 1. Initialize Configuration
```bash
cd publish
./gutenberg-sync config init
```

Edit `config.json` (or create it in the parent directory) to set:
```json
{
  "Sync": {
    "TargetDirectory": "/mnt/workspace/Gutenberg"
  }
}
```

### 2. Sync Metadata First (Recommended)
```bash
./gutenberg-sync sync -t /mnt/workspace/Gutenberg -p text-epub --metadata-only
```

### 3. Sync Full Content
```bash
./gutenberg-sync sync -t /mnt/workspace/Gutenberg -p text-epub
```

### 4. Extract Text for RAG
```bash
./gutenberg-sync extract -i /mnt/workspace/Gutenberg -o /mnt/workspace/Gutenberg-text
```

## Complete Workflow Example

```bash
# From project root
cd ~/Documents/Projects/Gutenberg\ Archive

# Build
./publish.sh

# Go to publish directory
cd publish

# Initialize config (optional, if you want a config file)
./gutenberg-sync config init

# Sync metadata first (builds catalog)
./gutenberg-sync sync -t /mnt/workspace/Gutenberg -p text-epub --metadata-only

# Sync content files
./gutenberg-sync sync -t /mnt/workspace/Gutenberg -p text-epub

# Extract text chunks
./gutenberg-sync extract -i /mnt/workspace/Gutenberg -o /mnt/workspace/Gutenberg-text

# Check health/status
./gutenberg-sync health
```

## Alternative: Run from Project Root

You can also run directly from the project root without changing directories:

```bash
# From project root
./publish/gutenberg-sync sync -t /mnt/workspace/Gutenberg -p text-epub
./publish/gutenberg-sync extract -i /mnt/workspace/Gutenberg -o /mnt/workspace/Gutenberg-text
```

## Common Commands

```bash
# Sync (text + EPUB, ~50GB)
./gutenberg-sync sync -t /mnt/workspace/Gutenberg -p text-epub

# Sync text-only (smallest, ~15GB)
./gutenberg-sync sync -t /mnt/workspace/Gutenberg -p text-only

# Dry-run to preview
./gutenberg-sync sync -t /mnt/workspace/Gutenberg -p text-epub --dry-run

# Extract with custom chunk size
./gutenberg-sync extract -i /mnt/workspace/Gutenberg -o /mnt/workspace/Gutenberg-text --chunk-size 1000

# Search catalog
./gutenberg-sync catalog search --query "Shakespeare"

# Check system health
./gutenberg-sync health

# Audit files
./gutenberg-sync audit scan --directory /mnt/workspace/Gutenberg
```

