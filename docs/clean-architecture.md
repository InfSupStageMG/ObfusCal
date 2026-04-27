# Clean Architecture - Project Structure & Rules

This document defines the architectural conventions for this codebase. It is intended for both human developers and AI assistants working on the project. All contributions must adhere to these rules.

> This is a **target architecture guide** for ObfusCal. Some examples describe the desired end state, not the current implementation.

---

## Overview

This project follows **Clean Architecture** as described by Robert C. Martin. The core principle is the **Dependency Rule**:

> Source code dependencies may only point **inward**. An inner layer must never reference anything from an outer layer.

The architecture is divided into four layers, each with a strict and well-defined responsibility:

```
        ┌─────────────────────────────────┐
        │         Infrastructure          │  ← Outer layer: frameworks, DB, external APIs
        │   ┌─────────────────────────┐   │
        │   │       Application       │   │  ← Use cases, business orchestration
        │   │   ┌─────────────────┐   │   │
        │   │   │      Domain     │   │   │  ← Core business rules, always framework-free
        │   │   └─────────────────┘   │   │
        │   └─────────────────────────┘   │
        └─────────────────────────────────┘
                         ▲
                         │
                    Presentation          ← API controllers/endpoints
```

---

## Project Layout

Target structure:

```
src/
├── ObfusCal.Domain/
├── ObfusCal.Application/
├── ObfusCal.Infrastructure/
└── ObfusCal.Api/

tests/
├── ObfusCal.Domain.Tests/
├── ObfusCal.Application.Tests/
└── ObfusCal.Integration.Tests/
```

Current repository projects are `ObfusCal.Domain`, `ObfusCal.Application`, `ObfusCal.Infrastructure`, `ObfusCal.Api`, and
`ObfusCal.Tests`.

---

## Layer Reference

### 1. Domain - `ObfusCal.Domain`

**The innermost layer. Contains all enterprise business rules.**

#### Rules
- Has **zero external NuGet dependencies**. Only the .NET BCL is allowed.
- Contains no references to any other project in the solution.
- All business logic that is intrinsic to the domain (invariants, rules, state transitions) lives here and nowhere else.
- Never references EF Core, ASP.NET, or any framework.

#### Allowed contents
- **Entities / aggregates** - e.g., `CalendarOwner`, `SyncPeer`, `ShadowSlotBatch`.
- **Value objects** - e.g., `BusyWindow`, `CalendarOwnerId`, `PeerId`.
- **Domain events** - e.g., `BusySlotsObfuscatedEvent`, `ShadowSlotsPushedEvent`.
- **Domain errors** - typed constants like `CalendarOwnerErrors.InvalidRange`.
- **Common base types** - `Entity<TId>`, `AggregateRoot<TId>`, `ValueObject`, `DomainEvent`, `Result<T>`, `Error`.

#### Folder structure

```
ObfusCal.Domain/
├── Common/
│   ├── Entity.cs
│   ├── AggregateRoot.cs
│   ├── ValueObject.cs
│   ├── DomainEvent.cs
│   └── Result.cs
├── CalendarOwners/
│   ├── CalendarOwner.cs
│   ├── CalendarOwnerId.cs
│   ├── CalendarOwnerErrors.cs
│   └── Events/
│       └── BusySlotsObfuscatedEvent.cs
├── BusySlots/
│   ├── BusyWindow.cs
│   ├── ObfuscationPolicy.cs
│   └── BusySlotErrors.cs
└── Sync/
    ├── SyncPeer.cs
    ├── PeerId.cs
    └── ShadowSlotBatch.cs
```

#### Key patterns

**Aggregate roots protect invariants and return `Result<T>` for business failures:**

```csharp
public sealed class CalendarOwner : AggregateRoot<CalendarOwnerId>
{
    private readonly List<BusyWindow> _shadowSlots = new();

    public Result AddShadowSlots(PeerId peerId, IEnumerable<BusyWindow> slots)
    {
        if (slots.Any(s => s.End <= s.Start))
            return Result.Failure(CalendarOwnerErrors.InvalidBusyWindow);

        _shadowSlots.AddRange(slots);
        RaiseDomainEvent(new ShadowSlotsPushedEvent(Id, peerId, slots.Count()));
        return Result.Success();
    }
}
```

