using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using FileSearchEngine.Models;

namespace FileSearchEngine.Controllers;

public sealed class HomeController(ILogger<HomeController> logger) : Controller
{
    public IActionResult Index()
    {
        return View();
    }
    
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    public IActionResult Search(string searchPhrase, int count = 5)
    {
        logger.LogInformation("Search start");
        
        if (string.IsNullOrEmpty(searchPhrase))
            return Content("No search phrase provided");

        if (count is < 1 or > 100)
            count = 5;
        
        var resp = Model.SearchFiles(searchPhrase, count).ToList();
        if(resp.Count == 0)
            return Content("No results found");
        
        var articles = Database.GetFiles(resp).OrderBy(a => resp.IndexOf(a.Id)).ToList();
        ViewBag.SearchResult = articles;
        logger.LogInformation("Search end");
        return Content(GetSearchResultString(articles));
    }

    private static string GetSearchResultString(IEnumerable<Article> articles)
    {
        return string.Join('\n', articles.Select(x => x.ToString()));
    }
}