#!/bin/bash
# cleanup_orphaned_files.sh - Delete files for books not in database

DB_PATH="/mnt/workspace/gutenberg/gutenberg.db"
TARGET_DIR="/mnt/workspace/gutenberg"

echo "=== Cleaning up orphaned files ==="
echo "Deleting files for books not in database..."

# Get all book IDs from database
DB_BOOK_IDS=$(sqlite3 "$DB_PATH" "SELECT book_id FROM ebooks;" | sort -n)

# Count EPUB directories
EPUB_DIRS=$(find "$TARGET_DIR/gutenberg-epub" -maxdepth 1 -type d -name "[0-9]*" | wc -l)
echo "Found $EPUB_DIRS EPUB directories"

# Delete EPUB directories for books not in database
DELETED=0
for epub_dir in "$TARGET_DIR/gutenberg-epub"/[0-9]*/; do
    if [ -d "$epub_dir" ]; then
        book_id=$(basename "$epub_dir")
        # Check if book_id exists in database
        if ! echo "$DB_BOOK_IDS" | grep -q "^${book_id}$"; then
            echo "Deleting orphaned EPUB: $book_id"
            rm -rf "$epub_dir"
            ((DELETED++))
        fi
    fi
done

echo "Deleted $DELETED orphaned EPUB directories"

# For main collection (hierarchical), it's more complex
# We'll check each directory and see if the book_id exists
MAIN_DELETED=0
if [ -d "$TARGET_DIR/gutenberg" ]; then
    # Find all book directories (pattern: 1/2/3/4/12345/)
    find "$TARGET_DIR/gutenberg" -type d -regextype posix-extended -regex '.*/[0-9]/[0-9]/[0-9]/[0-9]/[0-9]+$' | while read dir; do
        book_id=$(basename "$dir")
        # Check if book_id exists in database
        if ! echo "$DB_BOOK_IDS" | grep -q "^${book_id}$"; then
            echo "Deleting orphaned main: $dir"
            rm -rf "$dir"
            ((MAIN_DELETED++))
        fi
    done
fi

echo "Deleted $MAIN_DELETED orphaned main collection directories"
echo "=== Cleanup complete ==="
