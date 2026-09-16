using System.Text;
using Microsoft.AspNetCore.Mvc;
using TabularDb.Core.Exceptions;
using TabularDb.RestContracts;
using TabularDb.Server.Application;

namespace TabularDb.WebApi.Controllers;

[ApiController]
[Route("api/databases")]
public sealed class DatabasesController(DatabaseAppService service) : ControllerBase
{
    public const long MaxImportSize = 64 * 1024 * 1024;

    [HttpGet]
    public DatabaseListDto List() =>
        new(service.ListDatabases().Select(d => new DatabaseDto(d.Name, d.IsLoaded, d.IsModified)).ToList(),
            service.SupportedTypes);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public IActionResult Create(CreateDatabaseRequest request)
    {
        service.CreateDatabase(request.Name ?? "");
        var name = request.Name!.Trim();
        return Created("/" + ApiRoutes.Database(name), new DatabaseDto(name, true, true));
    }

    [HttpDelete("{db}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Delete(string db)
    {
        service.DeleteDatabase(db);
        return NoContent();
    }

    [HttpPost("{db}/save")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Save(string db)
    {
        service.SaveDatabase(db);
        return NoContent();
    }

    [HttpPost("{db}/load")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Load(string db)
    {
        service.LoadDatabase(db);
        return NoContent();
    }

    [HttpGet("{db}/export")]
    [Produces("application/json")]
    public IActionResult Export(string db)
    {
        var (fileName, json) = service.ExportDatabase(db);
        return File(Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImportSize + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportSize)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Import(IFormFile? file, [FromForm] string? name, [FromForm] bool overwrite = false)
    {
        if (file is null)
            throw new ValidationException("Не вибрано файл для імпорту");
        var dbName = DbNameValidator.Normalize(name);
        string json;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            json = await reader.ReadToEndAsync();
        service.ImportDatabase(dbName, json, overwrite);
        return NoContent();
    }
}
