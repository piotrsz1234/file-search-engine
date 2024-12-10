using Microsoft.AspNetCore.Mvc;

namespace FileSearchEngine.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FileController : ControllerBase
{
    private readonly string _fileDirectory = Path.Combine(Directory.GetCurrentDirectory(), "UploadedFiles");

    public FileController()
    {
        if (!Directory.Exists(_fileDirectory))
        {
            Directory.CreateDirectory(_fileDirectory);
        }
    }

    // GET: api/File
    [HttpGet]
    public IActionResult GetFileList()
    {
        var files = Directory.GetFiles(_fileDirectory).Select(Path.GetFileName).ToList();
        return Ok(files);
    }

    // DELETE: api/File/{fileName}
    [HttpDelete("{fileName}")]
    public IActionResult DeleteFile(string fileName)
    {
        var filePath = Path.Combine(_fileDirectory, fileName);
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
            return Ok(new { message = "File deleted successfully" });
        }
        return NotFound(new { message = "File not found" });
    }

    // POST: api/File/upload
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        var filePath = Path.Combine(_fileDirectory, file.FileName);
        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
        return Ok(new { message = "File uploaded successfully" });
    }
}