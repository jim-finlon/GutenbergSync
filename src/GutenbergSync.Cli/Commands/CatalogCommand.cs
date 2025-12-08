using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using GutenbergSync.Core.Catalog;
using Serilog;

namespace GutenbergSync.Cli.Commands;

/// <summary>
/// Command for querying the catalog
/// </summary>
public sealed class CatalogCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("catalog", "Query and manage the ebook catalog");

        var searchCommand = new Command("search", "Search the catalog");
        var queryOption = new Option<string>("--query", "Search query (searches title and author)");
        var authorOption = new Option<string>("--author", "Filter by author");
        var subjectOption = new Option<string>("--subject", "Filter by subject");
        var languageOption = new Option<string>("--language", "Filter by language");
        var limitOption = new Option<int?>("--limit", "Maximum number of results");

        searchCommand.AddOption(queryOption);
        searchCommand.AddOption(authorOption);
        searchCommand.AddOption(subjectOption);
        searchCommand.AddOption(languageOption);
        searchCommand.AddOption(limitOption);

        searchCommand.SetHandler(async (query, author, subject, language, limit) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger>();
            var catalogRepo = serviceProvider.GetRequiredService<ICatalogRepository>();

            try
            {
                var options = new CatalogSearchOptions
                {
                    Query = query,
                    Author = author,
                    Subject = subject,
                    Language = language,
                    Limit = limit
                };

                var results = await catalogRepo.SearchAsync(options);
                logger.Information("Found {Count} results", results.Count);

                // TODO: Display results in table format
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Search failed");
                Environment.ExitCode = 1;
            }
        }, queryOption, authorOption, subjectOption, languageOption, limitOption);

        var statsCommand = new Command("stats", "Show catalog statistics");
        statsCommand.SetHandler(async () =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger>();
            var catalogRepo = serviceProvider.GetRequiredService<ICatalogRepository>();

            try
            {
                var stats = await catalogRepo.GetStatisticsAsync();
                logger.Information("Total books: {TotalBooks}", stats.TotalBooks);
                logger.Information("Total authors: {TotalAuthors}", stats.TotalAuthors);
                logger.Information("Unique languages: {Languages}", stats.UniqueLanguages);
                // TODO: Display full statistics
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to get statistics");
                Environment.ExitCode = 1;
            }
        });

        command.AddCommand(searchCommand);
        command.AddCommand(statsCommand);

        return command;
    }
}

