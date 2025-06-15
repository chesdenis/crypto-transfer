using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ct.lib.model;
using ct.lib.services;

namespace ct.client;

public class CtPointClient
{
    private readonly ICtCryptoService _cryptoService;
    private readonly string _encryptionKey;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the CtPointClient class with the base server URL.
    /// </summary>
    /// <param name="baseUrl">The base URL of the CtPoint server (e.g., http://localhost:8080).</param>
    /// <param name="cryptoService"></param>
    /// <param name="encryptionKey"></param>
    public CtPointClient(string baseUrl, ICtCryptoService cryptoService, string encryptionKey)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL cannot be null or empty.", nameof(baseUrl));
        _cryptoService = cryptoService;
        _encryptionKey = encryptionKey;
        
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromHours(1),
            MaxResponseContentBufferSize = int.MaxValue
        };
    }

    public async Task<string> PingAsync()
    {
        var response = await _httpClient.GetAsync("/ping");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<CtFileMap> InitiateAsync(CtFile file)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));

        var encryptedBody = await EncryptAsync(file, _cryptoService, _encryptionKey);
        var response = await _httpClient.PostAsJsonAsync("/initiate", encryptedBody);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CtFileMap>(jsonResponse) ?? throw new InvalidOperationException();
    }

    public async Task<string> DownloadAsync(CtPartRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        
        var encryptedBody = await EncryptAsync(request, _cryptoService, _encryptionKey);

        var response = await _httpClient.PostAsJsonAsync("/download", encryptedBody);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<string> EncryptAsync<T>(T data, ICtCryptoService cryptoService, string encryptionKey) where T: class
    {
        var serialized =  JsonSerializer.Serialize(data);
        var serializedBytes = Encoding.UTF8.GetBytes(serialized);
        var encrypted =  await cryptoService.EncryptBytesAsync(serializedBytes, encryptionKey);
        var request = Convert.ToBase64String(encrypted);
        return request;
    }
}