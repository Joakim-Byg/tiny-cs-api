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

    [Fact]
    public async Task GetDarkLaunchEndpoint_WhenDisabled_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/dark-launch");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("disabled", content);
    }

    [Fact]
    public async Task GetFeaturesEndpoint_ReturnsFeatureStatus()
    {
        var response = await _client.GetAsync("/features");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("darkLaunch", content);
    }

    [Fact]
    public async Task GetDarkLaunchEndpoint_WhenDynamicallyEnabledViaConfigFile_ReloadsAtRuntime()
    {
        var tempConfigFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempConfigFile, "{\"Features\":{\"DarkLaunch\":{\"Enabled\":false,\"Message\":\"Initial\"}}}");
            Environment.SetEnvironmentVariable("CONFIG_PATH", tempConfigFile);

            using var customFactory = new WebApplicationFactory<Program>();
            var client = customFactory.CreateClient();

            // 1. Initial state: disabled -> 404
            var response1 = await client.GetAsync("/api/dark-launch");
            Assert.Equal(HttpStatusCode.NotFound, response1.StatusCode);

            // 2. Modify config file at runtime
            await File.WriteAllTextAsync(tempConfigFile, "{\"Features\":{\"DarkLaunch\":{\"Enabled\":true,\"Message\":\"Dynamically Activated!\"}}}");

            // Allow FileSystemWatcher to detect file modification
            HttpResponseMessage? response2 = null;
            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(100);
                response2 = await client.GetAsync("/api/dark-launch");
                if (response2.StatusCode == HttpStatusCode.OK)
                {
                    break;
                }
            }

            Assert.NotNull(response2);
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
            var content2 = await response2.Content.ReadAsStringAsync();
            Assert.Contains("Dynamically Activated!", content2);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONFIG_PATH", null);
            if (File.Exists(tempConfigFile))
            {
                File.Delete(tempConfigFile);
            }
        }
    }
}
