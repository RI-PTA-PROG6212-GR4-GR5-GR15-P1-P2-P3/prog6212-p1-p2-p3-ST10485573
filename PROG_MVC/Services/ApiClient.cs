// Original source: Microsoft Learn - "Make HTTP requests using IHttpClientFactory in ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests
// Accessed: 14 October 2026
// Adapted for: RaceDay MVC API client implementation with session token handling.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;

namespace PROG_MVC.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string TokenSessionKey = "AuthToken";

    public ApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Token
    {
        get => _httpContextAccessor.HttpContext?.Session.GetString(TokenSessionKey);
    }

    public void SetToken(string? token)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null) return;
        if (string.IsNullOrWhiteSpace(token))
        {
            ctx.Session.Remove(TokenSessionKey);
        }
        else
        {
            ctx.Session.SetString(TokenSessionKey, token);
        }
    }

    private void ApplyToken()
    {
        var token = Token;
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        ApplyToken();
        try
        {
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return default;
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch
        {
            return default;
        }
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string url, T body)
    {
        ApplyToken();
        return await _http.PostAsJsonAsync(url, body);
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string url, T body)
    {
        ApplyToken();
        return await _http.PutAsJsonAsync(url, body);
    }

    public async Task<HttpResponseMessage> DeleteAsync(string url)
    {
        ApplyToken();
        return await _http.DeleteAsync(url);
    }

    public async Task<HttpResponseMessage> PostFileAsync(string url, IFormFile file, string fieldName = "file")
    {
        ApplyToken();
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream();
        var fileContent = new StreamContent(stream);
        if (MediaTypeHeaderValue.TryParse(file.ContentType, out var mediaType))
        {
            fileContent.Headers.ContentType = mediaType;
        }
        content.Add(fileContent, fieldName, Path.GetFileName(file.FileName));
        return await _http.PostAsync(url, content);
    }
}
