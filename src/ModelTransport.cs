using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AkilliKisayol {
 internal sealed class ModelTransport:IDisposable {
  sealed class Connection:IDisposable {public HttpClient Client;public bool NoFormat,NoSchema,CompletionTokens,NoTemperature;public void Dispose(){Client.Dispose();}}
  readonly Dictionary<string,Connection> connections=new Dictionary<string,Connection>();readonly object sync=new object();
  Connection Get(ModelOptions options){var uri=options.BaseUri();string key=options.ApiKey,identity;using(var hash=SHA256.Create())identity=uri.AbsoluteUri+"|"+options.Model+"|"+Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(key)));lock(sync){Connection found;if(connections.TryGetValue(identity,out found))return found;if(connections.Count>=8){foreach(var old in connections.Values)old.Dispose();connections.Clear();}found=new Connection{Client=new HttpClient(new HttpClientHandler{UseProxy=!uri.IsLoopback,AllowAutoRedirect=false}){BaseAddress=uri,Timeout=TimeSpan.FromMinutes(10)}};if(key.Length>0)found.Client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",key);connections.Add(identity,found);return found;}}
  public async Task<string> Complete(ModelOptions options,Dictionary<string,object> original,Action<string> progress,CancellationToken cancellation){var connection=Get(options);for(int attempt=0;attempt<5;attempt++){using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation)){deadline.CancelAfter(TimeSpan.FromMinutes(10));var payload=new Dictionary<string,object>(original);lock(connection){if(connection.NoFormat)payload.Remove("response_format");else if(connection.NoSchema&&payload.ContainsKey("response_format"))payload["response_format"]=new{type="json_object"};if(connection.NoTemperature)payload.Remove("temperature");object count;if(connection.CompletionTokens&&payload.TryGetValue("max_tokens",out count)){payload.Remove("max_tokens");payload["max_completion_tokens"]=count;}}
    using(var message=new HttpRequestMessage(HttpMethod.Post,"chat/completions")){message.Content=new StringContent(LocalLlm.Json().Serialize(payload),Encoding.UTF8,"application/json");using(var response=await connection.Client.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,deadline.Token)){string body=await ReadBody(response.Content,deadline.Token);if(response.IsSuccessStatusCode)return ModelWire.Completion(body);if((int)response.StatusCode==400||(int)response.StatusCode==422){string error=body.ToLowerInvariant();bool unsupported=error.Contains("unsupported")||error.Contains("not support")||error.Contains("not permitted")||error.Contains("unknown")||error.Contains("unrecognized")||error.Contains("extra inputs")||error.Contains("only the default")||error.Contains("not allowed");bool changed=false;lock(connection){if(unsupported&&(error.Contains("response_format")||error.Contains("json_schema"))&&!connection.NoFormat){if(error.Contains("json_schema")&&!connection.NoSchema)connection.NoSchema=true;else connection.NoFormat=true;changed=true;}if(error.Contains("max_completion_tokens")&&error.Contains("max_tokens")&&!connection.CompletionTokens){connection.CompletionTokens=true;changed=true;}if(unsupported&&error.Contains("temperature")&&!connection.NoTemperature){connection.NoTemperature=true;changed=true;}}if(changed){progress("Adapting request parameters to this model server…");continue;}}
      throw new InvalidOperationException("Model server returned HTTP "+(int)response.StatusCode+". Check the base URL, model ID, authentication and model capabilities.");
    }}
   }}throw new InvalidOperationException("The model server rejected the negotiated request parameters.");}
  internal static async Task<string> ReadBody(HttpContent content,CancellationToken cancellation){if(content.Headers.ContentLength.HasValue&&content.Headers.ContentLength.Value>131072)throw new InvalidDataException("Model response exceeds 128 KB.");using(var source=await content.ReadAsStreamAsync())using(cancellation.Register(()=>source.Dispose()))using(var result=new MemoryStream()){var buffer=new byte[8192];int count;try{while((count=await source.ReadAsync(buffer,0,buffer.Length,cancellation))>0){if(result.Length+count>131072)throw new InvalidDataException("Model response exceeds 128 KB.");result.Write(buffer,0,count);}cancellation.ThrowIfCancellationRequested();return Encoding.UTF8.GetString(result.ToArray());}catch(Exception){cancellation.ThrowIfCancellationRequested();throw;}}}
  public void Dispose(){lock(sync){foreach(var connection in connections.Values)connection.Dispose();connections.Clear();}}
 }
}
