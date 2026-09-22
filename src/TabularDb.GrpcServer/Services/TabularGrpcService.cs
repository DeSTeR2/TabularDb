using Grpc.Core;
using TabularDb.Contracts.Grpc;
using TabularDb.Server.Application;

namespace TabularDb.GrpcServer.Services;

public sealed class TabularGrpcService(DatabaseAppService service) : TabularDbService.TabularDbServiceBase
{
    private static readonly Task<Empty> Done = Task.FromResult(new Empty());

    public override Task<DatabaseList> ListDatabases(Empty request, ServerCallContext context)
    {
        var reply = new DatabaseList();
        reply.Databases.AddRange(service.ListDatabases().Select(d => new DatabaseInfo
        {
            Name = d.Name,
            IsLoaded = d.IsLoaded,
            IsModified = d.IsModified,
        }));
        reply.Types_.AddRange(service.SupportedTypes);
        return Task.FromResult(reply);
    }

    public override Task<Empty> CreateDatabase(DbRef request, ServerCallContext context)
    {
        service.CreateDatabase(request.DbName);
        return Done;
    }

    public override Task<Empty> DeleteDatabase(DbRef request, ServerCallContext context)
    {
        service.DeleteDatabase(request.DbName);
        return Done;
    }

    public override Task<Empty> SaveDatabase(DbRef request, ServerCallContext context)
    {
        service.SaveDatabase(request.DbName);
        return Done;
    }

    public override Task<Empty> LoadDatabase(DbRef request, ServerCallContext context)
    {
        service.LoadDatabase(request.DbName);
        return Done;
    }

    public override Task<FileContent> ExportDatabase(DbRef request, ServerCallContext context)
    {
        var (fileName, json) = service.ExportDatabase(request.DbName);
        return Task.FromResult(new FileContent { FileName = fileName, Content = json });
    }

    public override Task<Empty> ImportDatabase(ImportRequest request, ServerCallContext context)
    {
        service.ImportDatabase(request.DbName, request.Content, request.Overwrite);
        return Done;
    }

    public override Task<TableList> ListTables(DbRef request, ServerCallContext context)
    {
        var data = service.ListTables(request.DbName);
        var reply = new TableList { IsModified = data.IsModified };
        reply.Tables.AddRange(data.Tables);
        return Task.FromResult(reply);
    }

    public override Task<SchemaMsg> GetSchema(TableRef request, ServerCallContext context) =>
        Task.FromResult(ToMessage(service.GetSchema(request.DbName, request.TableName)));

    public override Task<Empty> CreateTable(CreateTableRequest request, ServerCallContext context)
    {
        var fields = request.Fields.Select(f => new FieldInfo(f.Name, f.Type)).ToList();
        service.CreateTable(request.DbName, request.TableName, fields);
        return Done;
    }

    public override Task<Empty> DropTable(TableRef request, ServerCallContext context)
    {
        service.DropTable(request.DbName, request.TableName);
        return Done;
    }

    public override Task<RowPage> GetRows(GetRowsRequest request, ServerCallContext context)
    {
        var page = service.GetRows(request.DbName, request.TableName, request.Skip, request.Take);
        var reply = new RowPage { Total = page.Total };
        reply.Rows.AddRange(page.Rows.Select(ToMessage));
        return Task.FromResult(reply);
    }

    public override Task<RowMsg> AddRow(AddRowRequest request, ServerCallContext context) =>
        Task.FromResult(ToMessage(service.AddRow(request.DbName, request.TableName, FromCells(request.Cells))));

    public override Task<RowMsg> UpdateRow(UpdateRowRequest request, ServerCallContext context) =>
        Task.FromResult(ToMessage(service.UpdateRow(request.DbName, request.TableName, request.Id, FromCells(request.Cells))));

    public override Task<Empty> DeleteRow(RowRef request, ServerCallContext context)
    {
        service.DeleteRow(request.DbName, request.TableName, request.Id);
        return Done;
    }

    public override Task<DedupReply> RemoveDuplicates(DedupRequest request, ServerCallContext context)
    {
        var result = service.RemoveDuplicates(request.DbName, request.TableName, request.DryRun);
        var reply = new DedupReply { RemovedCount = result.RemovedCount };
        reply.RemovedRows.AddRange(result.RemovedRows.Select(ToMessage));
        return Task.FromResult(reply);
    }

    private static List<string?> FromCells(IEnumerable<Cell> cells) =>
        cells.Select(c => c.HasValue ? c.Value : null).ToList();

    private static RowMsg ToMessage(RowData row)
    {
        var message = new RowMsg { Id = row.Id };
        message.Cells.AddRange(row.Values.Select(v => v is null ? new Cell() : new Cell { Value = v }));
        return message;
    }

    private static SchemaMsg ToMessage(TableSchemaInfo schema)
    {
        var message = new SchemaMsg { TableName = schema.Name };
        message.Fields.AddRange(schema.Fields.Select(f => new FieldMsg { Name = f.Name, Type = f.Type }));
        return message;
    }
}
