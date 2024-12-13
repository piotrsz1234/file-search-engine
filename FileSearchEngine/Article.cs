namespace FileSearchEngine;

public sealed class Article
{
    public required int Id { get; set; }
    
    public required string Name { get; set; }
    
    public required string Text { get; set; }

    public override string ToString()
    {
        return $"Name: {Name}\n" +
               $"Text: {Text}\n";
    }
}