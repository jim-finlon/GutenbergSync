using System;
using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;

var dbPath = "/mnt/workspace/gutenberg/gutenberg.db";
var connectionString = $"Data Source={dbPath};";

Console.WriteLine($"Testing connection to: {dbPath}");
Console.WriteLine($"File exists: {System.IO.File.Exists(dbPath)}");
Console.WriteLine($"Connection string: {connectionString}");
Console.WriteLine();

using var connection = new SqliteConnection(connectionString);
await connection.OpenAsync();

var tables = await connection.QueryAsync<string>("SELECT name FROM sqlite_master WHERE type='table' AND name='ebooks'");
Console.WriteLine($"ebooks table exists: {tables.Any()}");

if (tables.Any())
{
    var count = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM ebooks");
    Console.WriteLine($"Total ebooks: {count}");
}
