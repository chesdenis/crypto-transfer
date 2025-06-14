using System.Net.Http.Json;
using System.Text.Json;
using ct.lib.model;

namespace ct.client;

public class CtPointClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the CtPointClient class with the base server URL.
    /// </summary>
    /// <param name="baseUrl">The base URL of the CtPoint server (e.g., http://localhost:8080).</param>
    public CtPointClient(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL cannot be null or empty.", nameof(baseUrl));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    public async Task<string> PingAsync()
    {
        var response = await _httpClient.GetAsync("/ping");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<Dictionary<string, string>> InitiateAsync(CtFile file)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));

        var response = await _httpClient.PostAsJsonAsync("/initiate", file);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(jsonResponse)
               ?? new Dictionary<string, string>();
    }

    public async Task<string> DownloadAsync(CtPartRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var response = await _httpClient.PostAsJsonAsync("/download", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> CheckAsync(CtPartHashRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var response = await _httpClient.PostAsJsonAsync("/check", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}