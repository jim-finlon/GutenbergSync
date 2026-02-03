# Guide: Keep Only English Books

This guide shows how to:
1. Remove all non-English books from the database
2. Remove all non-English book files from the file system
3. Configure future syncs to only process English books

## Important Notes

⚠️ **Language filtering is NOT implemented at the rsync level** - rsync will still download all files. However, we can:
- Filter the catalog to only English books before syncing content
- Delete non-English files after each sync
- Or manually manage which books to sync

## Step 1: Identify Non-English Books

First, let's see what languages you have:

```sql
-- See all languages and their book counts
SELECT 
    COALESCE(language_iso_code, language, 'Unknown') as lang_code,
    language,
    COUNT(*) as book_count
FROM ebooks
WHERE language IS NOT NULL OR language_iso_code IS NOT NULL
GROUP BY COALESCE(language_iso_code, language, 'Unknown'), language
ORDER BY book_count DESC;
```

```sql
-- Count English vs non-English
SELECT 
    CASE 
        WHEN language_iso_code = 'en' OR LOWER(language) LIKE '%english%' THEN 'English'
        ELSE 'Non-English'
    END as language_category,
    COUNT(*) as book_count
FROM ebooks
GROUP BY language_category;
```

## Step 2: Get List of Non-English Book IDs

```sql
-- Get all non-English book IDs
SELECT book_id, title, language, language_iso_code
FROM ebooks
WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
  AND (language IS NULL OR LOWER(language) NOT LIKE '%english%')
ORDER BY book_id;
```

Save this list to a file for reference:
```sql
-- Export non-English book IDs to a text file (run in SQLite Browser)
.mode csv
.headers on
.output non_english_books.csv
SELECT book_id, title, language, language_iso_code
FROM ebooks
WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
  AND (language IS NULL OR LOWER(language) NOT LIKE '%english%')
ORDER BY book_id;
.quit
```

## Step 3: Delete Non-English Books from Database

⚠️ **BACKUP YOUR DATABASE FIRST!**

```bash
# Backup the database
cp /mnt/workspace/gutenberg/gutenberg.db /mnt/workspace/gutenberg/gutenberg.db.backup
```

Then run these SQL commands in SQLite Browser:

```sql
-- Step 1: Delete from junction tables first (to avoid foreign key issues)
DELETE FROM ebook_authors 
WHERE ebook_id IN (
    SELECT book_id 
    FROM ebooks 
    WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
      AND (language IS NULL OR LOWER(language) NOT LIKE '%english%')
);

DELETE FROM ebook_subjects 
WHERE ebook_id IN (
    SELECT book_id 
    FROM ebooks 
    WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
      AND (language IS NULL OR LOWER(language) NOT LIKE '%english%')
);

DELETE FROM ebook_bookshelves 
WHERE ebook_id IN (
    SELECT book_id 
    FROM ebooks 
    WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
      AND (language IS NULL OR LOWER(language) NOT LIKE '%english%')
);

-- Step 2: Delete the books themselves
DELETE FROM ebooks 
WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
  AND (language IS NULL OR LOWER(language) NOT LIKE '%english%');

-- Step 3: Clean up orphaned authors (authors with no books)
DELETE FROM authors 
WHERE id NOT IN (SELECT DISTINCT author_id FROM ebook_authors);

-- Step 4: Verify the deletion
SELECT COUNT(*) as remaining_books FROM ebooks;
SELECT COUNT(*) as english_books FROM ebooks 
WHERE language_iso_code = 'en' OR LOWER(language) LIKE '%english%';
```

## Step 4: Delete Non-English Files from File System

The file structure in Project Gutenberg is typically:
- `/mnt/workspace/gutenberg/gutenberg-epub/12345/` (flat structure for EPUBs)
- `/mnt/workspace/gutenberg/gutenberg/1/2/3/4/12345/` (hierarchical for main collection)

**Option A: Delete based on book IDs (Recommended)**

First, get the list of non-English book IDs from the database, then:

