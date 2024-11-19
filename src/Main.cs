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
using System.Text.RegularExpressions;
using UnityEngine.Windows;
using System.Reflection;
using System.Collections;
using HutongGames.PlayMaker.Actions;
using System.Runtime.Remoting.Messaging;
using static UnityEngine.Rendering.RayTracingAccelerationStructure;
using static HutongGames.PlayMaker.Actions.Vector2RandomValue;
using System.IO.Compression;
using System.Text;

namespace BepinControl
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class TestMod : BaseUnityPlugin
    {
        private const string modGUID = "WarpWorld.CrowdControl";
        private const string modName = "Crowd Control";
        private const string modVersion = "1.1.2.0";

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

        public static bool forceMath = false;
        private const string MESSAGE_TAG = "</b>";

        public static Dictionary<string, string> spawnedObjects = new Dictionary<string, string>();


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
        public static List<string> spawnedCustomers = new();



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

            /*
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
            */

            try
            {
                twitchTcpClient = new TcpClient(twitchServer, twitchPort);
                twitchStream = twitchTcpClient.GetStream();
                twitchReader = new StreamReader(twitchStream);
                twitchWriter = new StreamWriter(twitchStream);

                // Request membership and tags capabilities from Twitch
                twitchWriter.WriteLine("CAP REQ :twitch.tv/membership twitch.tv/tags twitch.tv/commands");

                //if (twitchOauth.Length > 0) twitchWriter.WriteLine($"PASS oauth:{twitchOauth}");

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
                                        chatMessage = Regex.Replace(chatMessage, @"[^A-Za-z0-9!?.<>=/@#$%^&*(){}_\[\]\"";:'\\ ]", "");
                                        chatMessage = chatMessage.ToLower();
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

                                                //3_TV(Clone) - 3594812703

                                                var spawnedObjects = isHost ? NetworkServer.spawned : NetworkClient.spawned;

                                                NetworkIdentity closestIdentity = null;
                                                float closestDistance = float.MaxValue;

                                                Camera mainCamera = Camera.main;

                                                foreach (var kvp in spawnedObjects)
                                                {
                                                    NetworkIdentity serverIdentity = kvp.Value;

                                                    if (serverIdentity.assetId != 620925214) continue;
                                                    if (serverIdentity.gameObject.name.ToLower() != username.ToLower()) continue;

                                                    NPC_Info npcInfo = serverIdentity.gameObject.GetComponentInChildren<NPC_Info>();

                                                    if (npcInfo != null)
                                                    {
                                                        float distance = Vector3.Distance(mainCamera.transform.position, serverIdentity.gameObject.transform.position);

                                                        if (distance < closestDistance)
                                                        {
                                                            closestDistance = distance;
                                                            closestIdentity = serverIdentity;
                                                        }
                                                    }
                                                }

                                                if (closestIdentity != null)
                                                {
                                                    NPC_Info closestNpcInfo = closestIdentity.gameObject.GetComponentInChildren<NPC_Info>();

                                                    if (closestNpcInfo != null)
                                                    {
                                                       // mls.LogInfo($"{closestIdentity.name} - {closestIdentity.assetId} - {closestNpcInfo.isEmployee}  ");

                                                        if (chatMessage.Contains("*trash*") && !closestNpcInfo.isEmployee)
                                                        {
                                                            chatMessage = chatMessage.Replace("*trash*", "");
                                                            DropTrash(closestNpcInfo, closestIdentity.gameObject.name);
                                                        }
                                                    /*    no one can quit yet...
                                                     *    else if (chatMessage.Contains("*quit*") && closestNpcInfo.isEmployee)
                                                        {
                                                            chatMessage = chatMessage.Replace("*quit*", "");
                                                            if (chatMessage == "") chatMessage = "I QUIT!";
                                                            AboveNPCMessage(chatMessage, closestNpcInfo);

                                                            NPC_Manager npcManager = NPC_Manager.FindFirstObjectByType<NPC_Manager>();
                                                            closestNpcInfo.isEmployee = false;
                                                            npcManager.maxEmployees = npcManager.maxEmployees - 1;
                                                            if (npcManager.maxEmployees <= 0) npcManager.maxEmployees = 0;

                                                            //Destroy(closestNpcInfo.gameObject);
                                                            npcManager.UpdateEmployeesNumberInBlackboard();

                                                            //CrowdDelegates.callFunc(npcManager, "AssignEmployeePriorities", "");
                                                        } */
                                                        else
                                                        {
                                                            AboveNPCMessage(chatMessage, closestNpcInfo);
                                                        }
                                                    }


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


        public static void AddOrUpdateSpawnedObjects(string netID, string spawnName)
        {
            if (spawnName.Length >= 1)
            {
                if (spawnedObjects.ContainsKey(netID))
                {
                    spawnedObjects[netID] = spawnName;
                }
                else
                {
                    spawnedObjects.Add(netID, spawnName);
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
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
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



        public static void SendSpawnTrain(int requestID, ulong steamID, CrowdRequest crowdRequest)
        {

            Instance.pendingMessageIDs.Add(requestID.ToString());
            CrowdRequest.SourceDetails sourceDetails = crowdRequest.sourceDetails;
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            string json = JsonConvert.SerializeObject(sourceDetails);
            string compressedBase64Json = CompressAndEncode(json);


            var message = new
            {
                type = "CMD",
                command = "SPAWN_TRAIN",
                arg1 = compressedBase64Json,
                steamID = steamID,
                tag = MESSAGE_TAG,
                requestID = requestID
            };


            string jsonMessage = JsonConvert.SerializeObject(message, settings);


            Instance.SendChatMessage(jsonMessage);




        }

        public static void SendSpawnEmployee(int requestID, string customerName, string _twitchChannel = null)
        {

            Instance.pendingMessageIDs.Add(requestID.ToString());

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            var message = new
            {
                type = "CMD",
                command = "SPAWN_EMP",
                arg1 = customerName,
                arg2 = (_twitchChannel != null && _twitchChannel.Length >= 1) ? _twitchChannel : null,
                tag = MESSAGE_TAG,
                requestID = requestID
            };


            string jsonMessage = JsonConvert.SerializeObject(message, settings);
            Instance.SendChatMessage(jsonMessage);
        }

        public static void SpawnTrash(int requestID, string viewerName)
        {

            Instance.pendingMessageIDs.Add(requestID.ToString());

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            var message = new
            {
                type = "CMD",
                command = "SPAWN_TRASH",
                arg1 = viewerName,
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
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
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


        public static void JailPlayer(int requestID, ulong steamID, string viewerName)
        {

            Instance.pendingMessageIDs.Add(requestID.ToString());

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            var message = new
            {
                type = "CMD",
                command = "JAIL_PLAYER",
                steamID = steamID,
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
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
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
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore

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
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
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

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string channelName { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public ulong steamID { get; set; }


            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public Vector3 position { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public Quaternion rotation { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public Vector3 forwardDirection { get; set; }


            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public Transform playerCamera { get; set; }

        }

        public static string CompressAndEncode(string json)
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            // Compress using GZip or Deflate
            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
                {
                    gzipStream.Write(jsonBytes, 0, jsonBytes.Length);
                }
                return Convert.ToBase64String(memoryStream.ToArray());
            }
        }

        public static string DecodeAndDecompress(string base64CompressedJson)
        {
            byte[] compressedBytes = Convert.FromBase64String(base64CompressedJson);

            using (var compressedStream = new MemoryStream(compressedBytes))
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                gzipStream.CopyTo(resultStream);
                byte[] decompressedBytes = resultStream.ToArray();
                return Encoding.UTF8.GetString(decompressedBytes);
            }
        }

        public class CoroutineManager : MonoBehaviour
        {
            private static CoroutineManager _instance;

            public static CoroutineManager Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        // Create a new GameObject to attach the MonoBehaviour to
                        var obj = new GameObject("CoroutineManager");
                        _instance = obj.AddComponent<CoroutineManager>();
                        DontDestroyOnLoad(obj); // Prevent the object from being destroyed
                    }
                    return _instance;
                }
            }

            // This method allows you to start coroutines
            public static void StartRoutine(IEnumerator routine)
            {
                Instance.StartCoroutine(routine);
            }
        }

        private static void ProcessCommand(JsonMessage jsonMessage, string playerName)
        {

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
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
                        command = jsonMessage.command,
                        playerName = playerName,
                        response = versionMatched.ToString(),
                        messageID = jsonMessage.messageID,
                        tag = MESSAGE_TAG
                    };

                    mls.LogInfo($"Processing version check for: {playerName} Version: {jsonMessage.version} Matched: {versionMatched}");

                    string jsonResponse = JsonConvert.SerializeObject(response, settings);
                    Instance.SendChatMessage(jsonResponse);


                    break;

                case "SPAWN_TRAIN":
                    string compressedBase64Json = jsonMessage.arg1;
                    ulong targetSteamID = jsonMessage.steamID;

                    string decompressedJson = DecodeAndDecompress(compressedBase64Json);
                    CrowdRequest.SourceDetails sourceDetails = JsonConvert.DeserializeObject<CrowdRequest.SourceDetails>(decompressedJson);


                    if (isHost) {
                        try { 
                        var serverObjects = NetworkServer.spawned;
                        foreach (var kvp in serverObjects)
                        {
                            NetworkIdentity serverIdentity = kvp.Value;

                            if (serverIdentity.gameObject.name.Contains("Player"))
                            {

                                PlayerObjectController playerInfo = serverIdentity.gameObject.GetComponentInChildren<PlayerObjectController>();
                                if (playerInfo != null)
                                {

                                        if (targetSteamID == playerInfo.PlayerSteamID)
                                        {


                                            Vector3 position = playerInfo.transform.position;
                                            Quaternion rotation = playerInfo.transform.rotation;
                                            Vector3 forwardDirection = playerInfo.transform.forward;
                                            CrowdDelegates.Spawn_HypeTrain(sourceDetails, position, rotation, forwardDirection, playerInfo.transform);



                                        }
                                    }

                            }
                        }
                    }
                    catch (Exception e)
                    {
                        
                    }

            } else
            {
                        try
                        {
                            var serverObjects = NetworkClient.spawned;
                            foreach (var kvp in serverObjects)
                            {
                                NetworkIdentity serverIdentity = kvp.Value;

                                if (serverIdentity.gameObject.name.Contains("Player"))
                                {

                                    PlayerObjectController playerInfo = serverIdentity.gameObject.GetComponentInChildren<PlayerObjectController>();
                                    if (playerInfo != null)
                                    {
                                        if (targetSteamID == playerInfo.PlayerSteamID)
                                        {


                                            Vector3 position = playerInfo.transform.position;
                                            Quaternion rotation = playerInfo.transform.rotation;
                                            Vector3 forwardDirection = playerInfo.transform.forward;
                                            CrowdDelegates.Spawn_HypeTrain(sourceDetails, position, rotation, forwardDirection, playerInfo.transform);
                                            



                                        }
                                    }

                                }
                            }
                        }
                        catch (Exception e)
                        {

                        }
                    }



                    break;


                case "SPAWN_TRASH":
                    if (!isHost) return;

                    string customerName = jsonMessage.arg1;
                    try
                    {
                        GameData gameData = GameData.Instance;



                        int maxExclusive = 6 + gameData.GetComponent<UpgradesManager>().spaceBought;
                        int index = UnityEngine.Random.Range(0, maxExclusive);
                        Transform baseRaycastSpot = gameData.trashSpotsParent.transform.GetChild(index);
                        Vector3 spawnSpot = Vector3.zero;
                        bool foundRaycastSpot = false;
                        while (!foundRaycastSpot)
                        {
                            RaycastHit raycastHit;
                            if (Physics.Raycast(baseRaycastSpot.position + new Vector3(UnityEngine.Random.Range(-2.4f, 2.4f), 0f, UnityEngine.Random.Range(-1.9f, 1.9f)), -Vector3.up, out raycastHit, 5f, gameData.lMask) && raycastHit.transform.gameObject.tag == "Buildable")
                            {
                                spawnSpot = raycastHit.point;
                                foundRaycastSpot = true;
                            }
                        }
                        int networktrashID = UnityEngine.Random.Range(0, 5);
                        GameObject gameObject = Instantiate(gameData.trashSpawnPrefab, gameData.GetComponent<NetworkSpawner>().levelPropsOBJ.transform.GetChild(6).transform);
                        gameObject.transform.position = spawnSpot;
                        gameObject.GetComponent<TrashSpawn>().NetworktrashID = networktrashID;
                        gameObject.GetComponent<PlayMakerFSM>().enabled = true;
                        NetworkServer.Spawn(gameObject, (NetworkConnection)null);

                        NetworkIdentity networkIdentity = gameObject.GetComponent<NetworkIdentity>();

                        if (networkIdentity == null) return;
                        uint objectNetID = networkIdentity.netId;
                        if (objectNetID == 0) return;


                        var spawn_msg = new JsonMessage
                        {
                            type = "BST",
                            command = "SPAWN_TRASH",
                            arg1 = customerName,
                            arg2 = objectNetID.ToString(),
                            tag = MESSAGE_TAG
                        };
                        Instance.SendChatMessage(JsonConvert.SerializeObject(spawn_msg, settings));


                    }
                    catch (Exception e)
                    {
                        responseStatus = "STATUS_FAILURE";
                    }

                    requestID = jsonMessage.requestID;

                    var trash_response = new JsonMessage
                    {
                        type = "RSP",
                        command = jsonMessage.command,
                        requestID = requestID,
                        response = responseStatus,
                        tag = MESSAGE_TAG
                    };

                    Instance.SendChatMessage(JsonConvert.SerializeObject(trash_response, settings));

                    break;

                case "UPDATE_FP":

                    GameData gd = GameData.Instance;
                    // do this on the client and server, then the update only happens on the server
                    gd.gameFranchisePoints += 1;
                    gd.NetworkgameFranchisePoints = gd.gameFranchisePoints;
                    gd.UIFranchisePointsOBJ.text = gd.gameFranchisePoints.ToString();

                    if (!isHost) return;

                    var methodInfo = typeof(GameData).GetMethod("RpcAcquireFranchise", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (methodInfo != null) methodInfo.Invoke(GameData.Instance, new object[] { 0 });

                    break;



                case "JAIL_PLAYER":
                    if (!isHost) return;

                    ulong jailSteamID = jsonMessage.steamID;
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
                                    if (jailSteamID == playerInfo.PlayerSteamID)
                                    {

                                        PlayerPermissions playerPermssions = serverIdentity.gameObject.GetComponentInChildren<PlayerPermissions>();
                                        CrowdDelegates.callFunc(playerPermssions, "RpcJPlayer", 69);
                                        SendHudMessage($"{jailTwitchViewer} has sent a player to jail!", "red");
                                        responseStatus = "STATUS_SUCCESS";

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

                    customerName = jsonMessage.arg1;
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
                            if (_customerNetID == 0) return;


                            var spawn_msg = new JsonMessage
                            {
                                type = "BST",
                                command = "SPAWN_CUS",
                                arg1 = customerName,
                                arg2 = _customerNetID.ToString(),
                                channelName = _twitchChannel,
                                tag = MESSAGE_TAG
                            };
                            //mls.LogInfo($"Broadcasting {customerName} to NPC {_customerNetID.ToString()}");
                            Instance.SendChatMessage(JsonConvert.SerializeObject(spawn_msg, settings));


                        }
                        catch (Exception e)
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


                case "SPAWN_EMP":

                    customerName = jsonMessage.arg1;
                    _twitchChannel = jsonMessage.arg2;
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

                            Vector3 position = npcManager.employeeSpawnpoint.transform.position + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f));
                            GameObject gameObject = GameObject.Instantiate<GameObject>(npcManager.npcAgentPrefab, position, Quaternion.identity);
                            gameObject.transform.SetParent(npcManager.employeeParentOBJ.transform);
                            gameObject.name = customerName.ToString();
                            NPC_Info npc = gameObject.GetComponent<NPC_Info>();
                            npc.NetworkNPCID = UnityEngine.Random.Range(0, npcManager.NPCsEmployeesArray.Length - 1);
                            npc.NetworkisEmployee = true;
                            npc.name = customerName.ToString();
                            NetworkServer.Spawn(gameObject, (NetworkConnection)null);
                            NavMeshAgent component2 = gameObject.GetComponent<NavMeshAgent>();
                            component2.agentTypeID = npcManager.transform.Find("AgentSample").GetComponent<NavMeshAgent>().agentTypeID;
                            component2.enabled = true;
                            component2.speed = 2.5f + 2.5f * npcManager.extraEmployeeSpeedFactor;

                            NetworkServer.Spawn(gameObject, (NetworkConnection)null);
                            NetworkIdentity networkIdentity = gameObject.GetComponent<NetworkIdentity>();

                            if (networkIdentity == null) return;
                            uint _npcNetID = networkIdentity.netId;
                            if (_npcNetID == 0) return;

                            npcManager.maxEmployees++;
                            npcManager.UpdateEmployeesNumberInBlackboard();


                            AddOrUpdateSpawnedObjects(_npcNetID.ToString(), customerName);

                            var spawn_msg = new JsonMessage
                            {
                                type = "BST",
                                command = "SPAWN_EMP",
                                arg1 = customerName,
                                arg2 = _npcNetID.ToString(),
                                tag = MESSAGE_TAG
                            };
                            Instance.SendChatMessage(JsonConvert.SerializeObject(spawn_msg, settings));


                        }
                        catch (Exception e)
                        {
                            mls.LogInfo(e);
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


        public static void SendHudMessage(string message, string color = "blue", bool important = false)
        {
            GameCanvas gameCanvas = GameCanvas.FindFirstObjectByType<GameCanvas>();
            GameObject gameObject = important ? GameObject.Instantiate<GameObject>(gameCanvas.importantNotificationPrefab, gameCanvas.importantNotificationParentTransform) : GameObject.Instantiate<GameObject>(gameCanvas.notificationPrefab, gameCanvas.notificationParentTransform);
            gameObject.GetComponent<TextMeshProUGUI>().text = $"<color={color}>{message}</color>";
            gameObject.SetActive(true);
        }

        public static void AboveNPCMessage(string message, NPC_Info npc_info)
        {
            GameObject gameObject = GameObject.Instantiate<GameObject>(npc_info.messagePrefab, npc_info.transform.position + Vector3.up * 1.8f, Quaternion.identity);
            gameObject.GetComponent<TextMeshPro>().text = message;
            gameObject.SetActive(true);
    
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

                case "SPAWN_EMP":
                case "SPAWN_CUS":
                case "JAIL_PLAYER":
                case "SPAWN_TRASH":
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


        private static float GetObjectHeight(GameObject obj)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                return collider.bounds.size.y;
            }
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds.size.y;
            }

            return 1.8f;
        }


        public class NamePlateController : MonoBehaviour
        {
            public Transform target;
            private Camera mainCamera;
            public float distanceThreshold = 4f;

            private TextMeshPro tmp;

            void Start()
            {
                mainCamera = Camera.main;
                tmp = GetComponent<TextMeshPro>();
            }

            void Update()
            {
                if (target == null) return;

                float distance = Vector3.Distance(mainCamera.transform.position, target.position);

                if (distance <= distanceThreshold)
                {
                    tmp.enabled = true;
                    Vector3 directionToCamera = mainCamera.transform.position - transform.position;
                    directionToCamera.y = 0;
                    Quaternion lookRotation = Quaternion.LookRotation(directionToCamera);
                    transform.rotation = lookRotation * Quaternion.Euler(0, 180, 0);

                }
                else
                {
                    tmp.enabled = false;
                }
            }
        }

        private static void ProcessBroadcast(JsonMessage jsonMessage, string playerName)
        {
            try
            {
                switch (jsonMessage.command)
                {
                    case "SPAWN_TRASH":
                    case "SPAWN_CUS":
                    case "SPAWN_EMP":
                        string customerName = jsonMessage.arg1;
                        string customerNetID = jsonMessage.arg2;
                        string channelName = jsonMessage.channelName;

                        AddOrUpdateSpawnedObjects(customerNetID, customerName);

                        if (spawnedObjects.TryGetValue(customerNetID.ToString(), out string foundCustomerName))
                        {

                            if (uint.TryParse(customerNetID, out uint netID))
                            {
                                if (foundCustomerName.Length >= 1 && NetworkClient.spawned.TryGetValue(netID, out NetworkIdentity serverIdentity))
                                {
                                    GameObject localObject = serverIdentity.gameObject;

                                    float objectHeight = GetObjectHeight(localObject);

                                    GameObject namePlate = new GameObject("NamePlate");

                                    namePlate.transform.SetParent(localObject.transform);
                                    namePlate.transform.localPosition = Vector3.up * (objectHeight + 0.1f);

                                    localObject.transform.name = foundCustomerName + "-" + channelName;

                                    TextMeshPro tmp = namePlate.AddComponent<TextMeshPro>();
                                    tmp.text = foundCustomerName;
                                    tmp.alignment = TextAlignmentOptions.Center;
                                    tmp.fontSize = 1;

                                    NamePlateController namePlateController = namePlate.AddComponent<NamePlateController>();
                                    namePlateController.target = localObject.transform;
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



        private static void DropTrash(NPC_Info npc_info, string playerName)
        {


            TestMod.ActionQueue.Enqueue(() =>
            {
                GameData gameData = GameData.Instance;

                int networktrashID = UnityEngine.Random.Range(0, 5);
                GameObject gameObject = GameObject.Instantiate<GameObject>(gameData.trashSpawnPrefab, gameData.GetComponent<NetworkSpawner>().levelPropsOBJ.transform.GetChild(6).transform);
                gameObject.transform.position = npc_info.transform.position;
                gameObject.GetComponent<TrashSpawn>().NetworktrashID = networktrashID;
                gameObject.GetComponent<PlayMakerFSM>().enabled = true;
                NetworkServer.Spawn(gameObject, (NetworkConnection)null);

                NetworkIdentity networkIdentity = gameObject.GetComponent<NetworkIdentity>();

                if (networkIdentity == null) return;
                uint objectNetID = networkIdentity.netId;
                if (objectNetID == 0) return;


                var spawn_msg = new JsonMessage
                {
                    type = "BST",
                    command = "SPAWN_TRASH",
                    arg1 = playerName,
                    arg2 = objectNetID.ToString(),
                    tag = MESSAGE_TAG
                };
                //mls.LogInfo($"Broadcasting {customerName} to NPC {_customerNetID.ToString()}");
                Instance.SendChatMessage(JsonConvert.SerializeObject(spawn_msg));

            });



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
                    }
                    catch (Exception e)
                    {
                        mls.LogError("Cannot send chat msg: " + e);
                    }


                }

            }

        }

        public static Queue<Action> ActionQueue = new Queue<Action>();






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
                    //CreateChatStatusText("Twitch Chat is enabled.");
                    SendHudMessage("Twitch Chat is enabled", "green", true);
                }
                else
                {
                    TestMod.mls.LogInfo("Twitch Chat is disabled.");
                    SendHudMessage("Twitch Chat is disabled.", "red", true);
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








        [HarmonyPatch(typeof(NPC_Info), "AuxiliarAnimationPlay")]
        public class Patch_NPC_Info_AuxiliarAnimationPlay
        {
            static void Postfix(NPC_Info __instance, int animationIndex)
            {

                if (__instance.name.Length >= 1 && isChatConnected && !__instance.name.Contains("(Clone)"))
                {
                    string viewerName = __instance.name.Split('-')[0].ToLower();

                    // If the viewername + twitchChannel = __instance.name, then that user was spawned in that channel
                    if (viewerName + "-" + twitchChannel.ToLower() == __instance.name.ToLower())
                    {
                        if (TestMod.spawnedCustomers.Contains(viewerName))
                        {
                            mls.LogInfo($"HIT {viewerName} WITH A BROOOM");
                            ControlClient.TimeoutUser(viewerName, 1, "broomed!");
                        }
                    } 
                    
                }

            }
        }




        /*
        [HarmonyPatch(typeof(Data_Container), "UserCode_CmdActivateCashMethod__Int32")]
        public class Patch_DataContainer_UserCode_CmdActivateCashMethod__Int32
        {
            public static void Postfix(Data_Container __instance, int amountToPay)
            {
                TextMeshProUGUI component = __instance.transform.Find("CashRegisterCanvas/Container/MoneyToReturn").GetComponent<TextMeshProUGUI>();
                component.color = Color.red;
                component.text = "DO THE MATH!";
            }
        }

        [HarmonyPatch(typeof(Data_Container), "UpdateCash")]
        public class Patch_DataContainer_UpdateCash
        {
            public static void Postfix(Data_Container __instance, float amountToAdd)
            {

                TextMeshProUGUI component = __instance.transform.Find("CashRegisterCanvas/Container/MoneyToReturn").GetComponent<TextMeshProUGUI>();
                component.color = Color.red;
                component.text = "DO THE MATH!";

            }
        }


        */


        [HarmonyPatch(typeof(Data_Container), "UserCode_RpcHidePaymentMethod__Int32__Int32")]
        public class Patch_DataContainer_UserCode_RpcHidePaymentMethod__Int32__Int32
        {
            public static void Postfix(Data_Container __instance, int index, int amountGiven)
            {
                if (!forceMath) return;
                TextMeshProUGUI component = __instance.transform.Find("CashRegisterCanvas/Container/MoneyToReturn").GetComponent<TextMeshProUGUI>();
                component.color = Color.red;
                component.text = "DO THE MATH!";
            }
        }


        [HarmonyPatch(typeof(GameData), "UpdateSunPosition")]
        public class Patch_UpdateSunPosition
        {
            [HarmonyPostfix]
            public static void Postfix(GameData __instance)
            {
                // Find the main camera
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    // Position the sun and moon near the camera's location
                    Vector3 cameraPosition = mainCamera.transform.position;

                    // Adjust the positions of sun and moon relative to the camera
                    // For example, placing them at some fixed distance from the camera
                    float sunDistance = 20f; // Example distance from the camera for the sun
                    float moonDistance = 20f; // Example distance from the camera for the moon

                    __instance.sunLight.transform.position = cameraPosition + mainCamera.transform.forward * sunDistance;
                    __instance.moonLight.transform.position = cameraPosition + mainCamera.transform.forward * moonDistance;
                }
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