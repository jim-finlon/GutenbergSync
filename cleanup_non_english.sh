#!/bin/bash
# cleanup_non_english.sh - Complete cleanup of non-English books

set -e  # Exit on error

DB_PATH="/mnt/workspace/gutenberg/gutenberg.db"
TARGET_DIR="/mnt/workspace/gutenberg"

echo "=== Non-English Book Cleanup ==="
echo "Database: $DB_PATH"
echo "Target: $TARGET_DIR"
echo ""

# Check if database exists
if [ ! -f "$DB_PATH" ]; then
    echo "ERROR: Database not found at $DB_PATH"
    exit 1
fi

# Backup database
echo "1. Backing up database..."
BACKUP_PATH="${DB_PATH}.backup_$(date +%Y%m%d_%H%M%S)"
cp "$DB_PATH" "$BACKUP_PATH"
echo "   Backup created: $BACKUP_PATH"

# Get count before
BEFORE_COUNT=$(sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM ebooks;" 2>/dev/null || echo "0")
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

NON_ENGLISH_COUNT=$(echo "$NON_ENGLISH_IDS" | grep -v '^$' | wc -l)
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
WHERE id NOT IN (SELECT DISTINCT author_id FROM ebook_authors WHERE author_id IS NOT NULL);

COMMIT;
EOF

# Delete files
echo "5. Deleting files..."
DELETED=0
for book_id in $NON_ENGLISH_IDS; do
    book_id=$(echo "$book_id" | tr -d '[:space:]')
    if [ -n "$book_id" ] && [ "$book_id" != "" ]; then
        # EPUB directory (flat structure: gutenberg-epub/12345/)
        if [ -d "$TARGET_DIR/gutenberg-epub/$book_id" ]; then
            echo "   Deleting EPUB: $book_id"
            rm -rf "$TARGET_DIR/gutenberg-epub/$book_id"
            ((DELETED++))
        fi
        
        # Main collection (hierarchical: gutenberg/1/2/3/4/12345/)
        if [ ${#book_id} -ge 5 ]; then
            path_part="${book_id:0:1}/${book_id:1:1}/${book_id:2:1}/${book_id:3:1}/$book_id"
            if [ -d "$TARGET_DIR/gutenberg/$path_part" ]; then
                echo "   Deleting main: $path_part"
                rm -rf "$TARGET_DIR/gutenberg/$path_part"
                ((DELETED++))
            fi
        fi
    fi
done

echo "   Deleted $DELETED file directories"

# Final count
AFTER_COUNT=$(sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM ebooks;" 2>/dev/null || echo "0")
ENGLISH_COUNT=$(sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM ebooks WHERE language_iso_code = 'en' OR LOWER(language) LIKE '%english%';" 2>/dev/null || echo "0")

echo ""
echo "=== Cleanup Complete ==="
echo "Books before: $BEFORE_COUNT"
echo "Books after: $AFTER_COUNT"
echo "English books: $ENGLISH_COUNT"
echo "Removed: $((BEFORE_COUNT - AFTER_COUNT))"
echo ""
echo "Database backup: $BACKUP_PATH"
