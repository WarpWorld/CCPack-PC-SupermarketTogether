using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;
using Newtonsoft.Json;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.AI;
using System.IO;
using System.Net.Sockets;
using System.Linq;
using UnityEngine.Localization.Pseudo;
using UnityEngine.Playables;
using Newtonsoft.Json.Linq;
using static UnityEngine.InputSystem.InputRemoting;

namespace BepinControl
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class TestMod : BaseUnityPlugin
    {
        private const string modGUID = "WarpWorld.CrowdControl";
        private const string modName = "Crowd Control";
        private const string modVersion = "1.0.12.0";

        private readonly Harmony harmony = new Harmony(modGUID);
        public static ManualLogSource mls;
        internal static TestMod Instance = null;

        private ControlClient client = null;
        public static bool isFocused = true;

        private HashSet<string> pendingMessageIDs = new HashSet<string>();
        public static bool validVersion = false;
        public static bool versionResponse = false;
        public static bool isHost = false;
        public static bool ranVersionCheck = false;

        private const string MESSAGE_TAG = "</b>";

        public static Dictionary<string, string> customerDictionary = new Dictionary<string, string>();


        // START TWITCH STUFF
        public static bool isIrcConnected = false;
        private static bool isChatConnected = false;
        private static bool isTwitchChatAllowed = true;
        private const string twitchServer = "irc.chat.twitch.tv";
        private const int twitchPort = 6667;
        public static string twitchUsername = "justinfan1337";
        public static string twitchOauth = "";
        public static string twitchChannel = "";
        private static TcpClient twitchTcpClient;
        private static NetworkStream twitchStream;
        private static StreamReader twitchReader;
        private static StreamWriter twitchWriter;

        private static List<string> allowedUsernames = new() { "jaku", "s4turn", "crowdcontrol", "theunknowncod3r" };
        private static List<string> twitchChannels = new();

        private static readonly ConcurrentDictionary<int, Action<CrowdResponse.Status>> rspResponders = new();

        public static void AddResponder(int msgID, Action<CrowdResponse.Status> responder) => rspResponders[msgID] = responder;
        public static void RemoveResponder(int msgID) => rspResponders.TryRemove(msgID, out _);

        public static void ConnectToTwitchChat()
        {
            if (!isChatConnected && twitchChannel.Length >= 1)
            {
                new Thread(new ThreadStart(StartTwitchChatListener)).Start();
                isChatConnected = true;
            }
        }


        public static void StartTwitchChatListener()
        {

            // Twitch oauth ini support incase we want to connect with an authenicated user for something
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "twitchauth.ini");
            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath);

                foreach (string line in lines)
                {
                    if (line.StartsWith("username=", StringComparison.OrdinalIgnoreCase))
                    {
                        twitchUsername = line.Split('=')[1].Trim();
                    }
                    else if (line.StartsWith("oauth=", StringComparison.OrdinalIgnoreCase))
                    {
                        twitchOauth = line.Split('=')[1].Trim();
                    }
                }

                if (string.IsNullOrEmpty(twitchUsername) || string.IsNullOrEmpty(twitchOauth))
                {
                    twitchUsername = "justinfan1337";
                    twitchOauth = "";
                }
            }
            

            try
            {
                twitchTcpClient = new TcpClient(twitchServer, twitchPort);
                twitchStream = twitchTcpClient.GetStream();
                twitchReader = new StreamReader(twitchStream);
                twitchWriter = new StreamWriter(twitchStream);

                // Request membership and tags capabilities from Twitch
                twitchWriter.WriteLine("CAP REQ :twitch.tv/membership twitch.tv/tags twitch.tv/commands");

                if (twitchOauth.Length > 0) twitchWriter.WriteLine($"PASS oauth:{twitchOauth}");

                twitchWriter.WriteLine($"NICK {twitchUsername}");
                twitchWriter.WriteLine($"JOIN #{twitchChannel}");
                twitchWriter.Flush();

                mls.LogInfo($"Connected to Twitch channel: {twitchChannel}");

                twitchWriter.Flush();
                while (true)
                {
                    if (twitchStream.DataAvailable)
                    {
                        var message = twitchReader.ReadLine();
                        if (message != null)
                        {

                            if (message.StartsWith("PING"))
                            {
                                twitchWriter.WriteLine("PONG :tmi.twitch.tv");
                                twitchWriter.Flush();
                            }
                            else if (message.Contains("PRIVMSG"))
                            {
                                var messageParts = message.Split(new[] { ' ' }, 4);
                                if (messageParts.Length >= 4)
                                {
                                    var rawUsername = messageParts[1];
                                    string username = rawUsername.Substring(1, rawUsername.IndexOf('!') - 1);
                                    int messageStartIndex = message.IndexOf("PRIVMSG");
                                    if (messageStartIndex >= 0)
                                    {
                                        string chatMessage = messageParts[3].Substring(1);
                                        string[] chatParts = chatMessage.Split(new[] { " :" }, 2, StringSplitOptions.None);
                                        chatMessage = chatParts[1];

                                        var badges = ParseBadges(messageParts[0]);


                                        string badgeDisplay = "";
                                        if (badges.Contains("broadcaster"))
                                        {
                                            badgeDisplay = "[BROADCASTER]";
                                        }
                                        else if (badges.Contains("moderator"))
                                        {
                                            badgeDisplay = "[MODERATOR]";
                                        }
                                        else if (badges.Contains("vip"))
                                        {
                                            badgeDisplay = "[VIP]";
                                        }
                                        else if (badges.Contains("subscriber"))
                                        {
                                            badgeDisplay = "[SUBSCRIBER]";
                                        }


                                        if (!string.IsNullOrEmpty(badgeDisplay) || allowedUsernames.Any(name => name.Equals(username, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            TestMod.ActionQueue.Enqueue(() =>
                                            {


                                                var spawnedObjects = isHost ? NetworkServer.spawned : NetworkClient.spawned;
                                                //List<NetworkIdentity> matchingIdentities = new List<NetworkIdentity>();

                                                foreach (var kvp in spawnedObjects)
                                                {
                                                    NetworkIdentity serverIdentity = kvp.Value;
                                                    if (serverIdentity.assetId != 620925214) continue;
                                                    if (serverIdentity.gameObject.name.ToLower() != username.ToLower()) continue;

                                                    NPC_Info npcInfo = serverIdentity.gameObject.GetComponentInChildren<NPC_Info>();
                                                    if (npcInfo != null) npcInfo.RPCNotificationAboveHead(chatMessage, "crowdcontrol");
                                                    
                                                }

                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    Thread.Sleep(50);
                }
            }
            catch (Exception e)
            {
                mls.LogInfo($"Twitch Chat Listener Error: {e.ToString()}");
            }
        }

        public static void DisconnectFromTwitch()
        {
            try
            {
                if (twitchWriter != null && twitchChannels.Count >= 1)
                {

                    foreach (string channel in twitchChannels)
                    {
                        twitchWriter.WriteLine("PART #" + channel);
                    }

                    twitchWriter.Flush();
                    twitchWriter.Close();
                    twitchChannels.Clear();
                }

                if (twitchReader != null)
                {
                    twitchReader.Close();
                }

                if (twitchStream != null)
                {
                    twitchStream.Close();
                }

                if (twitchTcpClient != null)
                {
                    twitchTcpClient.Close();
                }

                mls.LogInfo("Disconnected from Twitch chat.");
            }
            catch (Exception e)
            {
                mls.LogError($"Error disconnecting from Twitch: {e.Message}");
            }
        }


        public static HashSet<string> ParseBadges(string tagsPart)
        {
            var badgesSet = new HashSet<string>();
            var tags = tagsPart.Split(';');

            foreach (var tag in tags)
            {
                if (tag.StartsWith("badges="))
                {
                    var badges = tag.Substring("badges=".Length).Split(',');
                    foreach (var badge in badges)
                    {
                        var badgeType = badge.Split('/')[0];
                        badgesSet.Add(badgeType);
                    }
                }
            }

            return badgesSet;
        }





        // END TWITCH STUFF



        public static void ResetPackChecks()
        {
            validVersion = false;
            isHost = false;
            versionResponse = false;
            ranVersionCheck = false;
        }

        
        public static void AddOrUpdateCustomer(string customerNetID, string customerName)
        {
            if (customerName.Length >= 1)
            {
                if (customerDictionary.ContainsKey(customerNetID))
                {
                    customerDictionary[customerNetID] = customerName;
                }
                else
                {
                    customerDictionary.Add(customerNetID, customerName);
                }
            }
        }


        void Awake()
        {
            Instance = this;
            mls = BepInEx.Logging.Logger.CreateLogSource("Crowd Control");

            mls.LogInfo($"Loaded {modGUID}. Patching.");
            harmony.PatchAll(typeof(TestMod));
            harmony.PatchAll();

            mls.LogInfo($"Initializing Crowd Control");

            try
            {
                client = new ControlClient();
                new Thread(new ThreadStart(client.NetworkLoop)).Start();
                new Thread(new ThreadStart(client.RequestLoop)).Start();
            }
            catch (Exception e)
            {
                mls.LogInfo($"CC Init Error: {e.ToString()}");
            }

            mls.LogInfo($"Crowd Control Initialized");

        }


        public static void UpdateFranchisePoints(int requestID, string viewerName)
        {

            Instance.pendingMessageIDs.Add(requestID.ToString());

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var message = new
            {
                type = "CMD",
                command = "UPDATE_FP",
                tag = MESSAGE_TAG,
                requestID = requestID
            };

            string jsonMessage = JsonConvert.SerializeObject(message, settings);
            Instance.SendChatMessage(jsonMessage);
        }


        public static void SendSpawnCustomer(int requestID, string customerName, string _twitchChannel = null)
        {

            Instance.pendingMessageIDs.Add(requestID.ToString());

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var message = new
            {
                type = "CMD",
                command = "SPAWN_CUS",
                arg1 = customerName,
                arg2 = (_twitchChannel != null && _twitchChannel.Length >= 1) ? _twitchChannel : null,
                tag = MESSAGE_TAG,
                requestID = requestID
            };


            string jsonMessage = JsonConvert.SerializeObject(message, settings);
            Instance.SendChatMessage(jsonMessage);
        }


        public static void JailPlayer(int requestID, string playerName, string viewerName)
        {

            Instance.pendingMessageIDs.Add(requestID.ToString());

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var message = new
            {
                type = "CMD",
                command = "JAIL_PLAYER",
                arg1 = playerName,
                arg2 = viewerName,
                tag = MESSAGE_TAG,
                requestID = requestID
            };


            string jsonMessage = JsonConvert.SerializeObject(message, settings);
            Instance.SendChatMessage(jsonMessage);
        }


        private static void SendVersionCheck()
        {
            string messageID = Guid.NewGuid().ToString();
            Instance.pendingMessageIDs.Add(messageID);

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var versionMessage = new
            {
                type = "CMD",
                command = "VERSION",
                version = modVersion,
                messageID = messageID,
                tag = MESSAGE_TAG
            };

            string jsonMessage = JsonConvert.SerializeObject(versionMessage, settings);
            Instance.SendChatMessage(jsonMessage);
        }


        [HarmonyPatch(typeof(PlayerObjectController), "UserCode_RpcReceiveChatMsg__String__String")]
        public static class Patch_UserCode_RpcReceiveChatMsg
        {
            [HarmonyPrefix]
            static bool Prefix(ref string playerName, ref string message)
            {
                try
                {


                    if (string.IsNullOrEmpty(message))
                    {
                        return true;
                    }

        
                    if (message.Contains(MESSAGE_TAG))
                    {

                        // If the message contains </b> and is valid JSON it's for us!
                        bool containsJson = IsValidJson(message);
                        if (containsJson)
                        {
                            ProcessMessage(message, playerName);
                            return false;
                        }
                        
                        return true;
                    }

 
                }
                catch (Exception ex)
                {

                    return true; 
                }

                return true;
            }


           
        }

        private static void ProcessMessage(string message, string playerName)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                {
                    TestMod.mls.LogError($"Received null or empty message from {playerName}");
                    return;
                }

                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };

                var jsonMessage = JsonConvert.DeserializeObject<JsonMessage>(message, settings);

                if (jsonMessage == null || jsonMessage.type == null || jsonMessage.command == null || jsonMessage.tag == null)
                {
                    TestMod.mls.LogWarning($"Received malformed message from {playerName}: {message}");
                    return;
                }


                switch (jsonMessage.type)
                {
                    case "CMD":
                        ProcessCommand(jsonMessage, playerName);
                        break;
                    case "RSP":
                        ProcessResponse(jsonMessage, playerName);
                        break;
                    case "BST":
                        ProcessBroadcast(jsonMessage, playerName);
                        break;
                    default:
                        mls.LogWarning($"Unknown message type from {playerName}: {jsonMessage.type}");
                        return;
                }
            }
            catch (Exception ex)
            {
                TestMod.mls.LogError($"Error processing message: {ex.Message} {message}");



            }
        }

       
        private static bool IsValidJson(string json)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };

                JsonConvert.DeserializeObject<JsonMessage>(json, settings);
                return true;
            }
            catch
            {
                return false;
            }
        }




        public class JsonMessage
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string type { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string command { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string version { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string response { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string messageID { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int requestID { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string tag { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string playerName { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string arg1 { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string arg2 { get; set; }

 

        }

        private static void ProcessCommand(JsonMessage jsonMessage, string playerName)
        {

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            string responseStatus = "STATUS_SUCCESS";
            int requestID;

            switch (jsonMessage.command)
            {
                case "VERSION":
                    if (!isHost) return;
                    bool versionMatched = jsonMessage.version == modVersion;

                    var response = new JsonMessage
                    {
                        type = "RSP",
                        command = "VERSION",
                        playerName = playerName,
                        response = versionMatched.ToString(),
                        messageID = jsonMessage.messageID,
                        tag = MESSAGE_TAG
                    };

                 

                    mls.LogInfo($"Processing version check for: {playerName} Version: {jsonMessage.version} Matched: {versionMatched}");

                    string jsonResponse = JsonConvert.SerializeObject(response, settings);
                    Instance.SendChatMessage(jsonResponse);

                    
                    break;
                case "UPDATE_FP":

                    GameData gd = GameData.Instance;
                    // do this on the client and server, then the update only happens on the server
                    gd.gameFranchisePoints += 1;
                    gd.NetworkgameFranchisePoints = gd.gameFranchisePoints;
                    gd.UIFranchisePointsOBJ.text = gd.gameFranchisePoints.ToString();

                    if (!isHost) return;

                    var methodInfo = typeof(GameData).GetMethod("RpcAcquireFranchise", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (methodInfo != null)
                    {
                        methodInfo.Invoke(GameData.Instance, new object[] { 0 });
                    }


                    break;

                case "JAIL_PLAYER":
                    if (!isHost) return;

                    string jailPlayerName = jsonMessage.arg1;
                    string jailTwitchViewer = jsonMessage.arg2;
                    requestID = jsonMessage.requestID;


                        responseStatus = "STATUS_FAILURE";
                        try
                        {

                        var serverObjects = NetworkServer.spawned;
                        foreach (var kvp in serverObjects)
                        {
                            NetworkIdentity serverIdentity = kvp.Value;

                            if (serverIdentity.gameObject.name.Contains("Player"))
                            {

                                PlayerObjectController playerInfo = serverIdentity.gameObject.GetComponentInChildren<PlayerObjectController>();
                                if (playerInfo != null)
                                {
                                    if (jailPlayerName.ToLower() == playerInfo.PlayerName.ToLower())
                                    {
                                        TestMod.mls.LogInfo("Jail Player " + playerInfo.PlayerName);
                                        PlayerPermissions playerPermssions = serverIdentity.gameObject.GetComponentInChildren<PlayerPermissions>();
                                        CrowdDelegates.callFunc(playerPermssions, "RpcJPlayer", 69);
                                        responseStatus = "STATUS_SUCCESS";

                                        SendHudMessage($"{jailTwitchViewer} has sent {jailPlayerName} to jail!");


                                    }
                                }


                            }
                            

                        }


                    }
                        catch (Exception e)
                        {
                            responseStatus = "STATUS_FAILURE";
                        }


                        var jail_response = new JsonMessage
                        {
                            type = "RSP",
                            command = "JAIL_PLAYER",
                            requestID = requestID,
                            response = responseStatus,
                            tag = MESSAGE_TAG
                        };

                        Instance.SendChatMessage(JsonConvert.SerializeObject(jail_response, settings));

                    break;


                case "SPAWN_CUS":

                        string customerName = jsonMessage.arg1;
                        string _twitchChannel = jsonMessage.arg2;
                        requestID = jsonMessage.requestID;
                        
                    if (customerName.Length >= 1)
                        {

                            if (!isHost) return;


                            if (_twitchChannel.Length >= 1 && isTwitchChatAllowed)
                            {
                                //set whatever the last channel we get here, just incase we're not connected yet, we use that for the connection
                                twitchChannel = _twitchChannel;


                                if (isChatConnected && !twitchChannels.Contains(_twitchChannel))
                                {
                                    twitchChannels.Add(_twitchChannel);
                                    twitchWriter.WriteLine($"JOIN #{_twitchChannel}");
                                    twitchWriter.Flush();
                                    mls.LogInfo($"Connected to {_twitchChannel} Twitch Chat!");
                                }

                                if (!isChatConnected && twitchChannel.Length >= 1)
                                {
                                    twitchChannels.Add(_twitchChannel);
                                    ConnectToTwitchChat();
                                }

                            }

                        try
                        {
                            NPC_Manager npcManager = NPC_Manager.FindFirstObjectByType<NPC_Manager>();
                            uint customerNetID = (uint)UnityEngine.Random.Range(0, npcManager.NPCsArray.Length - 1);

                            Vector3 position = npcManager.spawnPointsOBJ.transform.GetChild(UnityEngine.Random.Range(0, npcManager.spawnPointsOBJ.transform.childCount - 1)).transform.position;
                            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(npcManager.npcAgentPrefab, position, Quaternion.identity);
                            gameObject.transform.SetParent(npcManager.customersnpcParentOBJ.transform);

                            NPC_Info npc = gameObject.GetComponent<NPC_Info>();
                            npc.NetworkNPCID = (int)customerNetID;
                            npc.productItemPlaceWait = Mathf.Clamp(0.5f - (float)GameData.Instance.gameDay * 0.003f, 0.1f, 0.5f);

                            npc.name = customerNetID.ToString();
                            gameObject.name = customerNetID.ToString();

                            int num5 = UnityEngine.Random.Range(2 + GameData.Instance.difficulty, GameData.Instance.maxProductsCustomersToBuy);
                            for (int i = 0; i < num5; i++)
                            {
                                int item = ProductListing.Instance.availableProducts[UnityEngine.Random.Range(0, ProductListing.Instance.availableProducts.Count)];
                                npc.productsIDToBuy.Add(item);
                            }

                            NavMeshAgent component2 = gameObject.GetComponent<NavMeshAgent>();
                            component2.enabled = true;
                            component2.stoppingDistance = 1f;
                            component2.speed = 1.9f + (float)Mathf.Clamp(GameData.Instance.gameDay - 7, 0, 40) * 0.07f + (float)NetworkServer.connections.Count * 0.1f + (float)GameData.Instance.difficulty * 0.2f;

                            Vector3 position2 = npcManager.shelvesOBJ.transform.GetChild(UnityEngine.Random.Range(0, npcManager.shelvesOBJ.transform.childCount - 1)).Find("Standspot").transform.position;
                            component2.destination = position2;

                            NetworkServer.Spawn(gameObject, (NetworkConnection)null);
                            NetworkIdentity networkIdentity = gameObject.GetComponent<NetworkIdentity>();

                            if (networkIdentity == null) return;
                            uint _customerNetID = networkIdentity.netId;

                            //mls.LogInfo($"NPC {_customerNetID.ToString()}");
                            if (_customerNetID == 0) return;

                            AddOrUpdateCustomer(_customerNetID.ToString(), gameObject.name);

                            var spawn_msg = new JsonMessage
                            {
                                type = "BST",
                                command = "SPAWN_CUS",
                                arg1 = customerName,
                                arg2 = _customerNetID.ToString(),
                                tag = MESSAGE_TAG
                            };
                            //mls.LogInfo($"Broadcasting {customerName} to NPC {_customerNetID.ToString()}");
                            Instance.SendChatMessage(JsonConvert.SerializeObject(spawn_msg, settings));

                            
                        } catch (Exception e)
                        {
                            responseStatus = "STATUS_FAILURE";
                        }


                        var spawn_response = new JsonMessage
                        {
                            type = "RSP",
                            command = "SPAWN_CUS",
                            requestID = requestID,
                            response = responseStatus,
                            tag = MESSAGE_TAG
                        };

                        Instance.SendChatMessage(JsonConvert.SerializeObject(spawn_response, settings));

                    }
                    

                    break;

                default:
                    mls.LogWarning($"Unknown command: {jsonMessage.command}");
                    break;
            }
        }


        public static void SendHudMessage(string message)
        {
            GameCanvas.Instance.CreateCanvasNotification($"CC:<color=red>{message}</color>");
        }

        public static void SendServerAnnouncement(string message)
        {

            string value = ("<color=green>CrowdControl:</color> " + message);
            PlayMakerFSM chatFSM = LobbyController.Instance.ChatContainerOBJ.GetComponent<PlayMakerFSM>();
            chatFSM.FsmVariables.GetFsmString("Message").Value = value;
            chatFSM.SendEvent("Send_Data");

        }

        private static void ProcessResponse(JsonMessage jsonMessage, string playerName)
        {
            switch (jsonMessage.command)
            {
                case "VERSION":
                {
                    //If we are already valid, we don't need to send this again.
                    //This will get set to false when we leave a server
                    if (validVersion) return;
                    bool versionMatched = bool.TryParse(jsonMessage.response, out versionMatched);

                    if (Instance.pendingMessageIDs.Remove(jsonMessage.messageID))
                    {
                        versionResponse = true;
                        if (versionMatched)
                        {
                            validVersion = true;
                            mls.LogInfo($"Version Matched! Ready for Effects.");
                        }
                        else
                        {
                            validVersion = false;
                            mls.LogInfo($"Version Mismatch! Make sure mod version matches.");
                        }
                    }

                    break;
                }

                case "SPAWN_CUS":
                case "JAIL_PLAYER":
                    {

                        if (Instance.pendingMessageIDs.Remove(jsonMessage.requestID.ToString()))
                        {
                            if (!int.TryParse(jsonMessage.requestID.ToString(), out int msgID))
                            {
                                mls.LogWarning($"Invalid message ID: {jsonMessage.arg1}");
                                break;
                            }

                            if (!Enum.TryParse(jsonMessage.response, out CrowdResponse.Status status))
                            {
                                mls.LogWarning($"Invalid status: {jsonMessage.arg2}");
                                break;
                            }

                            if (!rspResponders.TryGetValue(msgID, out var responder))
                            {
                                mls.LogWarning($"No responder for message ID: {msgID}");
                                break;
                            }

                            try { responder(status); }
                            catch (Exception e)
                            {
                                mls.LogWarning($"Error processing response for message ID: {msgID}");
                                mls.LogError(e);
                            }
                        }

                    break;
                }
                // Add other response types here
                default:
                    mls.LogWarning($"Unknown response command: {jsonMessage.command}");
                    break;
            }
        }


        private static void ProcessBroadcast(JsonMessage jsonMessage, string playerName)
        {
            try
            {
                switch (jsonMessage.command)
                {
                    case "SPAWN_CUS":

                        string customerName = jsonMessage.arg1;
                        string customerNetID = jsonMessage.arg2;

                        AddOrUpdateCustomer(customerNetID, customerName);

                        if (customerDictionary.TryGetValue(customerNetID.ToString(), out string foundCustomerName))
                        {

                            if (uint.TryParse(customerNetID, out uint netID))
                            {
                                if (foundCustomerName.Length >= 1 && NetworkClient.spawned.TryGetValue(netID, out NetworkIdentity serverIdentity))
                                {
                                    GameObject localObject = serverIdentity.gameObject;

                                    GameObject namePlate = new GameObject("NamePlate");
                                    namePlate.transform.SetParent(localObject.transform);
                                    namePlate.transform.localPosition = Vector3.up * 1.9f;

                                    localObject.transform.name = foundCustomerName;

                                    TextMeshPro tmp = namePlate.AddComponent<TextMeshPro>();
                                    tmp.text = foundCustomerName;
                                    tmp.alignment = TextAlignmentOptions.Center;
                                    tmp.fontSize = 1;

                                    namePlate.AddComponent<NamePlateController>();

                                }
                            }
                        }



                        break;

                    


                    default:
                        mls.LogWarning($"Unknown response command: {jsonMessage.command}");
                        break;
                }
            }
            catch (Exception e)
            {

                mls.LogWarning("Unable to process customer spawn?" + e);
            }
        }



        private void SendChatMessage(string message)
        {

            if (NetworkClient.isConnected && NetworkClient.connection?.identity != null)
            {
                var playerController = NetworkClient.connection.identity.GetComponent<PlayerObjectController>();
                if (playerController != null)
                {
   
                    try
                    {
                        if (!string.IsNullOrEmpty(message))
                        {
                            playerController.SendChatMsg(message);
                        }
                    } catch (Exception e)
                    {
                        mls.LogError("Cannot send chat msg: " +  e);
                    }
                    

                }
                
            }
           
        }

        public static Queue<Action> ActionQueue = new Queue<Action>();


        public static GameObject currentTextObject = null;
        public static void CreateChatStatusText(string message)
        {

            //Only the host can enable/disable Twitch chat
            if (!isHost) return;
            if (currentTextObject != null) UnityEngine.Object.Destroy(currentTextObject);
            
            Camera cam = FindObjectOfType<Camera>();
            if (cam == null) return;

            currentTextObject = new GameObject("ChatStatusText");
            TextMeshPro chatStatusText = currentTextObject.AddComponent<TextMeshPro>();

            chatStatusText.fontSize = 0.05f;
            chatStatusText.color = new Color(0.5f, 0, 1);
            chatStatusText.alignment = TextAlignmentOptions.Center;
            chatStatusText.text = message;
            chatStatusText.lineSpacing = 1.2f;

            Vector3 screenCenterPosition = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.6f, 0.15f));
            currentTextObject.transform.position = screenCenterPosition;


            currentTextObject.transform.SetParent(cam.transform, true);
            currentTextObject.AddComponent<FaceCamera>();

            UnityEngine.Object.Destroy(currentTextObject, 3f);
        }

        public class FaceCamera : MonoBehaviour
        {
            private Camera mainCamera;

            void Start()
            {
                mainCamera = Camera.main ?? FindObjectOfType<Camera>();

            }

            void LateUpdate()
            {
                if (mainCamera == null) return;

                Vector3 directionToCamera = mainCamera.transform.position - transform.position;
                directionToCamera.y = 0;
                directionToCamera.Normalize();

                Quaternion lookRotation = Quaternion.LookRotation(directionToCamera);
                transform.rotation = lookRotation * Quaternion.Euler(0, 180, 0);

            }
        }

        [HarmonyPatch(typeof(PlayerNetwork), "Update")]
        [HarmonyPrefix]
        static void RunEffects()
        {


            if (UnityEngine.Input.GetKeyDown(KeyCode.F6))
            {
                isTwitchChatAllowed = !isTwitchChatAllowed;
                if (isChatConnected)
                {
                    DisconnectFromTwitch();
                    isChatConnected = false;
                }

                if (isTwitchChatAllowed)
                {
                    TestMod.mls.LogInfo("Twitch Chat is enabled.");
                    CreateChatStatusText("Twitch Chat is enabled.");
                }
                else
                {
                    TestMod.mls.LogInfo("Twitch Chat is disabled.");
                    CreateChatStatusText("Twitch Chat is disabled.");
                }

            }


            while (ActionQueue.Count > 0)
            {
                Action action = ActionQueue.Dequeue();
                action.Invoke();
            }

            lock (TimedThread.threads)
            {
                foreach (var thread in TimedThread.threads)
                {
                    if (!thread.paused)
                        thread.effect.tick();
                }
            }
        }

        [HarmonyPatch(typeof(EventSystem), "OnApplicationFocus")]
        public static class EventSystem_OnApplicationFocus_Patch
        {
            static void Postfix(bool hasFocus)
            {
                isFocused = hasFocus;
            }
        }


        [HarmonyPatch(typeof(LobbyController), "CreateHostPlayerItem")]
        public static class Patch_CreateHostPlayerItem
        {
            [HarmonyPostfix]
            public static void Postfix(LobbyController __instance)
            {
                // Set the player as host if they are the first player 
                var manager = AccessTools.Property(typeof(LobbyController), "Manager").GetValue(__instance) as CustomNetworkManager;

                if (manager != null && manager.GamePlayers.Count > 0 && manager.GamePlayers[0].ConnectionID == __instance.LocalplayerController.ConnectionID)
                {
                    isHost = true;
                }
                else
                {
                    isHost = false;
                }
            }
        }

        [HarmonyPatch(typeof(LobbyController), "RemovePlayerItem")]
        public static class Patch_RemovePlayerItem
        {
            [HarmonyPostfix]
            public static void Postfix(LobbyController __instance)
            {
                if (isHost) return;
                ResetPackChecks();
            }
        }


        public class NamePlateController : MonoBehaviour
        {
            private Camera mainCamera;

            void Start()
            {
                mainCamera = Camera.main;

                if (mainCamera == null)
                {
                    mainCamera = FindObjectOfType<Camera>();
                }
            }

            void LateUpdate()
            {
                if (mainCamera == null) return;

                Vector3 directionToCamera = mainCamera.transform.position - transform.position;
                directionToCamera.y = 0;
                Quaternion lookRotation = Quaternion.LookRotation(directionToCamera);
                transform.rotation = lookRotation * Quaternion.Euler(0, 180, 0);
            }
        }



        [HarmonyPatch(typeof(NPC_Info), "AuxiliarAnimationPlay")]
        public class Patch_NPC_Info_AuxiliarAnimationPlay
        {
            static void Postfix(NPC_Info __instance, int animationIndex)
            {

                if (__instance.name.Length >=1 && isChatConnected && !__instance.name.Contains("(Clone)"))
                {
                   //possible twitch timeout functionality later?
                   //mls.LogInfo($"HIT {__instance.name} WITH A BROOOM");
                }
                
            }
        }

        

        [HarmonyPatch(typeof(GameCanvas), "CreateCanvasNotification")]
        public class Patch_GameCanvas_CreateCanvasNotification
        {
            static bool Prefix(GameCanvas __instance, string hash)
            {
                if (!hash.StartsWith("CC:")) return true;
                hash = hash.Replace("CC:", "");
                GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(__instance.notificationPrefab, __instance.notificationParentTransform);
                gameObject.GetComponent<TextMeshProUGUI>().text = hash;
                gameObject.SetActive(true);
                return false;

            }
        }


        [HarmonyPatch(typeof(NPC_Info), "UserCode_RPCNotificationAboveHead__String__String")]
        public class Patch_UserCode_RPCNotificationAboveHead
        {
            static bool Prefix(NPC_Info __instance, string message1, string messageAddon)
            {
                if (messageAddon == "crowdcontrol")
                {
                    GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(__instance.messagePrefab, __instance.transform.position + Vector3.up * 1.8f, Quaternion.identity);
                    gameObject.GetComponent<TextMeshPro>().text = message1;
                    gameObject.SetActive(true);
                    return false;
                }

                return true;
            }
        }



        [HarmonyPatch(typeof(PlayerNetwork), "Start")]
        public static class Patch_PlayerNetwork_Start
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (isHost) return;
                if (ranVersionCheck) return;
                ranVersionCheck = true;
                if (!validVersion)
                {
                    TestMod.SendVersionCheck();
                    HandleVersionCheckTimeout();
                }
            }


            private static async void HandleVersionCheckTimeout()
            {
                await Task.Delay(5000);

                if (!versionResponse)
                {
                    mls.LogError("Host does is not running Crowd Control");
                }
            }

        }


    }
}