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
                                          elastic_id TEXT,
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
        cmd.CommandText = "SELECT id, name, content, elastic_id FROM files;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            files.Add(new Article
            {
                Id = int.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Text = reader.GetString(2),
                ElasticId = reader.GetString(3)
            });
        }
        return files;
    }
    
    public static bool DeleteFileById(int id)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM files WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        var rowsAffected = cmd.ExecuteNonQuery();
        return rowsAffected > 0;
    }
    
    public static void UpdateElasticId(int id, string elasticId)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "UPDATE files SET elastic_id = @elasticId WHERE id = @id;";
        cmd.Parameters.AddWithValue("@elasticId", elasticId);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
    
    public static string GetElasticId(int id)
    {
        try
        {
            using var cmd = Connection.CreateCommand();
            cmd.CommandText = "SELECT elastic_id FROM files WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var test = reader.GetString(0);
                return test;
            }
            
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
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
        cmd.CommandText = "SELECT id, name, content, elastic_id FROM files WHERE id IN (" + string.Join(", ", parameters) + ");";
    
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            files.Add(new Article
            {
                Id = int.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Text = reader.GetString(2),
                ElasticId = reader.GetString(3)
            });
        }
    
        return files;
    }
}