using Microsoft.Data.Sqlite;

namespace FileSearchEngine;

public static class Database
{
    private static readonly SqliteConnection Connection = new("Data Source=database.db");

    static Database()
    {
        Connection.Open();
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
                          
                                      CREATE TABLE IF NOT EXISTS files (
                                          id INTEGER PRIMARY KEY,
                                          name TEXT NOT NULL,
                                          content TEXT NOT NULL,
                                          UNIQUE(name)
                                      );
                                  
                          """;
        cmd.ExecuteNonQuery();
    }
    
    public static int AddFileIfNotExists(string name, string content)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
                          
                                  INSERT OR IGNORE INTO files (name, content)
                                  VALUES (@name, @content);
                                  
                                  SELECT id FROM files WHERE name = @name AND content = @content;
                          """;
    
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@content", content);

        var result = cmd.ExecuteScalar();
        return Convert.ToInt32(result);
    }
    
    public static List<Article> GetFiles()
    {
        List<Article> files = [];
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, content FROM files;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            files.Add(new Article
            {
                Id = int.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Text = reader.GetString(2)
            });
        }
        return files;
    }
    
    public static void DeleteFile(string name)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM files WHERE name = @name;";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.ExecuteNonQuery();
    }
    
    public static bool DeleteFileById(int id)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM files WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        var rowsAffected = cmd.ExecuteNonQuery();
        return rowsAffected > 0;
    }
    
    public static List<Article> GetFiles(List<int> names)
    {
        List<Article> files = []; // Correct initialization
        using var cmd = Connection.CreateCommand();
    
        // Create a list of parameters and build the query with placeholders
        var parameters = new List<string>();
        for (var i = 0; i < names.Count; i++)
        {
            parameters.Add($"@name{i}"); // Create a unique parameter for each name
            cmd.Parameters.AddWithValue($"@name{i}", names[i]);
        }
    
        // Build the query with the dynamically created parameters
        cmd.CommandText = "SELECT id, name, content FROM files WHERE id IN (" + string.Join(", ", parameters) + ");";
    
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            files.Add(new Article
            {
                Id = int.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Text = reader.GetString(2)
            });
        }
    
        return files;
    }
    
    public static string GetFileContent(string name)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "SELECT content FROM files WHERE name = @name;";
        cmd.Parameters.AddWithValue("@name", name);
        return (string)cmd.ExecuteScalar()!;
    }
    
    public static IEnumerable<string> SearchFiles(string query)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM files WHERE content LIKE @query;";
        cmd.Parameters.AddWithValue("@query", $"%{query}%");
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return reader.GetString(0);
        }
    }
}