```bash
#!/bin/bash
# delete_non_english_files.sh

TARGET_DIR="/mnt/workspace/gutenberg"
DB_PATH="/mnt/workspace/gutenberg/gutenberg.db"

# Get non-English book IDs from database
sqlite3 "$DB_PATH" <<EOF
.mode list
SELECT book_id FROM ebooks 
WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
  AND (language IS NULL OR LOWER(language) NOT LIKE '%english%');
EOF | while read book_id; do
    if [ -n "$book_id" ]; then
        # Delete from EPUB directory (flat structure)
        if [ -d "$TARGET_DIR/gutenberg-epub/$book_id" ]; then
            echo "Deleting EPUB directory: $book_id"
            rm -rf "$TARGET_DIR/gutenberg-epub/$book_id"
        fi
        
        # Delete from main collection (hierarchical: 1/2/3/4/12345)
        # Calculate path: book_id 12345 -> 1/2/3/4/12345
        if [ ${#book_id} -ge 5 ]; then
            path_part="${book_id:0:1}/${book_id:1:1}/${book_id:2:1}/${book_id:3:1}/$book_id"
            if [ -d "$TARGET_DIR/gutenberg/$path_part" ]; then
                echo "Deleting main collection directory: $path_part"
                rm -rf "$TARGET_DIR/gutenberg/$path_part"
            fi
        fi
    fi
done

echo "Done deleting non-English files"
```

**Option B: Delete all files, then re-sync only English books**

This is simpler but requires re-downloading:

```bash
# 1. Delete all files
rm -rf /mnt/workspace/gutenberg/gutenberg-epub/*
rm -rf /mnt/workspace/gutenberg/gutenberg/*

# 2. Re-sync metadata (rebuilds catalog with only what's in RDF files)
# 3. Delete non-English from database (Step 3 above)
# 4. Re-sync content (will only sync books that exist in catalog)
```

## Step 5: Future Syncs - English Only

Since language filtering isn't implemented at the rsync level, you have a few options:

### Option A: Filter Catalog Before Content Sync (Recommended)

Modify the sync process to only sync books that are English in the catalog. You would need to modify `SyncOrchestrator.cs` to filter the catalog query:

```csharp
// In SyncContentAsync method, change:
var allBooks = await _catalogRepository.SearchAsync(
    new CatalogSearchOptions { Limit = null },
    cancellationToken);

// To:
var allBooks = await _catalogRepository.SearchAsync(
    new CatalogSearchOptions { 
        Limit = null,
        Language = "en"  // Only English books
    },
    cancellationToken);
```

### Option B: Post-Sync Cleanup Script

After each sync, run a cleanup script:

```bash
#!/bin/bash
# cleanup_non_english_after_sync.sh

DB_PATH="/mnt/workspace/gutenberg/gutenberg.db"
TARGET_DIR="/mnt/workspace/gutenberg"

# Get non-English book IDs
sqlite3 "$DB_PATH" <<EOF | while read book_id; do
.mode list
SELECT book_id FROM ebooks 
WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
  AND (language IS NULL OR LOWER(language) NOT LIKE '%english%');
EOF
    if [ -n "$book_id" ]; then
        # Delete EPUB files
        rm -rf "$TARGET_DIR/gutenberg-epub/$book_id"
        
        # Delete main collection files
        if [ ${#book_id} -ge 5 ]; then
            path_part="${book_id:0:1}/${book_id:1:1}/${book_id:2:1}/${book_id:3:1}/$book_id"
            rm -rf "$TARGET_DIR/gutenberg/$path_part"
        fi
    fi
done

# Also remove from database
sqlite3 "$DB_PATH" <<EOF
DELETE FROM ebook_authors 
WHERE ebook_id IN (
    SELECT book_id FROM ebooks 
    WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
      AND (language IS NULL OR LOWER(language) NOT LIKE '%english%')
);

DELETE FROM ebook_subjects 
WHERE ebook_id IN (
    SELECT book_id FROM ebooks 
    WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
      AND (language IS NULL OR LOWER(language) NOT LIKE '%english%')
);

DELETE FROM ebook_bookshelves 
WHERE ebook_id IN (
    SELECT book_id FROM ebooks 
    WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
      AND (language IS NULL OR LOWER(language) NOT LIKE '%english%')
);

DELETE FROM ebooks 
WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
  AND (language IS NULL OR LOWER(language) NOT LIKE '%english%');
EOF

echo "Cleanup complete"
```

### Option C: Manual Management

1. Sync metadata only: `--metadata-only`
2. Review and delete non-English from database
3. Sync content (will only sync books in catalog)

## Complete One-Time Cleanup Script

Here's a complete script to do everything at once:

