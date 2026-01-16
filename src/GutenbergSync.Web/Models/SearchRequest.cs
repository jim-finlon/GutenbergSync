namespace GutenbergSync.Web.Models;

public class SearchRequest
{
    public string? Query { get; set; }
    public string? Author { get; set; }
    public string? Language { get; set; }
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

