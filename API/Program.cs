using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using API;
using API.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")).UseSnakeCaseNamingConvention());
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        // Binding errors (bad JSON, wrong types, etc.)
        if (ex is BadHttpRequestException bad)
        {
            await Results.Problem(
                title: "Invalid request payload",
                detail: bad.Message, // e.g., "Failed to read parameter ..."
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    ["hint"] = "Check field types and formats (e.g., dates as yyyy-MM-dd)."
                }
            ).ExecuteAsync(context);
            return;
        }

        // Fallback
        await Results.Problem(
            title: "Unexpected error",
            statusCode: StatusCodes.Status500InternalServerError
        ).ExecuteAsync(context);
    });
});

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.MapOpenApi();
app.UseHttpsRedirection();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/openapi/v1.json", "PowerPlant API v1");
    c.RoutePrefix = "docs";
    c.DocumentTitle = "PowerPlant API";
});

app.MapGet("/powerplants/{id:guid}", async (Guid id, ApplicationDbContext db, CancellationToken ct) =>
{
    var entity = await db.PowerPlants.FindAsync([id], ct);
    return entity is null ? Results.NotFound() : Results.Ok(entity);
}).WithName("GetPowerPlantById");

app.MapGet("/powerplants", async (ApplicationDbContext db, CancellationToken ct, string? owner, int page = 0, int count = 10) =>
{
    page = Math.Max(0, page);
    count = Math.Clamp(count <= 0 ? 10 : count, 1, 200);
    var query = db.PowerPlants.AsNoTracking().OrderBy(p => p.Id).AsQueryable();

    if (!string.IsNullOrWhiteSpace(owner))
    {
        if (db.Database.IsNpgsql())
            query = query.Where(p =>
                EF.Functions.ILike(
                    PgFunctions.Unaccent(p.Owner),
                    PgFunctions.Unaccent($"%{owner}%")));
        else if(db.Database.IsRelational())
            query = query.Where(p => EF.Functions.ILike(p.Owner, $"%{owner}%"));
        else
            // Fallback branch in case db is in-memory or some other sort
            // ReSharper disable once EntityFramework.UnsupportedServerSideFunctionCall
            query = query.Where(p => p.Owner.Contains(owner, StringComparison.OrdinalIgnoreCase));
    }

    var totalCount = await query.CountAsync(ct);
    var totalPages = (int)Math.Ceiling((double)totalCount / count);

    var list = await query
        .Skip(page * count)
        .Take(count)
        .ToListAsync(ct);

    return Results.Ok(new PowerPlantListResponse(list, totalCount, totalPages));
}).WithName("GetPowerPlants");

// \p{L} - matches any kind of letter from any language.
// [\p{L}'-] - in addition matches dashes or quotes attributable to some names
// + - repeats previous condition 1..N times
// \s - whitespace
// $ - end of string
// ^ - start of string
var ownerValidation = new Regex(@"^\p{L}[\p{L}'-]+\s[\p{L}'-]+$", RegexOptions.Compiled);

// More fluent validation packages could be used, but for 4 fields it's overkill, so manual approach is picked
app.MapPost("/powerplants", async (ApplicationDbContext db, CancellationToken ct, [FromBody] PowerPlantDto dto) =>
{
    var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(dto.Owner))
        errors["owner"] = ["'owner' cannot be empty"];
    else if (!ownerValidation.IsMatch(dto.Owner))
        errors["owner"] = [$"'owner' must be two words (letters only) separated by a space, received: {dto.Owner}"];
    
    if (dto.Power is null)
        errors["power"] = ["'power' cannot be empty"];
    else if(dto.Power is < 0 or > 200)
        errors["power"] = [$"'power' must be between 0 and 200, received: {dto.Power}"];
    
    if (dto.ValidFrom is null)
        errors["validFrom"] = ["'validFrom' cannot be empty"];
    else if (dto.ValidTo is not null && dto.ValidFrom > dto.ValidTo)
        errors["validTo"] = [$"'validTo' precedes 'validFrom', received: {dto.ValidFrom:yyyy-MM-dd} - {dto.ValidTo:yyyy-MM-dd}"];

    if (errors.Count > 0)
        return TypedResults.ValidationProblem(errors, title: "One or more validation errors occurred.");
    
    var entity = new PowerPlant
    {
        Owner = dto.Owner!,
        Power = dto.Power!.Value,
        ValidFrom = dto.ValidFrom!.Value,
        ValidTo = dto.ValidTo
    };
    
    await db.PowerPlants.AddAsync(entity, ct);
    await db.SaveChangesAsync(ct);

    return Results.CreatedAtRoute(
        routeName: "GetPowerPlantById",
        routeValues: new { id = entity.Id },
        value: entity);
}).WithName("PostPowerPlant");
app.MapHealthChecks("/healthz");
app.Run();

// Annotations don't work with minimal API infrastructure for validation purposes, but that is not their purpose.
// Their purpose is to inform OpenAPI service about shape and requirements of payload.
public sealed record PowerPlantDto(
    [property: Required, MaxLength(200)] string? Owner,
    [property: Required, Range(0,200)] decimal? Power,
    [property: Required] DateOnly? ValidFrom, 
    DateOnly? ValidTo = null);

public sealed record PowerPlantListResponse(IReadOnlyList<PowerPlant> PowerPlants, int TotalCount, int TotalPages);
public partial class Program;