```bash
#!/bin/bash
# cleanup_non_english.sh - Complete cleanup of non-English books

set -e  # Exit on error

DB_PATH="/mnt/workspace/gutenberg/gutenberg.db"
TARGET_DIR="/mnt/workspace/gutenberg"
BACKUP_DIR="/mnt/workspace/gutenberg/backup_$(date +%Y%m%d_%H%M%S)"

echo "=== Non-English Book Cleanup ==="
echo "Database: $DB_PATH"
echo "Target: $TARGET_DIR"
echo ""

# Backup database
echo "1. Backing up database..."
mkdir -p "$(dirname $DB_PATH)"
cp "$DB_PATH" "${DB_PATH}.backup_$(date +%Y%m%d_%H%M%S)"

# Get count before
BEFORE_COUNT=$(sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM ebooks;")
echo "   Total books before: $BEFORE_COUNT"

# Get non-English book IDs
echo "2. Finding non-English books..."
NON_ENGLISH_IDS=$(sqlite3 "$DB_PATH" <<EOF
.mode list
SELECT book_id FROM ebooks 
WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
  AND (language IS NULL OR LOWER(language) NOT LIKE '%english%');
EOF
)

NON_ENGLISH_COUNT=$(echo "$NON_ENGLISH_IDS" | wc -l)
echo "   Non-English books found: $NON_ENGLISH_COUNT"

if [ "$NON_ENGLISH_COUNT" -eq 0 ]; then
    echo "   No non-English books found. Exiting."
    exit 0
fi

# Confirm
read -p "3. Delete $NON_ENGLISH_COUNT non-English books? (yes/no): " confirm
if [ "$confirm" != "yes" ]; then
    echo "   Cancelled."
    exit 0
fi

# Delete from database
echo "4. Deleting from database..."
sqlite3 "$DB_PATH" <<EOF
BEGIN TRANSACTION;

-- Delete junction tables
DELETE FROM ebook_authors 
WHERE ebook_id IN (
    SELECT book_id FROM ebooks 
    WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
      AND (language IS NULL OR LOWER(language) NOT LIKE '%english%')
);

DELETE FROM ebook_subjects 
WHERE ebook_id IN (
    SELECT book_id FROM ebooks 
    WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
      AND (language IS NULL OR LOWER(language) NOT LIKE '%english%')
);

DELETE FROM ebook_bookshelves 
WHERE ebook_id IN (
    SELECT book_id FROM ebooks 
    WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
      AND (language IS NULL OR LOWER(language) NOT LIKE '%english%')
);

-- Delete books
DELETE FROM ebooks 
WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
  AND (language IS NULL OR LOWER(language) NOT LIKE '%english%');

-- Clean up orphaned authors
DELETE FROM authors 
WHERE id NOT IN (SELECT DISTINCT author_id FROM ebook_authors);

COMMIT;

-- Verify
SELECT 'Remaining books: ' || COUNT(*) FROM ebooks;
SELECT 'English books: ' || COUNT(*) FROM ebooks 
WHERE language_iso_code = 'en' OR LOWER(language) LIKE '%english%';
EOF

# Delete files
echo "5. Deleting files..."
DELETED=0
for book_id in $NON_ENGLISH_IDS; do
    if [ -n "$book_id" ]; then
        # EPUB directory
        if [ -d "$TARGET_DIR/gutenberg-epub/$book_id" ]; then
            rm -rf "$TARGET_DIR/gutenberg-epub/$book_id"
            ((DELETED++))
        fi
        
        # Main collection (hierarchical)
        if [ ${#book_id} -ge 5 ]; then
            path_part="${book_id:0:1}/${book_id:1:1}/${book_id:2:1}/${book_id:3:1}/$book_id"
            if [ -d "$TARGET_DIR/gutenberg/$path_part" ]; then
                rm -rf "$TARGET_DIR/gutenberg/$path_part"
                ((DELETED++))
            fi
        fi
    fi
done

echo "   Deleted $DELETED file directories"

# Final count
AFTER_COUNT=$(sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM ebooks;")
echo ""
echo "=== Cleanup Complete ==="
echo "Books before: $BEFORE_COUNT"
echo "Books after: $AFTER_COUNT"
echo "Removed: $((BEFORE_COUNT - AFTER_COUNT))"
```

## Verification Queries

After cleanup, verify everything:

```sql
-- Should show only English books
SELECT 
    language_iso_code,
    language,
    COUNT(*) as count
FROM ebooks
GROUP BY language_iso_code, language
ORDER BY count DESC;

-- Should return 0
SELECT COUNT(*) as non_english_count
FROM ebooks
WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
  AND (language IS NULL OR LOWER(language) NOT LIKE '%english%');
```

## Summary

1. **Backup your database first!**
2. Use SQL queries to identify and delete non-English books from the database
3. Use shell scripts to delete non-English files from the file system
4. For future syncs, either:
   - Modify the code to filter catalog queries by language
   - Run a cleanup script after each sync
   - Manually manage which books to sync

The database cleanup is straightforward with SQL. The file cleanup requires matching book IDs to directory structures.
