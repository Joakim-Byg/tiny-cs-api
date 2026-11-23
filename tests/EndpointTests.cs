using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Tests;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRootEndpoint_ReturnsOkAndMessage()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"service\":\"tiny-cs-api\"", content);
        Assert.Contains("service is running", content);
    }

    [Fact]
    public async Task GetReadyzEndpoint_ReturnsOkAndReady()
    {
        var response = await _client.GetAsync("/readyz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ready\"", content);
    }

    [Fact]
    public async Task GetLivezEndpoint_ReturnsOkAndLive()
    {
        var response = await _client.GetAsync("/livez");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"live\"", content);
    }

    [Fact]
    public async Task GetStartupEndpoint_ReturnsOkAndStarted()
    {
        var response = await _client.GetAsync("/health/startup");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"started\"", content);
    }

    [Fact]
    public async Task GetReadyEndpoint_ReturnsOkAndReady()
    {
        var response = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ready\"", content);
    }

    [Fact]
    public async Task GetLiveEndpoint_ReturnsOkAndLive()
    {
        var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"live\"", content);
    }

    [Fact]
    public async Task GetMetricsEndpoint_ReturnsPrometheusText()
    {
        var response = await _client.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("http_requests_total", content);
        Assert.Contains("http_request_duration_seconds", content);
    }
}
