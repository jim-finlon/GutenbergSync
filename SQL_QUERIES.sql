-- GutenbergSync SQL Queries for SQLite Browser
-- Database: /mnt/workspace/gutenberg/gutenberg.db

-- ============================================================================
-- SEARCH BY TITLE
-- ============================================================================

-- 1. Search books by title (case-insensitive, partial match)
SELECT 
    e.book_id,
    e.title,
    e.language,
    e.publication_date,
    GROUP_CONCAT(a.name, '; ') as authors
FROM ebooks e
LEFT JOIN ebook_authors ea ON e.book_id = ea.ebook_id
LEFT JOIN authors a ON ea.author_id = a.id
WHERE LOWER(e.title) LIKE LOWER('%pride%')
GROUP BY e.book_id, e.title, e.language, e.publication_date
ORDER BY e.title
LIMIT 50;

-- 2. Exact title match
SELECT 
    e.book_id,
    e.title,
    e.language,
    GROUP_CONCAT(a.name, '; ') as authors
FROM ebooks e
LEFT JOIN ebook_authors ea ON e.book_id = ea.ebook_id
LEFT JOIN authors a ON ea.author_id = a.id
WHERE e.title = 'Pride and Prejudice'
GROUP BY e.book_id, e.title, e.language;

-- 3. Title starts with (useful for browsing)
SELECT 
    e.book_id,
    e.title,
    e.language,
    GROUP_CONCAT(a.name, '; ') as authors
FROM ebooks e
LEFT JOIN ebook_authors ea ON e.book_id = ea.ebook_id
LEFT JOIN authors a ON ea.author_id = a.id
WHERE LOWER(e.title) LIKE LOWER('The %')
GROUP BY e.book_id, e.title, e.language
ORDER BY e.title
LIMIT 100;

-- ============================================================================
-- SEARCH BY AUTHOR
-- ============================================================================

-- 4. Search books by author name (case-insensitive, partial match)
SELECT 
    e.book_id,
    e.title,
    e.language,
    e.publication_date,
    GROUP_CONCAT(a.name, '; ') as authors
FROM ebooks e
INNER JOIN ebook_authors ea ON e.book_id = ea.ebook_id
INNER JOIN authors a ON ea.author_id = a.id
WHERE LOWER(a.name) LIKE LOWER('%shakespeare%')
GROUP BY e.book_id, e.title, e.language, e.publication_date
ORDER BY e.title;

-- 5. Find all books by a specific author (exact match)
SELECT 
    e.book_id,
    e.title,
    e.language,
    e.publication_date
FROM ebooks e
INNER JOIN ebook_authors ea ON e.book_id = ea.ebook_id
INNER JOIN authors a ON ea.author_id = a.id
WHERE a.name = 'Shakespeare, William'
ORDER BY e.title;

-- 6. List all authors and their book counts
SELECT 
    a.name,
    COUNT(DISTINCT ea.ebook_id) as book_count
FROM authors a
LEFT JOIN ebook_authors ea ON a.id = ea.author_id
GROUP BY a.id, a.name
HAVING book_count > 0
ORDER BY book_count DESC
LIMIT 50;

-- 7. Find authors with most books
SELECT 
    a.name,
    COUNT(DISTINCT ea.ebook_id) as book_count
FROM authors a
INNER JOIN ebook_authors ea ON a.id = ea.author_id
GROUP BY a.id, a.name
ORDER BY book_count DESC
LIMIT 20;

-- ============================================================================
-- COMBINED SEARCHES
-- ============================================================================

-- 8. Search by title AND author
SELECT 
    e.book_id,
    e.title,
    e.language,
    GROUP_CONCAT(a.name, '; ') as authors
FROM ebooks e
INNER JOIN ebook_authors ea ON e.book_id = ea.ebook_id
INNER JOIN authors a ON ea.author_id = a.id
WHERE LOWER(e.title) LIKE LOWER('%sherlock%')
  AND LOWER(a.name) LIKE LOWER('%doyle%')
GROUP BY e.book_id, e.title, e.language
ORDER BY e.title;

