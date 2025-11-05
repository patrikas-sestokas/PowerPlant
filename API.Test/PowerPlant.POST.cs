using System.Net;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using API.Models;
using Microsoft.Extensions.DependencyInjection;

namespace API.Test;

public class PowerPlantPostTests
{
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
    [InlineData(null, "\"owner\" cannot be empty")]
    [InlineData("", "\"owner\" cannot be empty")]
    [InlineData(" ", "\"owner\" cannot be empty")]
    public async Task Post_ReturnsBadRequest_WhenOwnerMissing(string? owner, string expectedMessage)
    {
        await AssertBadRequestAsync(new
        {
            owner,
            power = 50,
            validFrom = new DateOnly(2025, 1, 1),
            validTo = (DateOnly?)null
        }, expectedMessage);
    }

    [Theory]
    [InlineData("Jane Mary Doe")]
    [InlineData("Jane 123")]
    [InlineData("123 Doe")]
    public async Task Post_ReturnsBadRequest_WhenOwnerFailsRegex(string owner)
    {
        var expectedMessage =
            $"\"owner\" does not consist of two words (text-only characters) separated by a whitespace, received: {owner}";

        await AssertBadRequestAsync(
            new PowerPlantDto(owner, 50, new DateOnly(2025, 1, 1)),
            expectedMessage);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(201)]
    [InlineData(250.5)]
    public async Task Post_ReturnsBadRequest_WhenPowerOutOfRange(decimal power)
    {
        var expectedMessage = $"\"power\" must be between 0 and 200, received: {power}";

        await AssertBadRequestAsync(
            new PowerPlantDto("Jane Doe", power, new DateOnly(2025, 1, 1)), 
            expectedMessage);
    }

    [Fact]
    public async Task Post_ReturnsBadRequest_WhenValidToPrecedesValidFrom()
    {
        var validFrom = new DateOnly(2025, 1, 10);
        var validTo = new DateOnly(2025, 1, 5);
        var expectedMessage =
            $"\"valid_to\" precedes \"valid_from\", received: {validFrom:yyyy-MM-dd} - {validTo:yyyy-MM-dd}";

        await AssertBadRequestAsync(
            new PowerPlantDto("Jane Doe", 100, validFrom, validTo), 
            expectedMessage);
    }

    private static async Task AssertBadRequestAsync<T>(T payload, string expectedMessage)
    {
        await using var factory = new PowerPlantApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/powerplants", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<string>();
        Assert.Equal(expectedMessage, content);
    }
}
