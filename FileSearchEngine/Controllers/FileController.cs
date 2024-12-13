using Microsoft.AspNetCore.Mvc;

namespace FileSearchEngine.Controllers;

public sealed class FileController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
    
    public async Task<IActionResult> DeleteFile(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return BadRequest(new { message = "No file id provided" });
        }

        if (!int.TryParse(id, out var parsed) || parsed < 0)
            return BadRequest("Invalid id provided");

        var response = await Model.RemoveFile(parsed);
        if (!response)
            return NotFound(new { message = "File not found" });
        return Ok(new { message = "File deleted successfully" });
    }
    
    public async Task<IActionResult> UploadFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });
        
        if(file.ContentType != "text/plain")
            return BadRequest(new { message = "Only text files are accepted" });

        var content = await new StreamReader(file.OpenReadStream()).ReadToEndAsync();
        await Model.AddFile(file.FileName, content);
        return Ok(new { message = "File uploaded successfully" });
    }

    public IActionResult GetFileList()
    {
        var files = Database.GetFiles();
        var options = files.Select(f => 
            $"<option value='{f.Id}'>{f.Id} {f.Name}</option>").ToList();

        return Content(string.Join(Environment.NewLine, options));
    }
}