-- 9. Search by title OR author
SELECT 
    e.book_id,
    e.title,
    e.language,
    GROUP_CONCAT(a.name, '; ') as authors
FROM ebooks e
LEFT JOIN ebook_authors ea ON e.book_id = ea.ebook_id
LEFT JOIN authors a ON ea.author_id = a.id
WHERE LOWER(e.title) LIKE LOWER('%adventure%')
   OR LOWER(a.name) LIKE LOWER('%adventure%')
GROUP BY e.book_id, e.title, e.language
ORDER BY e.title
LIMIT 50;

-- ============================================================================
-- SEARCH BY LANGUAGE
-- ============================================================================

-- 10. Find all English books
SELECT 
    e.book_id,
    e.title,
    GROUP_CONCAT(a.name, '; ') as authors
FROM ebooks e
LEFT JOIN ebook_authors ea ON e.book_id = ea.ebook_id
LEFT JOIN authors a ON ea.author_id = a.id
WHERE e.language_iso_code = 'en' OR LOWER(e.language) LIKE '%english%'
GROUP BY e.book_id, e.title
ORDER BY e.title
LIMIT 100;

-- 11. Books by language (count)
SELECT 
    e.language,
    e.language_iso_code,
    COUNT(*) as book_count
FROM ebooks e
WHERE e.language IS NOT NULL
GROUP BY e.language, e.language_iso_code
ORDER BY book_count DESC;

-- ============================================================================
-- SEARCH BY SUBJECT
-- ============================================================================

-- 12. Find books by subject
SELECT 
    e.book_id,
    e.title,
    GROUP_CONCAT(a.name, '; ') as authors,
    GROUP_CONCAT(es.subject, '; ') as subjects
FROM ebooks e
INNER JOIN ebook_subjects es ON e.book_id = es.ebook_id
LEFT JOIN ebook_authors ea ON e.book_id = ea.ebook_id
LEFT JOIN authors a ON ea.author_id = a.id
WHERE LOWER(es.subject) LIKE LOWER('%fiction%')
GROUP BY e.book_id, e.title
ORDER BY e.title
LIMIT 50;

-- 13. Most common subjects
SELECT 
    es.subject,
    COUNT(DISTINCT es.ebook_id) as book_count
FROM ebook_subjects es
GROUP BY es.subject
ORDER BY book_count DESC
LIMIT 30;

-- ============================================================================
-- STATISTICS AND EXPLORATION
-- ============================================================================

-- 14. Database statistics
SELECT 
    (SELECT COUNT(*) FROM ebooks) as total_books,
    (SELECT COUNT(*) FROM authors) as total_authors,
    (SELECT COUNT(DISTINCT language) FROM ebooks WHERE language IS NOT NULL) as unique_languages,
    (SELECT COUNT(DISTINCT subject) FROM ebook_subjects) as unique_subjects;

-- 15. Books with multiple authors
SELECT 
    e.book_id,
    e.title,
    COUNT(DISTINCT ea.author_id) as author_count,
    GROUP_CONCAT(a.name, '; ') as authors
FROM ebooks e
INNER JOIN ebook_authors ea ON e.book_id = ea.ebook_id
INNER JOIN authors a ON ea.author_id = a.id
GROUP BY e.book_id, e.title
HAVING author_count > 1
ORDER BY author_count DESC
LIMIT 20;

-- 16. Recent books (if publication_date is available)
SELECT 
    e.book_id,
    e.title,
    e.publication_date,
    GROUP_CONCAT(a.name, '; ') as authors
FROM ebooks e
LEFT JOIN ebook_authors ea ON e.book_id = ea.ebook_id
LEFT JOIN authors a ON ea.author_id = a.id
WHERE e.publication_date IS NOT NULL
GROUP BY e.book_id, e.title, e.publication_date
ORDER BY e.publication_date DESC
LIMIT 50;

-- 17. Books with most subjects
SELECT 
    e.book_id,
    e.title,
    COUNT(DISTINCT es.subject) as subject_count,
    GROUP_CONCAT(es.subject, '; ') as subjects
