using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using API;
using API.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")).UseSnakeCaseNamingConvention());
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    app.MapOpenApi();
    app.MapScalarApiReference("/docs", options =>
    {
        options.WithTitle("PowerPlant API")
            .ExpandAllTags()
            .ShowOperationId();
    });
}

app.UseHttpsRedirection();

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
        else
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

app.MapPost("/powerplants", async (ApplicationDbContext db, CancellationToken ct, PowerPlantDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Owner))
        return Results.BadRequest("\"owner\" cannot be empty");
    if (!ownerValidation.IsMatch(dto.Owner))
        return Results.BadRequest($"\"owner\" does not consist of two words (text-only characters) separated by a whitespace, received: {dto.Owner}");
    
    if(dto.Power is < 0 or > 200)
        return Results.BadRequest($"\"power\" must be between 0 and 200, received: {dto.Power}");

    if (dto.ValidTo is not null && dto.ValidFrom > dto.ValidTo)
        return Results.BadRequest(
            $"\"valid_to\" precedes \"valid_from\", received: {dto.ValidFrom:yyyy-MM-dd} - {dto.ValidTo:yyyy-MM-dd}");
    
    var entity = new PowerPlant
    {
        Owner = dto.Owner,
        Power = dto.Power,
        ValidFrom = dto.ValidFrom,
        ValidTo = dto.ValidTo
    };
    
    await db.PowerPlants.AddAsync(entity, ct);
    await db.SaveChangesAsync(ct);

    return Results.CreatedAtRoute(
        routeName: "GetPowerPlantById",
        routeValues: new { id = entity.Id },
        value: entity);
}).WithName("PostPowerPlant");

app.Run();

public sealed record PowerPlantDto(string Owner, decimal Power, DateOnly ValidFrom, DateOnly? ValidTo = null);
public sealed record PowerPlantListResponse(IReadOnlyList<PowerPlant> PowerPlants, int TotalCount, int TotalPages);
public partial class Program;