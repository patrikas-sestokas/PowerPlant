# PowerPlant API
PowerPlant is a minimal REST API for storing and querying power plant metadata. It exposes CRUD-lite functionality (create + read) backed by Entity Framework Core and PostgreSQL, with automated validation, pagination, and accent-insensitive search.

## Features
- Minimal API built on .NET 9 with PostgreSQL persistence via EF Core.
- `GET /powerplants` with pagination, optional owner filtering, and case/accent insensitive matching (powered by `unaccent`/`pg_trgm` extensions).
- `GET /powerplants/{id}` to retrieve a single plant by identifier.
- `POST /powerplants` with explicit validation for owner format, power bounds, and date consistency, returning RFC 9457 problem responses.
- OpenAPI/Swagger UI at `/docs` plus OpenAPI document at `/openapi/v1.json`.
- `/healthz` health probe for readiness checks.
- Comprehensive integration-test suite covering happy paths, validation failures, and pagination/filtering scenarios.

## Project Structure
- `API/` – ASP.NET Core minimal API, EF Core context, migrations, and Dockerfile.
- `API.Test/` – xUnit integration tests using `WebApplicationFactory` and EF Core InMemory provider.
- `power_plants.sql` – optional seed data for PostgreSQL.
- `JUNIOR_NET_DEVELOPER_task.md` – original assignment brief.

## Getting Started
### Prerequisites
- .NET SDK 9.0+
- PostgreSQL 14+ (extensions `unaccent` and `pg_trgm` must be available)
- Optional: `psql` CLI for applying seed data

### Configuration
1. Make `API/appsettings.Development.json` for local overrides or set the `ConnectionStrings__Default` environment variable.  
   Example:  
   ```bash
   export ConnectionStrings__Default="Host=localhost;Port=5432;Database=powerplant;Username=powerplant;Password=change-me"
   ```
2. Ensure the configured PostgreSQL user can create the database (first run) and install the required extensions. This is handled automatically by EF Core migrations.

### Database Setup
Migrations execute on application start in `Development`, but for other environments run:
```bash
dotnet ef database update --project API
```
Optional sample data:
```bash
psql -d powerplant -f power_plants.sql
```

## Running the API
```bash
dotnet run --project API
```
Swagger UI becomes available at `https://localhost:5062/docs` (or the configured port). The repository also includes `API/PowerPlant.API.http` with ready-to-run HTTP requests for IDE clients.

## Running with Docker
Build the container image from the repository root using the Dockerfile in `API/`:
```bash
docker build -t powerplant-api -f API/Dockerfile API
```
Run the container, providing the connection string for your PostgreSQL instance and publishing port `8080`:
```bash
docker run --rm \
  -p 8080:8080 \
  -e ConnectionStrings__Default="Host=host.docker.internal;Port=5432;Database=powerplant;Username=powerplant;Password=change-me" \
  powerplant-api
```
Swagger UI will be served at `http://localhost:8080/docs`. Ensure the database already has the required schema (`dotnet ef database update`) before starting the container.

## Running Tests
```bash
dotnet test
```
Tests exercise the HTTP surface and validation logic end-to-end using an in-memory database.

## API Reference
### `GET /powerplants`
Query parameters:
- `owner` – filters by owner substring, case/accent insensitive when supported by the provider.
- `page` – zero-based page number (defaults to `0`).
- `count` – page size from `1` to `200` (defaults to `10`).

Response example:
```json
{
  "powerPlants": [
    {
      "id": "dd0ee67e-bee1-44c6-a773-5e9b133a0c95",
      "owner": "Vardenis Pavardenis",
      "power": 9.3,
      "validFrom": "2020-01-01",
      "validTo": "2025-01-01"
    }
  ],
  "totalCount": 1,
  "totalPages": 1
}
```

### `GET /powerplants/{id}`
Returns `404 Not Found` when the specified plant is missing.

### `POST /powerplants`
Request payload:
```json
{
  "owner": "Jane Doe",
  "power": 125,
  "validFrom": "2025-01-01",
  "validTo": "2025-12-31"
}
```
Validation rules:
- `owner` must be two alphabetic words separated by one space.
- `power` must be `0 <= value <= 200`.
- `validFrom` is required; `validTo`, when provided, cannot precede `validFrom`.
Validation failures return RFC 9457 `application/problem+json` responses with per-field messages.

## Known Limitations
- Write concurrency control is limited to the database; there is no optimistic concurrency token.
- PostgreSQL extensions (`unaccent`, `pg_trgm`) must exist; some managed providers disable them. Without them the search falls back to case-insensitive comparison, but matching quality is reduced.
- Tests rely on EF Core's InMemory provider, so provider-specific behavior (e.g., `ILIKE`, extensions) is not covered by automated tests.
