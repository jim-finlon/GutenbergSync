using System;
using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;

var dbPath = "/mnt/workspace/gutenberg/gutenberg.db";
var connectionString = $"Data Source={dbPath};";

using var connection = new SqliteConnection(connectionString);
await connection.OpenAsync();

Console.WriteLine("=== TABLES ===");
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
    Console.WriteLine($"  {col.name} ({col.type}) {(col.notnull == 1 ? "NOT NULL" : "NULL")} {(col.pk == 1 ? "PRIMARY KEY" : "")}");
}

Console.WriteLine("\n=== EBOOKS COUNT ===");
try
{
    var count = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM ebooks");
    Console.WriteLine($"  Total ebooks: {count}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Error: {ex.Message}");
}

Console.WriteLine("\n=== AUTHORS TABLE SCHEMA ===");
var authorColumns = await connection.QueryAsync<(string name, string type, int notnull, object? dflt_value, int pk)>(
    "PRAGMA table_info(authors)");
foreach (var col in authorColumns)
{
    Console.WriteLine($"  {col.name} ({col.type}) {(col.notnull == 1 ? "NOT NULL" : "NULL")} {(col.pk == 1 ? "PRIMARY KEY" : "")}");
}

Console.WriteLine("\n=== STATISTICS ===");
try
{
    var totalBooks = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM ebooks");
    var totalAuthors = await connection.QuerySingleAsync<int>("SELECT COUNT(DISTINCT id) FROM authors");
    var uniqueLanguages = await connection.QuerySingleAsync<int>("SELECT COUNT(DISTINCT language_iso_code) FROM ebooks WHERE language_iso_code IS NOT NULL");
    var uniqueSubjects = await connection.QuerySingleAsync<int>("SELECT COUNT(DISTINCT subject) FROM ebook_subjects");
    
    Console.WriteLine($"  Total Books: {totalBooks}");
    Console.WriteLine($"  Total Authors: {totalAuthors}");
    Console.WriteLine($"  Unique Languages: {uniqueLanguages}");
    Console.WriteLine($"  Unique Subjects: {uniqueSubjects}");
}
catch (Exception ex)
{
    Console.WriteLine($"  Error: {ex.Message}");
}

