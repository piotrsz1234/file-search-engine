namespace FileSearchEngine;

public static class FileHelper
{
    public static IEnumerable<Article> LoadArticles()
    {
        var rootFolder = $"{Directory.GetCurrentDirectory()}/Files/Default";
        var directories = Directory.GetDirectories(rootFolder);
        foreach (var directory in directories)
        {
            var files = Directory.GetFiles(directory);
            var label = Path.GetFileName(directory);
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                yield return new Article
                {
                    Name = file.Split('\\').Last(),
                    Text = text,
                    Id = 0
                };
            }
        }
    }
}