namespace GutenbergSync.Web.Services;

public interface IEpubCopyService
{
    Task<string?> FindEpubPathAsync(int bookId);
    Task<bool> CopyEpubAsync(int bookId, string destinationPath);
}

