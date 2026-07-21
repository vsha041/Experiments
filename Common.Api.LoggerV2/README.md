# RequestCorrelation.Logging

Minimal per-request correlation IDs for ASP.NET Core applications using
`Microsoft.Extensions.Logging`.

## Install

```bash
dotnet add package RequestCorrelation.Logging
```

## Register

Add one line to `Program.cs`:

```csharp
using RequestCorrelation.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRequestCorrelationLogging();

var app = builder.Build();

app.MapGet("/", (ILogger<Program> logger) =>
{
    logger.LogInformation("Handling the request");
    logger.LogInformation("Still handling the same request");
    return Results.Ok();
});

app.Run();
```

Both log events have the same structured `CorrelationId`. A different request
gets a new ID. Every response also contains:

- `X-Correlation-ID`: the correlation ID for the request.
- `X-Response-Timestamp`: the current UTC timestamp in ISO 8601 format.

If the request already contains a non-empty `X-Correlation-ID` header, that ID
is reused for its logs and response. Otherwise, a new ID is generated.

No change is needed to individual `ILogger` calls.

## Show scopes in the built-in console logger

Logging scopes are structured metadata. To display them in console output, use a
console formatter with scopes enabled:

```csharp
builder.Logging.AddSimpleConsole(options => options.IncludeScopes = true);
```

Structured logging providers that support `Microsoft.Extensions.Logging` scopes
can record `CorrelationId` as a searchable field.

## Read the ID in application code

```csharp
var correlationId = httpContext.GetCorrelationId();
```