**Domain errors are typed constants, never raw strings:**

```csharp
public static class CalendarOwnerErrors
{
    public static readonly Error InvalidBusyWindow =
        new("CalendarOwner.InvalidBusyWindow", "Busy slot end must be after start.");

    public static readonly Error UnknownPeer =
        new("CalendarOwner.UnknownPeer", "The pushing peer is not known for this owner.");
}
```

---

### 2. Application - `ObfusCal.Application`

**Orchestrates domain objects to fulfill use cases. Contains no business logic of its own.**

#### Rules
- References only `ObfusCal.Domain`. Never references `Infrastructure` or `Api`.
- Defines **interfaces** for external dependencies (calendar providers, storage, clock, peer directory).
- Organizes use cases using **CQRS**: commands mutate state, queries read state.
- Contains pipeline behaviors for validation/logging/transactions.
- Uses explicit hand-written mappings (no AutoMapper).

#### Allowed contents
- **Commands and command handlers** - e.g., push incoming shadow slots.
- **Queries and query handlers** - e.g., get merged free/busy view.
- **Validators** - one per command/query.
- **Response DTOs** returned to presentation.
- **Repository and service interfaces** - e.g., `IShadowSlotStore`, `ICalendarSource`.
- **Pipeline behaviors** - `ValidationBehavior`, `LoggingBehavior`, `TransactionBehavior`.
- **Domain event handlers**.

#### Forbidden contents
- EF Core, SQL, or persistence implementation details.
- Direct HTTP clients or SDK usage.
- References to `Infrastructure` or `Api` projects.

#### Folder structure

```
ObfusCal.Application/
├── Common/
│   ├── Abstractions/
│   │   ├── ICommand.cs
│   │   ├── IQuery.cs
│   │   ├── ICommandHandler.cs
│   │   ├── IQueryHandler.cs
│   │   └── IUnitOfWork.cs
│   ├── Behaviors/
│   │   ├── ValidationBehavior.cs
│   │   ├── LoggingBehavior.cs
│   │   └── TransactionBehavior.cs
│   └── Exceptions/
│       └── ValidationException.cs
├── CalendarOwners/
│   ├── Abstractions/
│   │   └── ICalendarOwnerRepository.cs
│   ├── Queries/
│   │   ├── GetBusySlots/
│   │   │   ├── GetBusySlotsQuery.cs
│   │   │   ├── GetBusySlotsHandler.cs
│   │   │   └── BusySlotResponse.cs
│   │   └── GetMergedFreeBusy/
│   │       ├── GetMergedFreeBusyQuery.cs
│   │       ├── GetMergedFreeBusyHandler.cs
│   │       └── MergedFreeBusyResponse.cs
├── ShadowSlots/
│   ├── Abstractions/
│   │   └── IShadowSlotStore.cs
│   └── Commands/
│       └── PushShadowSlots/
│           ├── PushShadowSlotsCommand.cs
│           ├── PushShadowSlotsHandler.cs
│           └── PushShadowSlotsValidator.cs
└── Obfuscation/
    ├── Abstractions/
    │   └── IObfuscationService.cs
    └── EventHandlers/
        └── BusySlotsObfuscatedEventHandler.cs
```

#### Key patterns

**Commands and queries use marker interfaces for pipeline behavior targeting:**

```csharp
public interface ICommand : IRequest<Result>;
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
```

**Handlers depend only on abstractions and orchestrate use cases:**

```csharp
internal sealed class GetMergedFreeBusyHandler(
    ICalendarSource calendarSource,
    IObfuscationService obfuscationService,
    IShadowSlotStore shadowSlotStore)
    : IQueryHandler<GetMergedFreeBusyQuery, IReadOnlyList<BusySlotResponse>>
{
    public async Task<Result<IReadOnlyList<BusySlotResponse>>> Handle(GetMergedFreeBusyQuery query, CancellationToken ct)
    {
        var events = await calendarSource.GetEventsAsync(query.From, query.To, ct);
        var ownSlots = obfuscationService.Obfuscate(events);
        var shadowSlots = await shadowSlotStore.GetAllSlotsAsync(query.From, query.To, ct);

        var merged = ownSlots
            .Concat(shadowSlots)
            .OrderBy(s => s.Start)
            .Select(s => new BusySlotResponse(s.Start, s.End))
            .ToList();

        return Result<IReadOnlyList<BusySlotResponse>>.Success(merged);
    }
}
```

