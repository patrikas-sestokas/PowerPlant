using System.Net;
using System.Net.Http.Json;
using API.Models;
using Microsoft.Extensions.DependencyInjection;

namespace API.Test;

public class PowerPlantPostTests
{
    // Minimal type to deserialize validation result
    private sealed class ValidationProblem
    {
        public string? Type { get; init; }
        public string? Title { get; init; }
        public int? Status { get; init; }
        public Dictionary<string, string[]> Errors { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_ReturnsCreated_WhenPayloadIsValid()
    {
        await using var factory = new PowerPlantApiFactory();
        using var client = factory.CreateClient();

        var payload = new PowerPlantDto(
            "Jane Doe",
            125,
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 12, 31));

        var response = await client.PostAsJsonAsync("/powerplants", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("/powerplants", response.Headers.Location?.LocalPath);

        var created = await response.Content.ReadFromJsonAsync<PowerPlant>();
        Assert.NotNull(created);
        Assert.Equal(payload.Owner, created!.Owner);
        Assert.Equal(Convert.ToDecimal(payload.Power), created.Power);
        Assert.Equal(payload.ValidFrom, created.ValidFrom);
        Assert.Equal(payload.ValidTo, created.ValidTo);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(db.PowerPlants);
    }

    [Theory]
    [InlineData("Jane Mary Doe")]
    [InlineData("Jane 123")]
    [InlineData("123 Doe")]
    public async Task Post_ReturnsValidationProblem_WhenOwnerFailsRegex(string owner)
    {
        var vp = await AssertValidationProblemAsync(
            new PowerPlantDto(owner, 50, new DateOnly(2025, 1, 1)));

        var expected =
            $"'owner' must be two words (letters only) separated by a space, received: {owner}";
        Assert.Contains(expected, vp.Errors["owner"]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(201)]
    [InlineData(250.5)]
    public async Task Post_ReturnsValidationProblem_WhenPowerOutOfRange(decimal power)
    {
        var vp = await AssertValidationProblemAsync(
            new PowerPlantDto("Jane Doe", power, new DateOnly(2025, 1, 1)));

        var expected = $"'power' must be between 0 and 200, received: {power}";
        Assert.Contains(expected, vp.Errors["power"]);
    }

    [Fact]
    public async Task Post_ReturnsValidationProblem_WhenValidToPrecedesValidFrom()
    {
        var validFrom = new DateOnly(2025, 1, 10);
        var validTo = new DateOnly(2025, 1, 5);

        var vp = await AssertValidationProblemAsync(
            new PowerPlantDto("Jane Doe", 100, validFrom, validTo));

        var expected =
            $"'validTo' precedes 'validFrom', received: {validFrom:yyyy-MM-dd} - {validTo:yyyy-MM-dd}";
        Assert.Contains(expected, vp.Errors["validTo"]);
    }

    [Fact]
    public async Task Post_ReturnsAllValidationErrors_WhenMultipleIssues()
    {
        // Missing owner + missing power + missing validFrom
        var vp = await AssertValidationProblemAsync(new
        {
            owner = (string?)null,
            power = (decimal?)null,
            validFrom = (DateOnly?)null,
            validTo = (DateOnly?)null
        });

        Assert.Contains("'owner' cannot be empty", vp.Errors["owner"]);
        Assert.Contains("'power' cannot be empty", vp.Errors["power"]);
        Assert.Contains("'validFrom' cannot be empty", vp.Errors["validFrom"]);
        // Check content type is a problem+json
        Assert.Equal(400, vp.Status);
    }
    
    [Theory]
    [InlineData("owner", null)]
    [InlineData("owner", "")]
    [InlineData("owner", " ")]
    [InlineData("power", null)]
    [InlineData("validFrom", null)]
    public async Task Post_ReturnsValidationProblem_WhenRequiredFieldMissing(string field, object? value)
    {
        var date = new DateOnly(2025, 1, 1);

        // Build the minimal payload for the given missing field
        var payload = field switch
        {
            "owner"     => new PowerPlantDto((string?)value, 50m, date),
            "power"     => new PowerPlantDto("Jane Doe", null, date),
            "validFrom" => new PowerPlantDto("Jane Doe", 50m, null),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        var vp = await AssertValidationProblemAsync(payload);

        var expected = field switch
        {
            "owner"     => "'owner' cannot be empty",
            "power"     => "'power' cannot be empty",
            "validFrom" => "'validFrom' cannot be empty",
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        Assert.Contains(expected, vp.Errors[field]);
    }
    
    [Fact]
    public async Task Post_ReturnsValidationProblem_WhenOwnerTooLong()
    {
        var vp = await AssertValidationProblemAsync(
            new PowerPlantDto(new string('a', 201), 50m, new DateOnly(2025, 1, 1)));

        var expected = "'owner' must be at most 200 characters long";
        Assert.Contains(expected, vp.Errors["owner"]);
    }

    // --- helpers ---

    private static async Task<ValidationProblem> AssertValidationProblemAsync<T>(T payload)
    {
        await using var factory = new PowerPlantApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/powerplants", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Optional: content-type assertion
        Assert.Contains("problem+json", response.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase);

        var validation = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        Assert.NotNull(validation);
        Assert.Equal(400, validation!.Status);
        Assert.NotEmpty(validation.Errors);
        return validation;
    }
}
