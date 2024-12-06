﻿using Catalyst;
using Catalyst.Models;
using Mosaik.Core;

namespace FileSearchEngine;

public static class Model
{
    private static Pipeline _nlp = null!;

    private static TFIDF _tfidf = null!;
    
    private static FastText _fastText = null!;
    
    public static async Task Initialize()
    {
        //Register language
        English.Register();

        //Load model
        Storage.Current = new DiskStorage("catalyst-models");
        
        //Load files
        var files = FileHelper.LoadArticles().ToList();
        
        await InitializeNlp();
        await InitializeTfidf(files);
        await InitializeFastText(files);
        foreach (var file in files)
        {
            Database.AddFileIfNotExists(file.Name, file.Text, file.Label);
        }
    }

    public static IEnumerable<string> SearchFiles(string query, int? resultCount = null)
    {
        var doc = new Document(query, Language.English);
        _nlp.ProcessSingle(doc);
        var tokens = SanitizeDoc(doc);
        var doc2 = new Document(string.Join(' ', tokens), Language.English);
        _nlp.ProcessSingle(doc2);
        _tfidf.Process(doc2);
    
        var v1 = _fastText.GetVector(query, Language.English);
        List<(Article, float)> results = [];
        foreach (var document in FileHelper.LoadArticles().ToList())
        {
            var docToCompare = new Document(document.Text, Language.English);
            _nlp.ProcessSingle(docToCompare);
            var docTokens = SanitizeDoc(docToCompare);
            var doc2ToCompare = new Document(string.Join(' ', docTokens), Language.English);
            _nlp.ProcessSingle(doc2ToCompare);
            _tfidf.Process(doc2ToCompare);

            _tfidf.Process(doc2ToCompare);
            if(!_fastText.TryGetDocumentVector(doc2ToCompare, out var v2))
                continue;

            var compare = v1.CosineSimilarityWith(v2);
            results.Add((document, compare));
        }

        if (results.Count == 0)
            return [];

        var resultsOrdered = results.OrderByDescending(x => x.Item2).ToList();
        if(resultCount is > 0 && resultCount < resultsOrdered.Count)
            resultsOrdered = resultsOrdered.Take(resultCount.Value).ToList();
        
        return resultsOrdered.Select(x => x.Item1.Name);
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
    }

    private static async Task InitializeFastText(List<Article> files)
    {
        try
        {
            _fastText = await FastText.FromStoreAsync(Language.English, 1, "fasttext");
        }
        catch (Exception)
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
            _fastText.Train(_nlp.Process(files.Select(x => new Document(x.Text, Language.English)
            {
                Labels = [x.Label]
            }).ToList()).ToArray());
        }
    }

    private static IEnumerable<string> SanitizeDoc(Document doc)
    {
        return doc.SelectMany(x => x.Tokens)
            .Select(x => x.Lemma)
            .Where(x => !Helper.StopWords.Contains(x));
    }
}