**Interfaces belong to Application, implementation belongs to Infrastructure:**

```csharp
// ObfusCal.Application/ShadowSlots/Abstractions/IShadowSlotStore.cs
public interface IShadowSlotStore
{
    Task SetSlotsAsync(string peerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default);
    Task<IReadOnlyList<BusySlot>> GetAllSlotsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
```

---

### 3. Infrastructure - `ObfusCal.Infrastructure`

**Implements all interfaces defined in Application. Contains all framework and I/O concerns.**

#### Rules
- References both `ObfusCal.Domain` and `ObfusCal.Application`.
- Is not referenced from feature code in `ObfusCal.Api`; wiring happens at composition root (`Program.cs`).
- Classes in this layer primarily exist to implement Application abstractions.
- EF Core configuration, migrations, and persistence logic live here.
- External service clients (calendar APIs, HTTP peers, storage) live here.

#### Allowed contents
- **DbContext** and EF Core mappings.
- **Migrations**.
- **Repository/storage implementations**.
- **Unit of Work implementation**.
- **External service adapters** (calendar providers, peer clients).
- **Domain event dispatcher**.
- **`DependencyInjection.cs`** extension method (target end state).

#### Forbidden contents
- Business logic.
- Direct references to `ObfusCal.Api` feature code.
- Use-case orchestration.

#### Folder structure

```
ObfusCal.Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs
│   ├── AppDbContextFactory.cs
│   ├── Migrations/
│   └── Configurations/
│       └── ShadowSlotEntityConfiguration.cs
├── Storage/
│   ├── EfCoreShadowSlotStore.cs
│   └── InMemoryShadowSlotStore.cs
├── Calendars/
│   ├── MockCalendarSource.cs
│   └── MicrosoftGraphCalendarSource.cs
└── DependencyInjection.cs
```

#### Key patterns

**Implement interfaces from Application/Core contracts only:**

```csharp
internal sealed class EfCoreShadowSlotStore(AppDbContext dbContext) : IShadowSlotStore
{
    public async Task SetSlotsAsync(string peerId, IReadOnlyList<BusySlot> slots, CancellationToken ct = default)
    {
        var existing = await dbContext.ShadowSlots.Where(s => s.PeerId == peerId).ToListAsync(ct);
        dbContext.ShadowSlots.RemoveRange(existing);
        dbContext.ShadowSlots.AddRange(slots.Select(s => ShadowSlotEntity.FromDomain(peerId, s)));
        await dbContext.SaveChangesAsync(ct);
    }
}
```

**Centralize registrations in one extension method (target):**

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        services.AddScoped<IShadowSlotStore, EfCoreShadowSlotStore>();
        services.AddScoped<ICalendarSource, MockCalendarSource>();
        return services;
    }
}
```

---

### 4. Presentation - `ObfusCal.Api`

**The entry point of the application. Translates HTTP to application commands/queries and back.**

#### Rules
- References only `ObfusCal.Application` in the target architecture.
- The composition root (`Program.cs`) is the place where Application and Infrastructure meet.
- Controllers/endpoints are thin: receive input, dispatch command/query, translate result.
- Validation is handled in Application via pipeline behaviors.

#### Allowed contents
- Controllers or Minimal APIs.
- Middleware (exception handling, request logging, correlation IDs).
- `Program.cs` composition root.
- Result-to-ProblemDetails mapping extensions.

#### Forbidden contents
- Business logic.
- Direct database access.
- Domain entity construction in controllers.
- Direct use of infrastructure implementations (`EfCoreShadowSlotStore`, `AppDbContext`, etc.).

#### Folder structure

```
ObfusCal.Api/
├── Program.cs
├── appsettings.json
├── Controllers/
│   ├── CalendarOwnersController.cs
│   └── ShadowSlotsController.cs
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
└── Extensions/
    └── ResultExtensions.cs
