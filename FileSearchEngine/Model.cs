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
            await ElasticDatabase.ResetDatabase();
            await ElasticDatabase.CleanDatabase();
            files = FileHelper.LoadArticles().ToList();
            foreach (var file in files)
            {
                var id = Database.AddFileIfNotExists(file.Name, file.Text);
                file.Id = id;
                var elasticId = await ElasticDatabase.AddFile(file, id);
                if (elasticId is null) continue;
                file.ElasticId = elasticId;
                Database.UpdateElasticId(file.Id, elasticId);
            }
            
            await InitializeNlp();
            await InitializeTfidf(files, false);
            await InitializeFastText(files, false);
            await GenerateVectorCache(true);
            return;
        }
        
        await InitializeNlp();
        await InitializeTfidf(files, true);
        await InitializeFastText(files, true);
        await GenerateVectorCache(false);
    }

    public static float[] GetVector(string query)
    {
        var doc = new Document(query, Language.English);
        _nlp.ProcessSingle(doc);
        var tokens = SanitizeDoc(doc);
        var doc2 = new Document(string.Join(' ', tokens), Language.English);
        _nlp.ProcessSingle(doc2);
        _tfidf.Process(doc2);
    
        return _fastText.GetVector(doc2.Value, Language.English);
    }
    
    public static IEnumerable<int> SearchFiles(string query, int? resultCount = null)
    {
        var doc = PreprocessDocument(query);
        var v1 = _fastText.GetVector(doc.Value, Language.English);
        List<(Article, float)> results = [];
        foreach (var document in Database.GetFiles())
        {
            if (!FastTextVectors.TryGetValue(document.Id, out var v2))
                continue;
            
            var compare = v1.CosineSimilarityWith(v2);
            results.Add((document, compare));
        }

        return results.Count == 0 ? [] : OrderResults(results, resultCount).Select(x => x.Item1.Id);
    }
    
    public static IEnumerable<int> SearchFilesTfIdf(string query, int? resultCount = null)
    {
        var doc = PreprocessDocument(query);
        var v1 = GetTfIdfVector(doc);
        List<(Article, float)> results = [];
        foreach (var document in Database.GetFiles())
        {
            var doc2 = PreprocessDocument(document.Text);
            var v2 = GetTfIdfVector(doc2);
            var compare = v1.CosineSimilarityWith(v2);
            results.Add((document, compare));
        }
        
        return results.Count == 0 ? [] : OrderResults(results, resultCount).Select(x => x.Item1.Id);
    }

    private static List<(Article, float)> OrderResults(List<(Article, float)> list, int? count)
    {
        var resultsOrdered = list.OrderByDescending(x => x.Item2).ToList();
        if(count is > 0 && count < resultsOrdered.Count)
            resultsOrdered = resultsOrdered.Take(count.Value).ToList();
        return resultsOrdered;
    }
    
    private static Document PreprocessDocument(string query, Language language = Language.English)
    {
        var doc = new Document(query, language);
        _nlp.ProcessSingle(doc);
        var tokens = SanitizeDoc(doc);
        var doc2 = new Document(string.Join(' ', tokens), Language.English);
        _nlp.ProcessSingle(doc2);
        _tfidf.Process(doc2);
        return doc2;
    }

    public static async Task AddFile(string fileName, string text)
    {
        var id = Database.AddFileIfNotExists(fileName, text);
        var elasticId = await ElasticDatabase.AddFile(new Article
        {
            Name = fileName,
            Text = text,
            Id = id
        }, id);
        
        if (elasticId is not null)
        {
            Database.UpdateElasticId(id, elasticId);
        }

        var article = new Article
        {
            Id = id,
            Name = fileName,
            Text = text,
            ElasticId = elasticId
        };
        await TrainTfIdf([article]);
        await TrainFastText([article]);
        
        GetVectorForSingleDocument(article);
        await ElasticDatabase.UpdateFile(article);
    }
    
    public static async Task<bool> RemoveFile(int id)
    {
        var elasticId = Database.GetElasticId(id);
        if(!Database.DeleteFileById(id))
            return false;

        if(!string.IsNullOrEmpty(elasticId))
            await ElasticDatabase.DeleteFile(elasticId);
        FastTextVectors.Remove(id);
        return true;
    }
    
    private static async Task GenerateVectorCache(bool updateElastic)
    {
        FastTextVectors.Clear();
        var files = Database.GetFiles();
        if (files.Count == 0)
            return;

        foreach (var document in files)
        {
            GetVectorForSingleDocument(document);
            if(updateElastic)
                await ElasticDatabase.UpdateFile(document);
        }
    }

    private static void GetVectorForSingleDocument(Article document)
    {
        var docToCompare = new Document(document.Text, Language.English);
        _nlp.ProcessSingle(docToCompare);
        var docTokens = SanitizeDoc(docToCompare);
        var doc2ToCompare = new Document(string.Join(' ', docTokens), Language.English);
        _nlp.ProcessSingle(doc2ToCompare);
        _tfidf.Process(doc2ToCompare);
        var vector = _fastText.GetVector(doc2ToCompare.Value, Language.English);
        FastTextVectors[document.Id] = vector;
        document.Vector = vector;
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
    
    private static async Task InitializeTfidf(List<Article> files, bool fromStorage)
    {
        try
        {
            if (fromStorage)
                _tfidf = await TFIDF.FromStoreAsync(Language.English, 1, "tfidf");
            else
            {
                _tfidf = new TFIDF(Language.English, 1, "tfidf");
                await TrainTfIdf(files);
            }
        }
        catch (Exception)
        {
            _tfidf = new TFIDF(Language.English, 1, "tfidf");
            await TrainTfIdf(files);
        }
    }
    
    private static async Task TrainTfIdf(List<Article> files)
    {
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

    private static async Task InitializeFastText(List<Article> files, bool fromStorage)
    {
        try
        {
            if (fromStorage)
                _fastText = await FastText.FromStoreAsync(Language.English, 1, "fasttext");
            else
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
                await TrainFastText(files);
            }
        }
        catch (Exception)
        {
            await TrainFastText(files);
        }
    }
    
    private static async Task TrainFastText(List<Article> files)
    {
        _fastText.Train(_nlp.Process(files
                .Select(x => new Document(x.Text, Language.English))
                .ToList())
            .ToArray());
        await _fastText.StoreAsync();
    }
    
    private static IEnumerable<string> SanitizeDoc(Document doc)
    {
        return doc.SelectMany(x => x.Tokens)
            .Where(x => x.POS != PartOfSpeech.PUNCT && x.POS != PartOfSpeech.SYM)
            .Select(x => x.Lemma)
            .Where(x => !Helper.StopWords.Contains(x));
    }

    private static float[] GetTfIdfVector(Document doc)
    {
        return NormalizeVector(doc.SelectMany(x => x.Tokens).Select(x => x.Frequency).ToArray(), 200);
    }
    
    private static float[] NormalizeVector(float[] vector, int target)
    {
        if (vector.Length == target)
            return vector;
        
        return vector.Length > target ? AggregateVector(vector, target) : ExpandVector(vector, target);
    }
    
    private static float[] AggregateVector(float[] vector, int targetLength)
    {
        // Determine how many elements to group together
        var groupSize = vector.Length / targetLength;
        var aggregatedVector = new float[targetLength];

        for (var i = 0; i < targetLength; i++)
        {
            float sum = 0;
            var startIndex = i * groupSize;
            var endIndex = (i + 1) * groupSize;

            // Aggregate (average) the values in the group
            for (var j = startIndex; j < endIndex && j < vector.Length; j++)
            {
                sum += vector[j];
            }
            aggregatedVector[i] = sum / groupSize;
        }

        return aggregatedVector;
    }

    private static float[] ExpandVector(float[] input, int target)
    {
        var paddedVector = new float[target];
        Array.Copy(input, paddedVector, input.Length);
        return paddedVector;
    }
}