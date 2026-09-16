using Microsoft.AspNetCore.Mvc;
using TabularDb.RestContracts;
using TabularDb.Server.Application;

namespace TabularDb.WebApi.Controllers;

[ApiController]
[Route("api/databases/{db}/tables/{table}/rows")]
public sealed class RowsController(DatabaseAppService service) : ControllerBase
{
    [HttpGet]
    public RowPageDto List(string db, string table, [FromQuery] int skip = 0, [FromQuery] int take = ApiRoutes.DefaultPageSize)
    {
        skip = Math.Max(0, skip);
        if (take <= 0 || take > DatabaseAppService.MaxPageSize)
            take = DatabaseAppService.MaxPageSize;
        var page = service.GetRows(db, table, skip, take);
        return new RowPageDto(page.Rows.Select(ToDto).ToList(), page.Total, skip, take);
    }

    [HttpPost]
    [ProducesResponseType<RowDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public IActionResult Add(string db, string table, RowValuesRequest request)
    {
        var row = service.AddRow(db, table, request.Values ?? []);
        return Created("/" + ApiRoutes.Row(db, table, row.Id), ToDto(row));
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public RowDto Update(string db, string table, long id, RowValuesRequest request) =>
        ToDto(service.UpdateRow(db, table, id, request.Values ?? []));

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Delete(string db, string table, long id)
    {
        service.DeleteRow(db, table, id);
        return NoContent();
    }

    internal static RowDto ToDto(RowData row) => new(row.Id, row.Values);
}
