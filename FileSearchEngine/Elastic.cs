using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace FileSearchEngine;

public static class Elastic
{
    public static ElasticsearchClient Client { get; private set; } = null!;
    
    public static void Initialize(string username, string password, string url)
    {
        var settings = new ElasticsearchClientSettings(new Uri(url))
            .Authentication(new BasicAuthentication(username, password));

        Client = new ElasticsearchClient(settings);
    }
}