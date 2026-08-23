The linking happens automatically through .NET `Activity` objects and the W3C `traceparent` HTTP header. Your application code never manually copies a trace ID.

The request chain looks like this:

| Step | Span created by | How it links |
|---|---|---|
| Browser → frontend | ASP.NET Core instrumentation | Starts a new trace or extracts incoming `traceparent` |
| Frontend → storage | HttpClient instrumentation | Creates a child span and injects `traceparent` |
| Request inside storage | ASP.NET Core instrumentation | Extracts `traceparent` and creates a server span |
| Storage → MySQL | EF Core instrumentation | Creates a database span under the current storage span |

Conceptually, the resulting IDs look like:

```text
TraceId: T

frontend HTTP server span        SpanId: A
└── frontend HttpClient span     SpanId: B, Parent: A
    └── storage server span      SpanId: C, Parent: B
        └── MySQL/EF Core span   SpanId: D, Parent: C
```

Every span has the same `TraceId`, which lets Jaeger display them as one trace.

### 1. Frontend receives the request

This registration creates an ASP.NET Core server span for every incoming request:

```csharp
.AddAspNetCoreInstrumentation()
```

It is configured in [frontend/Program.cs](C:/Repos/Experiments/Modern-Distributed-Tracing-in-.NET/chapter2/frontend/Program.cs:48).

When the request enters the frontend, the instrumentation creates a `System.Diagnostics.Activity`. During request processing, it is available as:

```csharp
Activity.Current
```

.NET preserves `Activity.Current` across asynchronous calls.

### 2. Frontend calls storage

The actual outgoing HTTP request occurs here:

```csharp
return _backend.GetStreamAsync("/memes/" + name, cancellationToken);
```

and here:

```csharp
var response = await _backend.PutAsync(
    "/memes/" + name,
    content,
    cancellationToken);
```

See [StorageService.cs](C:/Repos/Experiments/Modern-Distributed-Tracing-in-.NET/chapter2/frontend/StorageService.cs:12).

This registration instruments those calls:

```csharp
.AddHttpClientInstrumentation()
```

See [frontend/Program.cs](C:/Repos/Experiments/Modern-Distributed-Tracing-in-.NET/chapter2/frontend/Program.cs:53).

The instrumentation:

1. Reads the current frontend `Activity`.
2. Creates a child HttpClient activity.
3. Injects a W3C header similar to this:

```http
traceparent: 00-<trace-id>-<frontend-httpclient-span-id>-01
```

No code in `StorageService` needs to manually create or pass that header.

The named `HttpClient` registration at [frontend/Program.cs](C:/Repos/Experiments/Modern-Distributed-Tracing-in-.NET/chapter2/frontend/Program.cs:13) configures where the request goes. It doesn’t perform the propagation itself.

### 3. Storage receives the propagated context

Storage also registers:

```csharp
.AddAspNetCoreInstrumentation()
```

See [storage/Program.cs](C:/Repos/Experiments/Modern-Distributed-Tracing-in-.NET/chapter2/storage/Program.cs:51).

When storage receives the request, this instrumentation:

1. Reads the `traceparent` header.
2. Extracts the existing trace ID and parent span ID.
3. Creates the storage server span using the same trace ID.
4. Sets the frontend HttpClient span as its remote parent.

The controller then executes normally:

```csharp
Meme? meme = await _dbContext.Meme
    .Where(m => m.Name == name)
    .SingleOrDefaultAsync(cancellationToken);
```

See [MemesController.cs](C:/Repos/Experiments/Modern-Distributed-Tracing-in-.NET/chapter2/storage/Controllers/MemesController.cs:19).

### 4. Storage calls MySQL

This registration instruments Entity Framework Core:

```csharp
.AddEntityFrameworkCoreInstrumentation()
```

See [storage/Program.cs](C:/Repos/Experiments/Modern-Distributed-Tracing-in-.NET/chapter2/storage/Program.cs:52).

When EF Core executes `SingleOrDefaultAsync()` or `SaveChangesAsync()`, the instrumentation creates a database client span.

There’s no network trace-context header between storage and MySQL. Instead, the database operation runs while the storage request’s `Activity` is current, so the EF span automatically becomes its child.

### 5. Spans are sent to Jaeger

Both projects register:

```csharp
.AddJaegerExporter()
```

See:

- [frontend/Program.cs](C:/Repos/Experiments/Modern-Distributed-Tracing-in-.NET/chapter2/frontend/Program.cs:52)
- [storage/Program.cs](C:/Repos/Experiments/Modern-Distributed-Tracing-in-.NET/chapter2/storage/Program.cs:49)

The WSLC launcher provides:

```text
OTEL_SERVICE_NAME=frontend
OTEL_SERVICE_NAME=storage
OTEL_EXPORTER_JAEGER_AGENT_HOST=jaeger
```

`OTEL_SERVICE_NAME` tells Jaeger which service produced each span. `OTEL_EXPORTER_JAEGER_AGENT_HOST` tells the exporter where Jaeger is located. Neither setting creates the parent-child relationship—the span and trace IDs already contain it.

### The `traceresponse` middleware

The frontend also adds this response header:

```csharp
ctx.Response.Headers.Add("traceresponse", Activity.Current?.Id);
```

See [frontend/Program.cs](C:/Repos/Experiments/Modern-Distributed-Tracing-in-.NET/chapter2/frontend/Program.cs:32).

That exposes the current activity ID to the browser for inspection. It is not responsible for frontend-to-storage propagation. The important propagation header is the automatically injected `traceparent` header.