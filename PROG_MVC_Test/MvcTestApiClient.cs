extern alias progMvc;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using PROG_API.DTOs;
using MvcServices = progMvc::PROG_MVC.Services;

namespace PROG_MVC_Test;

internal sealed class MvcTestApiClient : MvcServices.IApiClient
{
    public string? Token { get; private set; }
    public Func<string, object?> GetResponse { get; set; } = _ => null;
    public Func<string, object?, HttpResponseMessage> PostResponse { get; set; } = (_, _) => Response(HttpStatusCode.OK);
    public List<(string Method, string Url, object? Body)> Requests { get; } = new();

    public void SetToken(string? token) => Token = token;

    public Task<T?> GetAsync<T>(string url)
    {
        Requests.Add(("GET", url, null));
        return Task.FromResult(GetResponse(url) is T value ? value : default);
    }

    public Task<HttpResponseMessage> PostAsync<T>(string url, T body)
    {
        Requests.Add(("POST", url, body));
        return Task.FromResult(PostResponse(url, body));
    }

    public Task<HttpResponseMessage> PutAsync<T>(string url, T body)
    {
        Requests.Add(("PUT", url, body));
        return Task.FromResult(Response(HttpStatusCode.OK));
    }

    public Task<HttpResponseMessage> DeleteAsync(string url)
    {
        Requests.Add(("DELETE", url, null));
        return Task.FromResult(Response(HttpStatusCode.OK));
    }

    public Task<HttpResponseMessage> PostFileAsync(string url, IFormFile file, string fieldName = "file")
    {
        Requests.Add(("POSTFILE", url, file));
        return Task.FromResult(Response(HttpStatusCode.OK));
    }

    public static HttpResponseMessage Response(HttpStatusCode status, object? body = null)
    {
        var response = new HttpResponseMessage(status);
        if (body is not null) response.Content = JsonContent.Create(body);
        return response;
    }

    public static AuthResponseDto Auth(string role) => new()
    {
        Token = "test-token",
        User = new UserDto { Id = 42, FullName = "Test User", Role = role }
    };
}
