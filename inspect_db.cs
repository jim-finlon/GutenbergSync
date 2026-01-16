using System;
using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;

var dbPath = "/mnt/workspace/gutenberg/gutenberg.db";
var connectionString = $"Data Source={dbPath};";

Console.WriteLine($"Connecting to: {dbPath}");
Console.WriteLine($"File exists: {System.IO.File.Exists(dbPath)}");
Console.WriteLine();

using var connection = new SqliteConnection(connectionString);
await connection.OpenAsync();

Console.WriteLine("=== ALL TABLES ===");
var tables = await connection.QueryAsync<string>("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name");
foreach (var table in tables)
{
    Console.WriteLine($"  - {table}");
}

Console.WriteLine("\n=== EBOOKS TABLE SCHEMA ===");
var columns = await connection.QueryAsync<(string name, string type, int notnull, object? dflt_value, int pk)>(
    "PRAGMA table_info(ebooks)");
foreach (var col in columns)
{
    var nullable = col.notnull == 0 ? "NULL" : "NOT NULL";
    var pk = col.pk == 1 ? " PRIMARY KEY" : "";
    var defaultValue = col.dflt_value != null ? $" DEFAULT {col.dflt_value}" : "";
    Console.WriteLine($"  {col.name,-25} {col.type,-15} {nullable}{pk}{defaultValue}");
}

Console.WriteLine("\n=== AUTHORS TABLE SCHEMA ===");
var authorColumns = await connection.QueryAsync<(string name, string type, int notnull, object? dflt_value, int pk)>(
    "PRAGMA table_info(authors)");
foreach (var col in authorColumns)
{
    var nullable = col.notnull == 0 ? "NULL" : "NOT NULL";
    var pk = col.pk == 1 ? " PRIMARY KEY" : "";
    Console.WriteLine($"  {col.name,-25} {col.type,-15} {nullable}{pk}");
}

Console.WriteLine("\n=== EBOOK_SUBJECTS TABLE SCHEMA ===");
var subjectColumns = await connection.QueryAsync<(string name, string type, int notnull, object? dflt_value, int pk)>(
    "PRAGMA table_info(ebook_subjects)");
foreach (var col in subjectColumns)
{
    var nullable = col.notnull == 0 ? "NULL" : "NOT NULL";
    var pk = col.pk == 1 ? " PRIMARY KEY" : "";
    Console.WriteLine($"  {col.name,-25} {col.type,-15} {nullable}{pk}");
}

Console.WriteLine("\n=== ACTUAL STATISTICS ===");
try
{
    var totalBooks = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM ebooks");
    Console.WriteLine($"  Total Books: {totalBooks}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Error counting books: {ex.Message}");
}

try
{
    var totalAuthors = await connection.QuerySingleAsync<int>("SELECT COUNT(DISTINCT id) FROM authors");
    Console.WriteLine($"  Total Authors: {totalAuthors}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Error counting authors: {ex.Message}");
}

try
{
    var uniqueLanguages = await connection.QuerySingleAsync<int>("SELECT COUNT(DISTINCT language_iso_code) FROM ebooks WHERE language_iso_code IS NOT NULL");
    Console.WriteLine($"  Unique Languages: {uniqueLanguages}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Error counting languages: {ex.Message}");
}

try
{
    var uniqueSubjects = await connection.QuerySingleAsync<int>("SELECT COUNT(DISTINCT subject) FROM ebook_subjects");
    Console.WriteLine($"  Unique Subjects: {uniqueSubjects}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Error counting subjects: {ex.Message}");
}

Console.WriteLine("\n=== SAMPLE EBOOK RECORD ===");
try
{
    var sample = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT * FROM ebooks LIMIT 1");
    if (sample != null)
    {
        var dict = (IDictionary<string, object>)sample;
        foreach (var kvp in dict)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  Error getting sample: {ex.Message}");
}

