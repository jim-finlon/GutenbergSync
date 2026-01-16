using System;
using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;

var dbPath = "/mnt/workspace/gutenberg/gutenberg.db";
var connectionString = $"Data Source={dbPath};";

Console.WriteLine($"Testing: {dbPath}");
Console.WriteLine($"Exists: {System.IO.File.Exists(dbPath)}");

using var conn = new SqliteConnection(connectionString);
await conn.OpenAsync();

var tableCheck = await conn.QueryFirstOrDefaultAsync<string>("SELECT name FROM sqlite_master WHERE type='table' AND name='ebooks'");
Console.WriteLine($"ebooks table: {(tableCheck != null ? "EXISTS" : "NOT FOUND")}");

if (tableCheck != null)
{
    var count = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM ebooks");
    Console.WriteLine($"Count: {count}");
}
