namespace FileSearchEngine;

public sealed class Article
{
    public required int Id { get; set; }
    
    public required string Name { get; set; }
    
    public required string Text { get; set; }
}