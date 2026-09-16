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

## Етап 3: веб-версія

| Проєкт | Призначення |
|---|---|
| `TabularDb.RestContracts` | DTO та маршрути REST API |
| `TabularDb.WebApi` | REST API (ASP.NET Core) і хостинг веб-клієнта |
| `TabularDb.Web` | веб-клієнт Blazor WebAssembly |
| `tests/TabularDb.WebApi.Tests` | тести REST API |

```
dotnet run --project src/TabularDb.WebApi
```

Застосунок відкривається за адресою `http://localhost:5200`, бази зберігаються в каталозі `data-web`. Опис API в режимі розробки: `/openapi/v1.json`, інтерактивна документація: `/scalar`.

| Метод | Шлях | Дія |
|---|---|---|
| GET | `/api/databases` | список баз і підтримуваних типів |
| POST | `/api/databases` | створити базу |
| DELETE | `/api/databases/{db}` | видалити базу |
| POST | `/api/databases/{db}/save` | зберегти на диск |
| POST | `/api/databases/{db}/load` | перечитати з диска |
| GET | `/api/databases/{db}/export` | завантажити файл бази |
| POST | `/api/databases/import` | імпортувати файл (multipart) |
| GET, POST | `/api/databases/{db}/tables` | список таблиць, створити таблицю |
| GET, DELETE | `/api/databases/{db}/tables/{table}` | схема таблиці, видалити таблицю |
| GET, POST | `/api/databases/{db}/tables/{table}/rows` | сторінка рядків (`skip`, `take`), додати рядок |
| PUT, DELETE | `/api/databases/{db}/tables/{table}/rows/{id}` | змінити, видалити рядок |
| POST | `/api/databases/{db}/tables/{table}/remove-duplicates?dryRun=` | вилучити повторювані рядки або переглянути їх |

Коди відповідей: 400 — помилки валідації (`errors`), 404 — не знайдено, 409 — вже існує, 422 — пошкоджений файл.
