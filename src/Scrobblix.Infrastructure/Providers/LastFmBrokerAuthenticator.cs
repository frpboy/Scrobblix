using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scrobblix.Application.Abstractions;

namespace Scrobblix.Infrastructure.Providers;

/** Browser consent only. No developer credential or listener password enters this client. */
public sealed class LastFmBrokerAuthenticator(HttpClient client, Uri endpoint, ICredentialStore credentials) : ILastFmAuthenticator
{
    private static readonly Regex Hex = new("^[a-fA-F0-9]{32}$", RegexOptions.CultureInvariant);
    private static readonly Regex RequestToken = new(@"\A[A-Za-z0-9_-]{32}\z", RegexOptions.CultureInvariant);
    public async Task<LastFmAuthorization> BeginAsync(CancellationToken cancellationToken)
    {
        using var result=await PostAsync(new { method="auth.getToken" },cancellationToken);
        var root=result.RootElement;
        var token=root.GetProperty("token").GetString();
        var url=root.GetProperty("authorizationUrl").GetString();
        if(token is null || !RequestToken.IsMatch(token) || !Uri.TryCreate(url,UriKind.Absolute,out var uri) ||
            uri.Scheme!="https" || uri.Host!="www.last.fm" || !uri.IsDefaultPort || uri.AbsolutePath!="/api/auth/" ||
            uri.UserInfo.Length!=0 || uri.Fragment.Length!=0)
            throw new InvalidOperationException("Last.fm authorization response was invalid.");
        var fields=uri.Query.TrimStart('?').Split('&').Select(x=>x.Split('=',2)).ToArray();
        if(fields.Length!=2 || fields.Any(x=>x.Length!=2) ||
            fields.Count(x=>x[0]=="token" && Uri.UnescapeDataString(x[1])==token)!=1 ||
            fields.Count(x=>x[0]=="api_key" && Hex.IsMatch(Uri.UnescapeDataString(x[1])))!=1)
            throw new InvalidOperationException("Last.fm authorization URL was invalid.");
        return new LastFmAuthorization(token,uri);
    }
    public async Task<LastFmSession> CompleteAsync(string token,CancellationToken cancellationToken)
    {
        if(!RequestToken.IsMatch(token)) throw new ArgumentException("Invalid authorization token.",nameof(token));
        using var result=await PostAsync(new { method="auth.getSession",token },cancellationToken);
        var session=result.RootElement.GetProperty("session");
        var name=session.GetProperty("name").GetString();var key=session.GetProperty("key").GetString();
        if(string.IsNullOrWhiteSpace(name) || name.Length>255 || name.Any(char.IsControl) || key is null || !RequestToken.IsMatch(key))
            throw new InvalidOperationException("Last.fm session response was invalid.");
        await credentials.StoreAsync(LastFmDesktopAuthenticator.CredentialResource,name,key,cancellationToken);
        return new LastFmSession(name);
    }
    private async Task<JsonDocument> PostAsync<T>(T payload,CancellationToken token)
    {
        LastFmBrokerProtocol.ValidateEndpoint(endpoint);
        using var request=new HttpRequestMessage(HttpMethod.Post,endpoint) { Content=JsonContent.Create(payload) };
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
        response.EnsureSuccessStatusCode();
        await using var stream=await response.Content.ReadAsStreamAsync(token);
        using var buffer=new MemoryStream();var chunk=new byte[4096];
        while(true) {
            var read=await stream.ReadAsync(chunk,token);if(read==0)break;
            if(buffer.Length+read>65536)throw new InvalidOperationException("Last.fm response exceeded the size limit.");
            buffer.Write(chunk,0,read);
        }
        return JsonDocument.Parse(buffer.ToArray());
    }
}
