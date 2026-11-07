using System.Net;
using System.Text;
using System.Text.Json;
using Scrobblix.Infrastructure.Providers;
using Scrobblix.Application.Abstractions;
using Scrobblix.Application.Providers;

Check(LastFmConnectionDefaults.ResolveBrokerEndpoint(null)=="https://scrobblix-lastfm-broker.frpboy12.workers.dev/v1/lastfm","normal-user endpoint pinned without developer keys");
Check(LastFmConnectionDefaults.ResolveBrokerEndpoint("")=="","explicit invalid override never falls back silently");
Check(LastFmConnectionDefaults.ResolveBrokerEndpoint("https://broker.invalid/v1/lastfm")=="https://broker.invalid/v1/lastfm","qualification override remains explicit");
_ = new LastFmScrobbleProvider(new HttpClient(new Capture()),new Uri(LastFmConnectionDefaults.BrokerEndpoint));
var capture=new Capture();using var client=new HttpClient(capture);
var endpoint=new Uri("https://broker.invalid/v1/lastfm");var credentials=new Credentials();
var auth=new LastFmBrokerAuthenticator(client,endpoint,credentials);
var begun=await auth.BeginAsync(default);
Check(begun.AuthorizationUri.Host=="www.last.fm","browser destination fixed Last.fm");
await auth.CompleteAsync(begun.Token,default);
Check(credentials.User=="SyntheticUser" && credentials.Key==new string('b',32),"session safely returned to credential boundary");
var provider=new LastFmScrobbleProvider(client,endpoint);
// Synthetic response ambiguity remains unknown; no credentials or real network.
var submission=new ScrobbleSubmission("Synthetic Artist","Synthetic Track",DateTimeOffset.FromUnixTimeSeconds(1700000000),new string('b',32),null,60);
var unknown=await provider.SubmitScrobbleAsync(submission,default);
Check(!unknown.Accepted && !unknown.IsTransient,"malformed success remains held, never accepted or retried");
await provider.LoveTrackAsync(submission,default);
await provider.UnloveTrackAsync(submission,default);
await provider.UpdateNowPlayingAsync(submission,default);
string[] expectedMethods=["auth.getToken","auth.getSession","track.scrobble","track.love","track.unlove","track.updateNowPlaying"];
Check(capture.Methods.SequenceEqual(expectedMethods),"scoped methods routed through broker");
foreach(var uri in new[]{"http://broker.invalid/v1/lastfm","https://broker.invalid/other","https://user@broker.invalid/v1/lastfm","https://broker.invalid/v1/lastfm?url=bad"}) {
 var rejected=false;try{_=new LastFmScrobbleProvider(client,new Uri(uri));}catch(ArgumentException){rejected=true;}Check(rejected,"unsafe broker endpoint rejected");
}
capture.BadUrl=true;var bad=false;try{await auth.BeginAsync(default);}catch(InvalidOperationException){bad=true;}Check(bad,"untrusted browser destination rejected");
foreach(var (status,body,transient,reason) in new[] {
 (429,"{\"broker_error\":\"limited\"}",true,"provider.rate_limited"),
 (503,"{\"broker_error\":\"not_configured\"}",true,"provider.temporary_unavailable"),
 (503,"{\"broker_error\":\"limiter_unavailable\"}",true,"provider.temporary_unavailable"),
 (400,"{\"broker_error\":\"invalid_request\"}",false,"provider.response_invalid"),
 (502,"{\"broker_error\":\"upstream_outcome_unknown\"}",false,"provider.submission_outcome_unknown"),
 (200,"{\"broker_error\":\"limited\"}",false,"provider.submission_outcome_unknown"),
 (429,"{\"broker_error\":\"limited\",\"scrobbles\":{}}",false,"provider.submission_outcome_unknown")
}) {
 capture.OverrideStatus=status;capture.OverrideBody=body;
 var outcome=await provider.SubmitScrobbleAsync(submission,default);
 Check(!outcome.Accepted && outcome.IsTransient==transient && outcome.ReasonCode==reason,"broker pre-forward rejection versus upstream ambiguity");
}
capture.OverrideStatus=200;capture.OverrideBody=null;capture.BadUrl=false;
foreach(var requestToken in new[]{string.Concat(Enumerable.Repeat("Zg_-",8)),new string('_',32),new string('-',32)}) {
 capture.RequestToken=requestToken;
 var authorization=await auth.BeginAsync(default);
 Check(authorization.Token==requestToken,"URL-safe opaque token retained");
 await auth.CompleteAsync(authorization.Token,default);
 Check(capture.SessionToken==requestToken,"session request retains exact token");
}
foreach(var requestToken in new[]{new string('x',31),new string('x',33),new string('x',31)+"&",new string('x',31)+" ",new string('x',31)+"é",new string('x',32)+"\n"}) {
 capture.RequestToken=requestToken;var rejected=false;
 try{await auth.BeginAsync(default);}catch(InvalidOperationException){rejected=true;}Check(rejected,"unsafe response token rejected");
 var before=capture.Methods.Count;rejected=false;
 try{await auth.CompleteAsync(requestToken,default);}catch(ArgumentException){rejected=true;}Check(rejected && capture.Methods.Count==before,"unsafe token rejected before session request");
}
capture.RequestToken=string.Concat(Enumerable.Repeat("Zg_-",8));capture.PublicKey=capture.RequestToken;
var keyRejected=false;try{await auth.BeginAsync(default);}catch(InvalidOperationException){keyRejected=true;}Check(keyRejected,"API key remains hex-only");
capture.PublicKey=new string('c',32);capture.SessionKey=capture.RequestToken;credentials.Key=null;
keyRejected=false;try{await auth.CompleteAsync(capture.RequestToken,default);}catch(InvalidOperationException){keyRejected=true;}Check(keyRejected && credentials.Key is null,"nonhex session key never stored");
Console.WriteLine("PASS synthetic broker client: no app secret/signature/password, fixed endpoint/browser host, scoped methods and credential boundary");
static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
sealed class Capture:HttpMessageHandler {
 private static readonly string[] Forbidden=["api_key","api_sig","api_secret","password","url"];
 public List<string> Methods {get;}=[];public bool BadUrl;public int OverrideStatus=200;public string? OverrideBody;public string RequestToken=new('a',32);public string PublicKey=new('c',32);public string SessionKey=new('b',32);public string? SessionToken;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) {
  if(request.RequestUri!.AbsoluteUri!="https://broker.invalid/v1/lastfm" || request.Method!=HttpMethod.Post)throw new InvalidOperationException("wrong destination");
  using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
  foreach(var field in Forbidden)if(json.RootElement.TryGetProperty(field,out _))throw new InvalidOperationException("disallowed credential/field");
  var method=json.RootElement.GetProperty("method").GetString()!;Methods.Add(method);
  if(method=="track.scrobble" && json.RootElement.GetProperty("timestamp").ValueKind!=JsonValueKind.Number)throw new InvalidOperationException("timestamp must be numeric");
  if(method=="auth.getSession")SessionToken=json.RootElement.GetProperty("token").GetString();
  var body=method switch {
   "auth.getToken"=>JsonSerializer.Serialize(new{token=RequestToken,authorizationUrl=BadUrl?"https://invalid.example/":"https://www.last.fm/api/auth/?api_key="+PublicKey+"&token="+RequestToken}),
   "auth.getSession"=>JsonSerializer.Serialize(new{session=new{name="SyntheticUser",key=SessionKey}}),
   _=>"{}"
  };
  return new HttpResponseMessage((HttpStatusCode)OverrideStatus){Content=new StringContent(OverrideBody ?? body,Encoding.UTF8,"application/json")};
 }
}
sealed class Credentials:ICredentialStore {
 public string? User;public string? Key;
 public Task StoreAsync(string resource,string userName,string secret,CancellationToken token){User=userName;Key=secret;return Task.CompletedTask;}
 public Task<string?> GetAsync(string resource,string userName,CancellationToken token)=>Task.FromResult(Key);
 public Task DeleteAsync(string resource,string userName,CancellationToken token)=>Task.CompletedTask;
}
