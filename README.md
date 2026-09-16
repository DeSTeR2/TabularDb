# TabularDb

Система управління табличними базами даних (система типу 1).

Власенко Захар Сергійович, ТТП-41. Варіант: типи `time`, `timeInvl`; операція — вилучення повторюваних рядків.

## Етап 2: десктоп-клієнт і віддалений сервер

| Проєкт | Призначення |
|---|---|
| `TabularDb.Core` | модель, типи, валідація, вилучення дублікатів |
| `TabularDb.Storage` | збереження бази у файл `*.tdb.json` |
| `TabularDb.Contracts` | gRPC-контракт `tabulardb.proto` |
| `TabularDb.Server.Application` | сховище баз на сервері та прикладні операції |
| `TabularDb.GrpcServer` | gRPC-сервер (ASP.NET Core) |
| `TabularDb.Desktop` | клієнт WinForms |
| `tests/*` | unit- та інтеграційні тести (xUnit) |

Потрібен .NET SDK 10.

```
dotnet test
dotnet run --project src/TabularDb.GrpcServer
dotnet run --project src/TabularDb.Desktop
```

Сервер слухає два порти: `http://localhost:5100` (gRPC, HTTP/2) і `http://localhost:5101` (gRPC-Web, HTTP/1.1). Бази зберігаються в каталозі `data-grpc`. Клієнт читає адресу з `appsettings.json`; адресу, API-ключ і режим gRPC-Web можна змінити у вікні підключення.

### Налаштування сервера

| Ключ | Змінна середовища | Значення |
|---|---|---|
| `Storage:DataDir` | `Storage__DataDir` | каталог з файлами баз |
| `Security:ApiKey` | `Security__ApiKey` | якщо задано, клієнт має передавати заголовок `x-api-key` |

### Розгортання в Azure Container Apps

```
az containerapp up --name tabulardb-grpc --resource-group tabulardb --location westeurope \
  --source . --ingress external --target-port 8080 \
  --env-vars Security__ApiKey=<ключ>
az containerapp ingress update --name tabulardb-grpc --resource-group tabulardb --transport http2
az containerapp update --name tabulardb-grpc --resource-group tabulardb --min-replicas 0 --max-replicas 1
```

Щоб бази не зникали після перезапуску, до контейнера треба підключити том Azure Files у `/data`. У контейнері gRPC слухає порт 8080, gRPC-Web — 8081. Якщо HTTP/2 до сервера не проходить, розгорніть застосунок з `--target-port 8081` і транспортом `http`, а в клієнті увімкніть режим gRPC-Web.