FROM ebooks e
INNER JOIN ebook_subjects es ON e.book_id = es.ebook_id
GROUP BY e.book_id, e.title
ORDER BY subject_count DESC
LIMIT 20;

-- ============================================================================
-- QUICK LOOKUP BY BOOK ID
-- ============================================================================

-- 18. Get full details for a specific book ID
SELECT 
    e.book_id,
    e.title,
    e.language,
    e.language_iso_code,
    e.publication_date,
    e.download_count,
    GROUP_CONCAT(DISTINCT a.name, '; ') as authors,
    GROUP_CONCAT(DISTINCT es.subject, '; ') as subjects,
    GROUP_CONCAT(DISTINCT eb.bookshelf, '; ') as bookshelves
FROM ebooks e
LEFT JOIN ebook_authors ea ON e.book_id = ea.ebook_id
LEFT JOIN authors a ON ea.author_id = a.id
LEFT JOIN ebook_subjects es ON e.book_id = es.ebook_id
LEFT JOIN ebook_bookshelves eb ON e.book_id = eb.ebook_id
WHERE e.book_id = 1342
GROUP BY e.book_id, e.title, e.language, e.language_iso_code, e.publication_date, e.download_count;

-- ============================================================================
-- USEFUL PATTERNS
-- ============================================================================

-- 19. Find books with "and" in title (common pattern)
SELECT 
    e.book_id,
    e.title,
    GROUP_CONCAT(a.name, '; ') as authors
FROM ebooks e
LEFT JOIN ebook_authors ea ON e.book_id = ea.ebook_id
LEFT JOIN authors a ON ea.author_id = a.id
WHERE e.title LIKE '% and %'
GROUP BY e.book_id, e.title
ORDER BY e.title
LIMIT 50;

-- 20. Search for books with specific word count pattern in title
SELECT 
    e.book_id,
    e.title,
    GROUP_CONCAT(a.name, '; ') as authors
FROM ebooks e
LEFT JOIN ebook_authors ea ON e.book_id = ea.ebook_id
LEFT JOIN authors a ON ea.author_id = a.id
WHERE LENGTH(e.title) - LENGTH(REPLACE(e.title, ' ', '')) + 1 BETWEEN 2 AND 4  -- 2-4 words
GROUP BY e.book_id, e.title
ORDER BY e.title
LIMIT 50;

-- ============================================================================
-- LANGUAGE FILTERING (For English-Only Setup)
-- ============================================================================

-- 21. See all languages and their book counts
SELECT 
    COALESCE(language_iso_code, language, 'Unknown') as lang_code,
    language,
    COUNT(*) as book_count
FROM ebooks
WHERE language IS NOT NULL OR language_iso_code IS NOT NULL
GROUP BY COALESCE(language_iso_code, language, 'Unknown'), language
ORDER BY book_count DESC;

-- 22. Count English vs non-English
SELECT 
    CASE 
        WHEN language_iso_code = 'en' OR LOWER(language) LIKE '%english%' THEN 'English'
        ELSE 'Non-English'
    END as language_category,
    COUNT(*) as book_count
FROM ebooks
GROUP BY language_category;

-- 23. Get all non-English book IDs (for deletion)
SELECT 
    book_id,
    title,
    language,
    language_iso_code
FROM ebooks
WHERE (language_iso_code IS NULL OR language_iso_code != 'en')
  AND (language IS NULL OR LOWER(language) NOT LIKE '%english%')
ORDER BY book_id;

-- 24. Get only English books
SELECT 
    e.book_id,
    e.title,
    e.language,
    e.language_iso_code,
    GROUP_CONCAT(a.name, '; ') as authors
FROM ebooks e
LEFT JOIN ebook_authors ea ON e.book_id = ea.ebook_id
LEFT JOIN authors a ON ea.author_id = a.id
WHERE e.language_iso_code = 'en' OR LOWER(e.language) LIKE '%english%'
GROUP BY e.book_id, e.title, e.language, e.language_iso_code
ORDER BY e.title
LIMIT 100;
