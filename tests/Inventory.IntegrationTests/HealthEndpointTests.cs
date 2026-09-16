using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>
/// Task 1.13 — verifies the health probes against a real HTTP pipeline.
/// </summary>
/// <remarks>
/// These run the actual API host through <see cref="WebApplicationFactory{TEntryPoint}"/>,
/// so they exercise middleware ordering, routing and response shaping rather than a mock.
/// </remarks>
public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    /// <summary>Creates the fixture-backed test class.</summary>
    /// <param name="factory">The in-memory API host.</param>
    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>Liveness answers 200 without touching any dependency.</summary>
    [Fact]
    public async Task Liveness_Returns_Healthy()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }

    /// <summary>
    /// Liveness discloses nothing beyond its own status.
    /// </summary>
    /// <remarks>
    /// The probe is unauthenticated (docs/32 CR-064). An unauthenticated endpoint that leaks
    /// a framework version, an environment name or a machine name hands a scanner its first
    /// foothold for free.
    /// </remarks>
    [Fact]
    public async Task Liveness_Does_Not_Disclose_Environment_Detail()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health");
        string body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Development", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("version", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Readiness answers, and answers truthfully, whether or not a database exists.
    /// </summary>
    /// <remarks>
    /// PostgreSQL is not installed on the development machine yet (docs/33 section 4.3), so
    /// this is expected to report <c>Unhealthy</c> with HTTP 503 today and <c>Healthy</c> with
    /// HTTP 200 once it is. Both are correct; what would be wrong is reporting ready without a
    /// reachable database. The assertion therefore pins the contract — a definite verdict and
    /// no leaked detail — rather than one of the two verdicts.
    /// </remarks>
    [Fact]
    public async Task Readiness_Reports_A_Definite_Verdict_Without_Leaking_Detail()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health/ready");
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Readiness must answer 200 or 503; got {(int)response.StatusCode}.");

        Assert.True(
            body is "Healthy" or "Unhealthy" or "Degraded",
            $"Readiness must report a health status; got '{body}'.");

        Assert.DoesNotContain("Host=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Every response carries a correlation id for tracing.</summary>
    [Fact]
    public async Task Responses_Carry_A_Correlation_Id()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health");

        Assert.True(
            response.Headers.TryGetValues("X-Correlation-Id", out var values),
            "Every response must carry X-Correlation-Id (docs/21 section 3).");

        Assert.False(string.IsNullOrWhiteSpace(values!.FirstOrDefault()));
    }

    /// <summary>A well-formed inbound correlation id is preserved across the call.</summary>
    [Fact]
    public async Task Inbound_Correlation_Id_Is_Echoed()
    {
        using HttpClient client = _factory.CreateClient();

        const string supplied = "test-correlation-0001";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", supplied);

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(supplied, response.Headers.GetValues("X-Correlation-Id").Single());
    }

    /// <summary>
    /// A hostile inbound correlation id is replaced, not echoed.
    /// </summary>
    /// <remarks>
    /// The header is attacker-controlled text that lands in log files. Echoing newlines or
    /// control characters into a log is log forging — an attacker writes a convincing fake
    /// entry and the audit trail can no longer be trusted.
    /// </remarks>
    [Theory]
    [InlineData("bad\nvalue")]
    [InlineData("bad\rvalue")]
    [InlineData("<script>alert(1)</script>")]
    public async Task Malformed_Correlation_Id_Is_Replaced(string hostile)
    {
        using HttpClient client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", hostile);

        using HttpResponseMessage response = await client.SendAsync(request);
        string returned = response.Headers.GetValues("X-Correlation-Id").Single();

        Assert.NotEqual(hostile, returned);
        Assert.True(Guid.TryParse(returned, out _), $"Expected a generated GUID; got '{returned}'.");
    }
}