```

#### Key patterns

**Controllers are delivery mechanisms only:**

```csharp
[ApiController]
[Route("api/calendar-owners")]
public sealed class CalendarOwnersController(ISender sender) : ControllerBase
{
    [HttpGet("{id}/busy-slots")]
    public async Task<IActionResult> GetBusySlots(string id, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken ct)
    {
        var result = await sender.Send(new GetBusySlotsQuery(id, from, to), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemDetails();
    }

    [HttpGet("{id}/merged-freebusy")]
    public async Task<IActionResult> GetMergedFreeBusy(string id, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken ct)
    {
        var result = await sender.Send(new GetMergedFreeBusyQuery(id, from, to), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblemDetails();
    }
}
```

**Composition root wires all layers (target):**

```csharp
builder.Services
    .AddApplication()                          // validators, behaviors
    .AddInfrastructure(builder.Configuration); // DbContext, stores, adapters
```

---

## Dependency Graph

The arrows represent "references / depends on":

```
ObfusCal.Api
    │
    └──► ObfusCal.Application
                │
                └──► ObfusCal.Domain

ObfusCal.Infrastructure
    │
    ├──► ObfusCal.Application   (implements interfaces defined here)
    └──► ObfusCal.Domain        (persists and reads entities/value objects)
```

`ObfusCal.Api` and `ObfusCal.Infrastructure` should not depend on each other's feature code. They connect at runtime through DI.

---

## Cross-Cutting Concerns

### Error Handling

Use a `Result<T>` / `Error` pattern throughout. Never use exceptions to communicate business rule violations.

- Domain methods return `Result<T>`.
- Application handlers propagate `Result<T>` to the API layer.
- The API maps failure results to RFC 9457 problem details responses.
- Exceptions are reserved for unrecoverable failures (for example: DB connection failure) and handled by middleware.

### Validation

- `ValidationBehavior<TRequest, TResponse>` runs validators before handlers.
- Domain rule validation is in domain methods and returned as `Result` failures.

### Domain Events

- Aggregates raise events like `BusySlotsObfuscatedEvent`.
- Events are dispatched after persistence succeeds.
- Event handlers live in Application and handle side effects (notifications, read-model updates, telemetry).

### Transactions

- `TransactionBehavior<TRequest, TResponse>` wraps commands in a transaction.
- Queries do not run inside write transactions.
- Handlers do not begin transactions manually.

---

## Testing Strategy

Each layer should have a dedicated test project and strategy.

| Layer | Test project | Strategy |
|---|---|---|
| Domain | `ObfusCal.Domain.Tests` | Pure unit tests for invariants and value objects. |
| Application | `ObfusCal.Application.Tests` | Unit tests with mocked interfaces (`ICalendarSource`, `IShadowSlotStore`). |
| Integration | `ObfusCal.Integration.Tests` | End-to-end HTTP tests via `WebApplicationFactory<Program>`. |

Current state: tests are centralized in `ObfusCal.Tests`. Split by layer incrementally while preserving coverage.

---

## NuGet Package Ownership by Layer

| Package | Allowed in |
|---|---|
| *(none)* | Domain |
| Microsoft.Extensions.DependencyInjection.Abstractions | Application |
| Entity Framework Core | Infrastructure |
| Npgsql.EntityFrameworkCore.PostgreSQL | Infrastructure |
| Serilog | Infrastructure |
| Calendar provider SDKs / HTTP clients | Infrastructure |
| Microsoft.AspNetCore | Api |
| Swashbuckle | Api |

If you want to add a NuGet package to `ObfusCal.Domain`, stop and reconsider first.

---

## Rules Summary

| Rule | Detail |
|---|---|
| **Dependency direction** | Always inward. Never outward. |
| **Domain has no dependencies** | Zero external NuGet packages. Zero project references. |
| **Interfaces in Application** | Repository/service interfaces are defined in `Application`, implemented in `Infrastructure`. |
| **Infrastructure not used directly in Api features** | They meet at the composition root in `Program.cs`. |
| **No logic in controllers** | Controllers dispatch commands/queries and map results. |
| **No business logic in Application** | Handlers orchestrate; domain rules stay in domain. |
| **No persistence in Application** | Handlers call abstractions, never EF/SQL directly. |
| **Result pattern, not exceptions** | Business rule violations return `Result.Failure(error)`. |
| **One handler per use case** | Every command/query has one handler. |
| **Validators in Application** | One validator per command/query, executed via pipeline. |
