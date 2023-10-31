using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PlayFab.MultiplayerModels;

namespace Plato.Function
{
     public class TitleAuthenticationContext{
        public string Id {get; set;}
        public string EntityToken{get; set;}
    }

    public class FunctionExecutionContext<T>
    {
        public PlayFab.ProfilesModels.EntityProfileBody CallerEntityProfile {get; set;}
        public TitleAuthenticationContext TitleAuthenticationContext {get; set;}
        public bool? GeneratePlayStreamEvent {get; set;}
        public T FunctionArgument {get; set;}
    }

    public static class AllocateServer
    {
        
        [FunctionName("AllocateServer")]
        public static async Task<dynamic> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            FunctionExecutionContext<dynamic> context = JsonConvert.DeserializeObject<FunctionExecutionContext<dynamic>>(await req.ReadAsStringAsync());

            var apiSettings = new PlayFab.PlayFabApiSettings()
            {
                TitleId = context.TitleAuthenticationContext.Id,
                DeveloperSecretKey = "PLAYFAB_DEVELOPER_SECRET_KEY_HERE"
            };

            PlayFab.PlayFabAuthenticationContext titleContext = new PlayFab.PlayFabAuthenticationContext
            {
                EntityToken = context.TitleAuthenticationContext.EntityToken
            };
            
            var multiplayerAPI = new PlayFab.PlayFabMultiplayerInstanceAPI(apiSettings,titleContext);
            string matchId = context.FunctionArgument.MatchId;
            string queueName = context.FunctionArgument.QueueName;

            var listServerResponse = ListServers();
            bool isServerAvailable = false;

            foreach(var server in listServerResponse.Result.servers)
            {
                var serverArguments = JsonConvert.DeserializeObject<ServerArguments>(server.server_arguments);
                
                if(serverArguments.matchId == matchId)
                {
                    isServerAvailable = true;

                    if(server.status == "launching")
                    {
                        return new OkObjectResult(new TicketResult{Status = "Launching"});
                    }
                    else if(server.status == "running")
                    {
                        return new OkObjectResult(new TicketResult{Status = "Running", ServerURL = server.server_url, Port = server.ports._443, MatchId = server.match_id});
                    }
                    else
                    {
                        return new OkObjectResult(new TicketResult{Status = server.status});
                    }
                }
            }
            
            if(isServerAvailable == false)
            {
                GetMatchRequest getMatchRequest = new GetMatchRequest{MatchId = matchId, QueueName = queueName};
                var getMatchResult = await multiplayerAPI.GetMatchAsync(getMatchRequest);

                if(getMatchResult.Result.Members[0].Entity.Id == context.CallerEntityProfile.Entity.Id)
                {                
                    var startGameServerResponse = await StartGameServer(new ServerArguments{matchId = matchId, queueName = queueName});
                    return new OkObjectResult(new TicketResult{Status = "Started Server"});
                }
                else
                {
                    return new OkObjectResult(new TicketResult{Status = "Allocating"});
                }
            }

            return new OkObjectResult(new TicketResult{Status = "Waiting"});
        }

        
        public static async Task<StartGameServer> StartGameServer(ServerArguments serverArguments)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("token", "PLAYFLOW_API_KEY");
                client.DefaultRequestHeaders.Add("arguments", JsonConvert.SerializeObject(serverArguments));
                client.DefaultRequestHeaders.Add("region", "sea");
                client.DefaultRequestHeaders.Add("type", "small");

                
                var response = await client.PostAsync(String.Format("https://api.cloud.playflow.app/start_game_server"), null);
                var result = await response.Content.ReadAsStringAsync();
                var jsonResult = JsonConvert.DeserializeObject<StartGameServer>(result);
                return jsonResult;
            }
        }

        public static async Task<ListServers> ListServers()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("token", "PLAYFLOW_API_KEY");
                client.DefaultRequestHeaders.Add("include-launching", "true");

                var response = await client.PostAsync(String.Format("https://api.cloud.playflow.app/list_servers"), null);
                var result = await response.Content.ReadAsStringAsync();
                var jsonResult = JsonConvert.DeserializeObject<ListServers>(result);

                return jsonResult;
            }
        }
    }
    
public class ServerArguments
{
    public string matchId { get; set; }
    public string queueName { get; set; }
}    

public class TicketResult
{
    public string Status;
    public string ServerURL;
    public int Port;
    public string MatchId;
}

 public class StartGameServer
    {
        public string match_id { get; set; }
        public string ip { get; set; }
        public string region { get; set; }
        public string status { get; set; }
        public int version { get; set; }
        public bool capacity { get; set; }
    }

public class Ports
{
    [Newtonsoft.Json.JsonProperty("7778")]
    public int _7778 { get; set; }

    [Newtonsoft.Json.JsonProperty("443")]
    public int _443 { get; set; }
}

public class ListServers
{
    public int total_servers { get; set; }
    public List<Server> servers { get; set; }
}

public class Server
{
    public string match_id { get; set; }
    public string status { get; set; }
    public string region { get; set; }
    public string instance_type { get; set; }
    public bool ssl_enabled { get; set; }
    public string ip { get; set; }
    public DateTime start_time { get; set; }
    public Ports ports { get; set; }
    public string server_arguments { get; set; }
    public string server_url { get; set; }
    public int playflow_api_version { get; set; }
    public string server_tag { get; set; }
}
}
