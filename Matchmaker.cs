using PlayFab;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PlayFab.MultiplayerModels;
using System;
using Mirror;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using EntityKey = PlayFab.MultiplayerModels.EntityKey;
using PlayFab.Json;
using Mirror.SimpleWeb;
using Newtonsoft.Json;

public class Matchmaker : MonoBehaviour
{

    public m_NetworkManager networkManager;
    public SimpleWebTransport webTransport;

    [SerializeField] private TMP_Text queueStatusText;

    public string queueName = "Plato";

    private string ticketID;
    private string matchID;

    private Coroutine pollTicketCoroutine;
    private Coroutine getMatchCoroutine;


    public void StartMatchmaking()
    {
        queueStatusText.text = "Submitting Ticket";

        PlayFabMultiplayerAPI.CreateMatchmakingTicket(
            new CreateMatchmakingTicketRequest
            {
                Creator = new MatchmakingPlayer
                {
                    Entity = new EntityKey
                    {
                        Id = m_UserData.entityID,
                        Type = "title_player_account"
                    },
                    Attributes = new MatchmakingPlayerAttributes
                    {
                        DataObject = new
                        {
                            Latency = new object[]
                            {
                                new
                                {
                                    region = "NorthEurope",
                                    latency = 10
                                }
                            }
                        }
                    }
                },
                GiveUpAfterSeconds = 120,
                QueueName = queueName
            }, OnMatchmakingTicketCreated, OnMatchmakingError
            );
    }

    private void OnMatchmakingTicketCreated(CreateMatchmakingTicketResult result)
    {
        ticketID = result.TicketId;

        pollTicketCoroutine = StartCoroutine(PollTicket());
        queueStatusText.text = "Ticket Created";
    }

    private void OnMatchmakingError(PlayFabError error)
    {
        Debug.Log(error.ErrorMessage);

        if (pollTicketCoroutine != null) StopCoroutine(pollTicketCoroutine);
        if (getMatchCoroutine != null) StopCoroutine(getMatchCoroutine);
    }

    private IEnumerator PollTicket()
    {
        while (true)
        {
            PlayFabMultiplayerAPI.GetMatchmakingTicket(new GetMatchmakingTicketRequest { TicketId = ticketID, QueueName = queueName },
                result =>
                {
                    switch (result.Status)
                    {
                        case "Matched":
                            queueStatusText.text = "Status: Opponent Found";
                            StopCoroutine(pollTicketCoroutine);
                            queueStatusText.text = "Status: Waiting For Server";
                            matchID = result.MatchId;
                            getMatchCoroutine = StartCoroutine(GetMatch());
                            break;
                        case "Canceled":
                            queueStatusText.text = "Status: Match Canceled";
                            StopCoroutine(pollTicketCoroutine);
                            break;
                        default:
                            queueStatusText.text = "Status: " + result.Status;
                            break;
                    }
                }, OnMatchmakingError);

            yield return new WaitForSeconds(6);
        }
    }

    private IEnumerator GetMatch()
    {
        while (true)
        {
            PlayFabCloudScriptAPI.ExecuteFunction(new ExecuteFunctionRequest()
            {
                FunctionName = "AllocateServer",
                FunctionParameter = new { MatchId = matchID, QueueName = queueName },
                GeneratePlayStreamEvent = true,
            }, success =>
            {
                Debug.Log(success.FunctionResult);
                GetMatch result = JsonConvert.DeserializeObject<GetMatch>(success.FunctionResult.ToString());

                switch (result.Status)
                {
                    case "Allocating":
                        queueStatusText.text = "Status: Allocating Server";
                        break;
                    case "Launching":
                        queueStatusText.text = "Status: Launching Server";
                        break;
                    case "Running":
                        StopCoroutine(getMatchCoroutine);
                        queueStatusText.text = "Status: Joining Server";
                        networkManager.networkAddress = result.ServerURL;
                        webTransport.port = (ushort)result.Port;

                        networkManager.StartClient();
                        queueStatusText.text = "Waiting For Other Players To Join...";
                        break;
                    default:
                        queueStatusText.text = "Status: " + result.Status;
                        break;
                }

            }, OnMatchmakingError);

            yield return new WaitForSeconds(10);
        }
    }

    public void StopMatckmaking()
    {
        PlayFabMultiplayerAPI.CancelMatchmakingTicket
            (
            new CancelMatchmakingTicketRequest
            {
                TicketId = ticketID,
                QueueName = queueName
            },
            result =>
            {
              queueStatusText.text = "Status: Matchmaking Stopped";
            }, OnMatchmakingError);
    }

}


public class GetMatch
{
    public string Status;
    public string ServerURL;
    public int Port;
    public string MatchId;
}
