using Microsoft.AspNetCore.Mvc;
using TabularDb.RestContracts;
using TabularDb.Server.Application;

namespace TabularDb.WebApi.Controllers;

[ApiController]
[Route("api/databases/{db}/tables")]
public sealed class TablesController(DatabaseAppService service) : ControllerBase
{
    [HttpGet]
    public TableListDto List(string db)
    {
        var data = service.ListTables(db);
        return new TableListDto(data.Tables, data.IsModified);
    }

    [HttpGet("{table}")]
    public TableSchemaDto Get(string db, string table) => ToDto(service.GetSchema(db, table));

    [HttpPost]
    [ProducesResponseType<TableSchemaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public IActionResult Create(string db, CreateTableRequest request)
    {
        var fields = (request.Fields ?? []).Select(f => new FieldInfo(f.Name ?? "", f.Type ?? "")).ToList();
        var schema = service.CreateTable(db, request.Name ?? "", fields);
        return Created("/" + ApiRoutes.Table(db, schema.Name), ToDto(schema));
    }

    [HttpDelete("{table}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Drop(string db, string table)
    {
        service.DropTable(db, table);
        return NoContent();
    }

    [HttpPost("{table}/remove-duplicates")]
    public DedupResultDto RemoveDuplicates(string db, string table, [FromQuery] bool dryRun = false)
    {
        var result = service.RemoveDuplicates(db, table, dryRun);
        return new DedupResultDto(result.RemovedCount, result.RemovedRows.Select(RowsController.ToDto).ToList(), dryRun);
    }

    private static TableSchemaDto ToDto(TableSchemaInfo schema) =>
        new(schema.Name, schema.Fields.Select(f => new FieldDto(f.Name, f.Type)).ToList());
}
