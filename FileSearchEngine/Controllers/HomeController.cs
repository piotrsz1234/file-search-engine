using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FileSearchEngine.Models;

namespace FileSearchEngine.Controllers;

public sealed class HomeController(ILogger<HomeController> logger) : Controller
{
    private readonly ILogger<HomeController> _logger = logger;

    public IActionResult Index()
    {
        return View();
    }
    
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    public IActionResult Search(string searchPhrase)
    {
        _logger.LogInformation("Search start");
        
        if (string.IsNullOrEmpty(searchPhrase))
            return Content("No search phrase provided");
        
        var resp = Model.SearchFiles(searchPhrase, 5).ToList();
        if(resp.Count == 0)
            return Content("No results found");
        
        var articles = Database.GetFiles(resp);
        ViewBag.SearchResult = articles;
        _logger.LogInformation("Search end");
        return Content(GetSearchResultString(articles));
    }

    private static string GetSearchResultString(List<Article> articles)
    {
        return string.Join('\n', articles.Select(x => (x.Name, x.Text)));
    }
}