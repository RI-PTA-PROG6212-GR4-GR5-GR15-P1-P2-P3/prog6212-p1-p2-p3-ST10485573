// Original source: Microsoft Learn - "Make HTTP requests using IHttpClientFactory in ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests
// Accessed: 14 October 2026
// Adapted for: RaceDay MVC API client interface.
using System.Net.Http.Json;
using PROG_API.DTOs;

namespace PROG_MVC.Services;

public interface IApiClient
{
    Task<T?> GetAsync<T>(string url);
    Task<HttpResponseMessage> PostAsync<T>(string url, T body);
    Task<HttpResponseMessage> PutAsync<T>(string url, T body);
    Task<HttpResponseMessage> DeleteAsync(string url);
    Task<HttpResponseMessage> PostFileAsync(string url, IFormFile file, string fieldName = "file");
    void SetToken(string? token);
    string? Token { get; }
}