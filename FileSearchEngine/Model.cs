using Catalyst;
using Catalyst.Models;
using Mosaik.Core;

namespace FileSearchEngine;

public static class Model
{
    private static Pipeline _nlp = null!;

    private static TFIDF _tfidf = null!;
    
    private static FastText _fastText = null!;
    
    private static readonly Dictionary<int, float[]> FastTextVectors = new();
    
    public static async Task Initialize()
    {
        //Register language
        English.Register();

        //Load model
        Storage.Current = new DiskStorage("catalyst-models");
        
        //Load files
        var files = Database.GetFiles();
        if(files.Count == 0)
        {
            files = FileHelper.LoadArticles().ToList();
            foreach (var file in files)
            {
                var id = Database.AddFileIfNotExists(file.Name, file.Text);
                file.Id = id;
            }
            
            await InitializeNlp();
            await TrainTfIdf(files);
            await TrainFastText(files);
            await GenerateVectorCache();
            return;
        }
        
        await InitializeNlp();
        await InitializeTfidf(files);
        await InitializeFastText(files);
        await GenerateVectorCache();
    }

    public static IEnumerable<int> SearchFiles(string query, int? resultCount = null)
    {
        var doc = new Document(query, Language.English);
        _nlp.ProcessSingle(doc);
        var tokens = SanitizeDoc(doc);
        var doc2 = new Document(string.Join(' ', tokens), Language.English);
        _nlp.ProcessSingle(doc2);
        _tfidf.Process(doc2);
    
        var v1 = _fastText.GetVector(doc2.Value, Language.English);
        List<(Article, float)> results = [];
        foreach (var document in Database.GetFiles())
        {
            if (!FastTextVectors.TryGetValue(document.Id, out var v2))
                continue;
            
            var compare = v1.CosineSimilarityWith(v2);
            results.Add((document, compare));
        }

        if (results.Count == 0)
            return [];

        var resultsOrdered = results.OrderByDescending(x => x.Item2).ToList();
        if(resultCount is > 0 && resultCount < resultsOrdered.Count)
            resultsOrdered = resultsOrdered.Take(resultCount.Value).ToList();
        
        return resultsOrdered.Select(x => x.Item1.Id);
    }

    public static async Task AddFile(string fileName, string text)
    {
        Database.AddFileIfNotExists(fileName, text);
        var files = Database.GetFiles();
        await TrainTfIdf(files);
        await TrainFastText(files);
        await GenerateVectorCache();
    }
    
    public static async Task RemoveFile(string fileName)
    {
        Database.DeleteFile(fileName);
        var files = Database.GetFiles();
        await TrainTfIdf(files);
        await TrainFastText(files);
        await GenerateVectorCache();
    }
    
    private static Task GenerateVectorCache()
    {
        FastTextVectors.Clear();
        var files = Database.GetFiles();
        if (files.Count == 0)
            return Task.CompletedTask;

        foreach (var document in files)
        {
            var docToCompare = new Document(document.Text, Language.English);
            _nlp.ProcessSingle(docToCompare);
            var docTokens = SanitizeDoc(docToCompare);
            var doc2ToCompare = new Document(string.Join(' ', docTokens), Language.English);
            _nlp.ProcessSingle(doc2ToCompare);
            _tfidf.Process(doc2ToCompare);
            var vector = _fastText.GetVector(doc2ToCompare.Value, Language.English);
            FastTextVectors[document.Id] = vector;
        }

        return Task.CompletedTask;
    }
    
    private static async Task InitializeNlp()
    {
        try
        {
            _nlp = await Pipeline.FromStoreAsync(Language.English, 1, "nlp");
        }
        catch(Exception)
        {
            _nlp = await Pipeline.ForAsync(Language.English);
            await _nlp.StoreAsync();
        }
    }
    
    private static async Task InitializeTfidf(List<Article> files)
    {
        try
        {
            _tfidf = await TFIDF.FromStoreAsync(Language.English, 1, "tfidf");
        }
        catch (Exception)
        {
            await TrainTfIdf(files);
        }
    }
    
    private static async Task TrainTfIdf(List<Article> files)
    {
        _tfidf = new TFIDF(Language.English, 1, "tfidf");
        List<Document> docs = [];
        foreach (var file in files)
        {
            var doc = new Document(file.Text, Language.English);
            _nlp.ProcessSingle(doc);
            file.Text = string.Join(' ', doc
                .SelectMany(x => x.Tokens)
                .Select(x => x.Lemma)
                .Where(x => !Helper.StopWords.Contains(x)));
            var doc2 = new Document(file.Text, Language.English);
            _nlp.ProcessSingle(doc2);
            docs.Add(doc2);
        }
        await _tfidf.Train(docs);
        await _tfidf.StoreAsync();
    }

    private static async Task InitializeFastText(List<Article> files)
    {
        try
        {
            _fastText = await FastText.FromStoreAsync(Language.English, 1, "fasttext");
        }
        catch (Exception)
        {
            await TrainFastText(files);
        }
    }
    
    private static async Task TrainFastText(List<Article> files)
    {
        _fastText = new FastText(Language.English, 1, "fasttext")
        {
            Data =
            {
                Type = FastText.ModelType.PVDM,
                Loss = FastText.LossType.NegativeSampling,
                ContextWindow = 1
            }
        };
        _fastText.Train(_nlp.Process(files
                .Select(x => new Document(x.Text, Language.English))
                .ToList())
            .ToArray());
        await _fastText.StoreAsync();
    }

    private static IEnumerable<string> SanitizeDoc(Document doc)
    {
        return doc.SelectMany(x => x.Tokens)
            .Select(x => x.Lemma)
            .Where(x => !Helper.StopWords.Contains(x));
    }
}