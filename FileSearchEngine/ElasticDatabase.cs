using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;

namespace FileSearchEngine;

public static class ElasticDatabase
{
    private static ElasticsearchClient Client { get; set; } = null!;
    
    private const string ArticleIndex = "article-index";

    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    public static bool Initialized => Client != null;
    
    public static void Initialize(string username, string password, string url, string fingerprint)
    {
        var settings = new ElasticsearchClientSettings(new Uri(url))
            .CertificateFingerprint(fingerprint)
            .Authentication(new BasicAuthentication(username, password));

        Client = new ElasticsearchClient(settings);
    }
    
    public static async Task ResetDatabase()
    {
        if(!Initialized)
            return;
        
        var exists = await Client.Indices.ExistsAsync(ArticleIndex);
        if(!exists.Exists)
            await Client.Indices.DeleteAsync<Article>(ArticleIndex);
        
        await Client.Indices.CreateAsync<Article>(index => index
            .Index(ArticleIndex)
            .Mappings(mappings => mappings
                .Properties(properties => properties
                    .IntegerNumber(x => x.Id)
                    .Text(x => x.Name)
                    .Text(x => x.Text)
                    .DenseVector(x => x.Vector)
                )
            )
            .Settings(x => x.Similarity(y => y.Bm25("default")))
        );
    }

    public static async Task CleanDatabase()
    {
        if(!Initialized)
            return;
        
        await Client.DeleteByQueryAsync<object>(ArticleIndex, x => x
            .Query(q => q.MatchAll(new MatchAllQuery()))
        );
    }
    
    public static async Task<string?> AddFile(Article article, int databaseId)
    {
        if(!Initialized)
            return null;
        
        var indexExistsResponse = await Client.Indices.ExistsAsync(databaseId.ToString());
        if(indexExistsResponse.Exists)
            return null;
        
        var response = await Client.IndexAsync(article, i => i.Index(ArticleIndex));
        return response.Id;
    }
    
    public static async Task DeleteFile(string databaseId)
    {
        if(!Initialized)
            return;
        
        if(!int.TryParse(databaseId, out var id))
            return;
        await Client.DeleteAsync(new DeleteRequest(ArticleIndex, id));
    }
    
    public static async Task UpdateFile(Article article)
    {
        if(!Initialized)
            return;
        
        if(!int.TryParse(article.ElasticId, out var id))
            return;

        await Client.UpdateAsync<Article, Article>(ArticleIndex, id, u => u.Doc(article));
    }
    
    public static async Task<IEnumerable<Article>> SearchFiles(string query, int? resultCount = null)
    { 
        if(!Initialized)
            return [];
        
        var response = await Client.SearchAsync<Article>(s => s 
            .Index(ArticleIndex) 
            .From(0)
            .Size(resultCount ?? 5)
            .Query(q => q
                .MatchPhrase(mp => mp
                        .Field(f => f.Text)
                        .Query(query)
                )
            )
        );

        return response.Documents;
    }

    public static async Task<IEnumerable<Article>> SearchFilesKnn(string query, int? resultCount = null)
    {
        if(!Initialized)
            return [];
        
        var vector = Model.GetVector(query);
        var response = await Client.SearchAsync<Article>(s => s
            .Index(ArticleIndex)
            .From(0)
            .Size(resultCount ?? 5)
            .Query(q => q
                .Knn(x => x
                        .QueryVector(vector)
                        .Field(y => y.Vector)
                        .k(5)
                )
            ));

        return response.Documents;
    }
}