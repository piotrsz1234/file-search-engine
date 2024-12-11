using Microsoft.AspNetCore.Mvc;

namespace FileSearchEngine.Controllers;

public sealed class FileController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
    
    public IActionResult DeleteFile(string id)
    {
        return NotFound(new { message = "File not found" });
    }
    
    public async Task<IActionResult> UploadFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        // var filePath = Path.Combine(_fileDirectory, file.FileName);
        // await using (var stream = new FileStream(filePath, FileMode.Create))
        // {
        //     await file.CopyToAsync(stream);
        // }
        return Ok(new { message = "File uploaded successfully" });
    }
}