namespace FileSearchEngine;

public sealed class Article
{
    public required int Id { get; set; }
    
    public required string Name { get; set; }
    
    public required string Text { get; set; }
    
    public string? ElasticId { get; set; }
    
    public float[] Vector { get; set; } = null!;

    public override string ToString()
    {
        return $"Name: {Name}\n" +
               $"Text: {Text}\n";
    }
}