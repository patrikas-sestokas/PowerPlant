using System.Net;
using System.Net.Http.Json;
using API;
using API.Models;
using Microsoft.Extensions.DependencyInjection;

namespace API.Test;

public class PowerPlantGetTests
{
    [Fact]
    public async Task Get_ReturnsNotFound_WhenPlantDoesNotExist()
    {
        await using var factory = new PowerPlantApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/powerplants/{CreateGuid(0)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact]
    public async Task Get_ReturnsPlant_WhenPlantExists()
    {
        await using var factory = new PowerPlantApiFactory();
        var id = CreateGuid(1);
        var plant = CreatePlant("Jane Doe", 125, new DateOnly(2025, 1, 1), id: id);
        await SeedAsync(factory, plant);

        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<PowerPlant>($"/powerplants/{id}");

        Assert.Equivalent(plant, result, strict: true);
    }
    [Fact]
    public async Task Get_ReturnsEmptyCollection_WhenNoPlantsExist()
    {
        await using var factory = new PowerPlantApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/powerplants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PowerPlantListResponse>();
        Assert.NotNull(payload);
        Assert.Empty(payload!.PowerPlants);
    }

    [Fact]
    public async Task Get_ReturnsFirstPage_WithDefaultPagination()
    {
        await using var factory = new PowerPlantApiFactory();
        var plants = Enumerable.Range(0, 12)
            .Select(i => CreatePlant(
                owner: $"Owner {i}",
                power: 50 + i,
                validFrom: new DateOnly(2025, 1, 1).AddDays(i),
                id: CreateGuid(i)))
            .ToArray();
        await SeedAsync(factory, plants);

        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<PowerPlantListResponse>("/powerplants");

        Assert.NotNull(result);
        Assert.Equal(10, result!.PowerPlants.Count);
        Assert.Equal(
            plants.OrderBy(p => p.Id).Take(10).Select(p => p.Id), 
            result.PowerPlants.Select(p => p.Id));
    }

    [Fact]
    public async Task Get_ReturnsRequestedPage_WhenPagingApplied()
    {
        await using var factory = new PowerPlantApiFactory();
        var plants = Enumerable.Range(0, 8)
            .Select(i => CreatePlant(
                owner: $"Owner {i}",
                power: 60 + i,
                validFrom: new DateOnly(2025, 2, 1).AddDays(i),
                id: CreateGuid(i)))
            .ToArray();
        await SeedAsync(factory, plants);

        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<PowerPlantListResponse>("/powerplants?page=1&count=3");

        Assert.NotNull(result);
        Assert.Equal(3, result!.PowerPlants.Count);
        Assert.Equal(
            plants.OrderBy(p => p.Id).Skip(3).Take(3).Select(p => p.Id), 
            result.PowerPlants.Select(p => p.Id));
    }

    [Fact]
    public async Task Get_NormalizesNegativePageAndZeroCount()
    {
        await using var factory = new PowerPlantApiFactory();
        var plants = Enumerable.Range(0, 6)
            .Select(i => CreatePlant(
                owner: $"Owner {i}",
                power: 70 + i,
                validFrom: new DateOnly(2025, 3, 1).AddDays(i),
                id: CreateGuid(i)))
            .ToArray();
        await SeedAsync(factory, plants);

        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<PowerPlantListResponse>("/powerplants?page=-5&count=0");

        Assert.NotNull(result);
        Assert.Equal(
            plants.OrderBy(p => p.Id).Take(6).Select(p => p.Id), 
            result.PowerPlants.Select(p => p.Id));
    }

    [Fact]
    public async Task Get_ClampsMaximumPageSize()
    {
        await using var factory = new PowerPlantApiFactory();
        var plants = Enumerable.Range(0, 210)
            .Select(i => CreatePlant(
                owner: $"Owner {i}",
                power: 80 + i,
                validFrom: new DateOnly(2025, 4, 1).AddDays(i),
                id: CreateGuid(i)))
            .ToArray();
        await SeedAsync(factory, plants);

        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<PowerPlantListResponse>("/powerplants?count=500");

        Assert.NotNull(result);
        Assert.Equal(200, result!.PowerPlants.Count);
        Assert.Equal(
            plants.OrderBy(p => p.Id).Take(200).Select(p => p.Id),
            result.PowerPlants.Select(p => p.Id));
    }

    [Fact]
    public async Task Get_FiltersByOwner_WhenOwnerQueryProvided()
    {
        await using var factory = new PowerPlantApiFactory();
        var matching = CreatePlant("Jane Doe", 90, new DateOnly(2025, 5, 1), id: CreateGuid(1));
        var other = CreatePlant("John Smith", 95, new DateOnly(2025, 5, 2), id: CreateGuid(2));
        await SeedAsync(factory, matching, other);

        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<PowerPlantListResponse>("/powerplants?owner=Jane");

        Assert.NotNull(result);
        Assert.Single(result!.PowerPlants);
        Assert.Equal(matching.Id, result.PowerPlants[0].Id);
    }

    private static async Task SeedAsync(PowerPlantApiFactory factory, params PowerPlant[] plants)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.PowerPlants.RemoveRange(db.PowerPlants);
        await db.SaveChangesAsync();
        db.PowerPlants.AddRange(plants);
        await db.SaveChangesAsync();
    }

    private static PowerPlant CreatePlant(string owner, decimal power, DateOnly validFrom, DateOnly? validTo = null, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Owner = owner,
            Power = power,
            ValidFrom = validFrom,
            ValidTo = validTo
        };

    private static Guid CreateGuid(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value + 1:000000000000}");
}

file sealed record PowerPlantListResponse(List<PowerPlant> PowerPlants);
