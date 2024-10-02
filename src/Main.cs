using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static HutongGames.PlayMaker.Actions.SendMessage;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine.AI;
using System.IO;
using System.Net.Sockets;
using J4F;
using System.Linq;

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



        // START TWITCH STUFF
        public static bool isIrcConnected = false;
        private static bool isChatConnected = false;
        private static bool isTwitchChatAllowed = true;
        private const string twitchServer = "irc.chat.twitch.tv";
        private const int twitchPort = 6667;
        private const string twitchUsername = "justinfan1337";
        public static string twitchChannel = "jaku";
        private static TcpClient twitchTcpClient;
        private static NetworkStream twitchStream;
        private static StreamReader twitchReader;
        private static StreamWriter twitchWriter;

        private static TextMeshPro chatStatusText;


        private static List<string> allowedUsernames = new List<string> { "jaku", "s4turn", "crowdcontrol", "theunknowncod3r" };


        public static void ConnectToTwitchChat()
        {
            if (!isChatConnected && twitchChannel.Length >= 1)
            {
                new Thread(new ThreadStart(StartTwitchChatListener)).Start();
                isChatConnected = true;
            }
        }

        public static NPC_Info FindChildByNPCID(GameObject parentObject, int targetNPCID)
        {
            if (parentObject == null)
            {
                // Parent object is null
                Debug.LogError("Parent object is null.");
                return null;
            }

            int childCount = parentObject.transform.childCount;

            // If there are no child objects
            if (childCount == 0)
            {
                // No children found
                Debug.LogWarning("No child objects found.");
                return null;
            }

            // Loop through each child of the parent object
            for (int i = 0; i < childCount; i++)
            {
                Transform childTransform = parentObject.transform.GetChild(i);

                // Get the NPC_Info component of the child
                NPC_Info npcInfo = childTransform.gameObject.GetComponent<NPC_Info>();

                if (npcInfo != null && npcInfo.NetworkNPCID == targetNPCID)
                {
                    // We found the matching NPC by NetworkNPCID
                    Debug.Log("Found NPC with NetworkNPCID: " + targetNPCID);
                    return npcInfo;
                }
            }

            // No matching NPC found
            Debug.LogWarning("No matching NPC found for NetworkNPCID: " + targetNPCID);
            return null;
        }

        public static void StartTwitchChatListener()
        {
            try
            {
                twitchTcpClient = new TcpClient(twitchServer, twitchPort);
                twitchStream = twitchTcpClient.GetStream();
                twitchReader = new StreamReader(twitchStream);
                twitchWriter = new StreamWriter(twitchStream);

                // Request membership and tags capabilities from Twitch
                twitchWriter.WriteLine("CAP REQ :twitch.tv/membership twitch.tv/tags");

                twitchWriter.WriteLine($"NICK {twitchUsername}");
                twitchWriter.WriteLine($"JOIN #{twitchChannel}");
                twitchWriter.Flush();

                mls.LogInfo($"Connected to Twitch channel: {twitchChannel}");



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

                                        //mls.LogInfo($"chatMessage: {chatMessage}");
                                        //mls.LogInfo($"username: {username}");
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

                                                List<string> customerNetIDs = GetKeysByValue(username);
                                                mls.LogInfo("customerNetIDs:" + customerNetIDs);

                                                foreach (string netID in customerNetIDs)
                                                {
                                                    GameObject npcObject = NPC_Manager.Instance.NPCsArray[int.Parse(netID)];
                                                    NPC_Info npcInfo = FindChildByNPCID(NPC_Manager.Instance.customersnpcParentOBJ, int.Parse(netID));

                                                    if (npcInfo != null)
                                                    {

                                                        mls.LogInfo("CHAT:" + netID + " " + chatMessage);
                                                        npcInfo.RPCNotificationAboveHead(chatMessage, "crowdcontrol");

                                                    } else
                                                    {
                                                        mls.LogInfo("Boo?" + netID);
                                                    }

                                                }

                                               

                                                /*
                                                 * List<Customer> customers = (List<Customer>)CrowdDelegates.getProperty(CSingleton<CustomerManager>.Instance, "m_CustomerList");

                                                 if (customers.Count >= 1)
                                                 {
                                                     foreach (Customer customer in customers)
                                                     {
                                                         if (customer.isActiveAndEnabled && customer.name.ToLower() == username.ToLower())
                                                         {
                                                             string lowerChatMessage = chatMessage.ToLower();


                                                             CSingleton<PricePopupSpawner>.Instance.ShowTextPopup(chatMessage, 1.8f, customer.transform);
                                                         }
                                                     }
                                                 }
                                                */
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
                if (twitchWriter != null && twitchChannel.Length >= 1)
                {
                    twitchWriter.WriteLine("PART #" + twitchChannel);
                    twitchWriter.Flush();
                    twitchWriter.Close();
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
        private const string MESSAGE_TAG = "</b>";

        public static Dictionary<string, string> customerDictionary = new Dictionary<string, string>();
        
        public static void AddOrUpdateCustomer(string customerNetID, string customerName)
        {
            // Ensure the name is at least 1 character
            if (customerName.Length >= 1)
            {
                if (customerDictionary.ContainsKey(customerNetID))
                {
                    // Update existing entry
                    customerDictionary[customerNetID] = customerName;
                    Debug.Log("Updated customer with NetID: " + customerNetID);
                }
                else
                {
                    // Add new entry
                    customerDictionary.Add(customerNetID, customerName);
                    Debug.Log("Added new customer with NetID: " + customerNetID);
                }
            }
        }

        public static List<string> GetKeysByValue(string customerName)
        {
            // Create a list to store the matching keys
            List<string> matchingKeys = new List<string>();

            // Iterate over the dictionary to find all keys that have the specified value
            foreach (var entry in customerDictionary)
            {
                if (entry.Value == customerName)
                {
                    matchingKeys.Add(entry.Key);  // Add the key to the list if the value matches
                }
            }

            return matchingKeys;
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


        public static void SendSpawnCustomer(string customerName, string networkID)
        {

            ConnectToTwitchChat();
            string messageID = Guid.NewGuid().ToString();
            Instance.pendingMessageIDs.Add(messageID);

            var versionMessage = new
            {
                type = "CMD",
                command = "SPAWN_CUS",
                arg1 = customerName,
                arg2 = networkID,
                messageID = messageID,
                tag = MESSAGE_TAG
            };

            string jsonMessage = JsonConvert.SerializeObject(versionMessage);
            Instance.SendChatMessage(jsonMessage, "CMD");
        }


        private static void SendVersionCheck()
        {
            string messageID = Guid.NewGuid().ToString();
            Instance.pendingMessageIDs.Add(messageID);

            var versionMessage = new
            {
                type = "CMD",
                command = "VERSION",
                version = modVersion,
                messageID = messageID,
                tag = MESSAGE_TAG
            };

            string jsonMessage = JsonConvert.SerializeObject(versionMessage);
            Instance.SendChatMessage(jsonMessage, "CMD");
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
            
                var jsonMessage = JsonConvert.DeserializeObject<JsonMessage>(message);

                if (jsonMessage.type == null || jsonMessage.command == null || jsonMessage.messageID == null || jsonMessage.tag == null)
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
                    default:
                        mls.LogWarning($"Unknown message type from {playerName}: {jsonMessage.type}");
                        return;
                }
            }
            catch (Exception ex)
            {
                TestMod.mls.LogError($"Error processing message: {ex.Message}");
            }
        }

       
        private static bool IsValidJson(string json)
        {
            try
            {
                JsonConvert.DeserializeObject(json);
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
                        tag = "</b>"
                    };

                    var settings = new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    };

                    mls.LogInfo($"Processing version check for: {playerName} Version: {jsonMessage.version} Matched: {versionMatched}");

                    string jsonResponse = JsonConvert.SerializeObject(response, settings);
                    Instance.SendChatMessage(jsonResponse, "RSP");

                    
                    break;
                case "SPAWN_CUS":


                    string customerName = jsonMessage.arg1;
                    string customerNetID = jsonMessage.arg2;

                    if (customerName.Length >= 1)
                    {
                        AddOrUpdateCustomer(customerNetID, customerName);
                        if (!isHost) return;
                        mls.LogInfo($"Spawning Customer: {customerNetID} customerName: {customerName} ");
                        NPC_Manager npcManager = NPC_Manager.FindFirstObjectByType<NPC_Manager>();

                        float num = 5f - (float)(GameData.Instance.gameDay + GameData.Instance.difficulty + NetworkServer.connections.Count) * 0.05f;
                        float num2 = 12f - (float)(GameData.Instance.gameDay + GameData.Instance.difficulty + NetworkServer.connections.Count) * 0.12f;
                        num = Mathf.Clamp(num, 2f, float.PositiveInfinity);
                        num2 = Mathf.Clamp(num2, 4f, float.PositiveInfinity);

                        int gameDay = GameData.Instance.gameDay;
                        float num3;
                        if (NetworkServer.connections.Count <= 1)
                        {
                            num3 = ((float)gameDay - 7f) * 0.05f + (float)GameData.Instance.difficulty * 0.1f;
                            num3 = Mathf.Clamp(num3, 0f, 1.25f + (float)GameData.Instance.difficulty);
                        }
                        else
                        {
                            num3 = ((float)gameDay - 7f) * 0.15f + (float)GameData.Instance.difficulty * 0.15f;
                            num3 = Mathf.Clamp(num3, 0f, 4f + (float)GameData.Instance.difficulty + (float)NetworkServer.connections.Count);
                        }
                        Vector3 position = npcManager.spawnPointsOBJ.transform.GetChild(UnityEngine.Random.Range(0, npcManager.spawnPointsOBJ.transform.childCount - 1)).transform.position;
                        GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(npcManager.npcAgentPrefab, position, Quaternion.identity);
                        gameObject.transform.SetParent(npcManager.customersnpcParentOBJ.transform);

                        NPC_Info npc = gameObject.GetComponent<NPC_Info>();
                        npc.NetworkNPCID = int.Parse(customerNetID);
                        npc.productItemPlaceWait = Mathf.Clamp(0.5f - (float)GameData.Instance.gameDay * 0.003f, 0.1f, 0.5f);
                        npc.name = customerName;



                        NetworkServer.Spawn(gameObject, (NetworkConnection)null);


                        int num5 = UnityEngine.Random.Range(2 + GameData.Instance.difficulty, GameData.Instance.maxProductsCustomersToBuy);
                        for (int i = 0; i < num5; i++)
                        {
                            int item = ProductListing.Instance.availableProducts[UnityEngine.Random.Range(0, ProductListing.Instance.availableProducts.Count)];
                            npc.productsIDToBuy.Add(item);
                        }


                        NavMeshAgent npc2 = gameObject.GetComponent<NavMeshAgent>();
                        npc2.enabled = true;
                        npc2.stoppingDistance = 1f;
                        npc2.speed = 1.9f + (float)Mathf.Clamp(GameData.Instance.gameDay - 7, 0, 40) * 0.07f + (float)NetworkServer.connections.Count * 0.1f + (float)GameData.Instance.difficulty * 0.2f;


                        Vector3 position2 = npcManager.shelvesOBJ.transform.GetChild(UnityEngine.Random.Range(0, npcManager.shelvesOBJ.transform.childCount - 1)).Find("Standspot").transform.position;
                        npc2.destination = position2;


                    }





                    break;

                default:
                    mls.LogWarning($"Unknown command: {jsonMessage.command}");
                    break;
            }
        }

        private static void ProcessResponse(JsonMessage jsonMessage, string playerName)
        {
            switch (jsonMessage.command)
            {
                case "VERSION":
                    //If we are already valid, we don't need to send this again.
                    //This will get set to false when we leave a server
                    if (validVersion) return;
                    bool versionMatched = bool.TryParse(jsonMessage.response, out versionMatched);

                    if (Instance.pendingMessageIDs.Remove(jsonMessage.messageID)) {
                        versionResponse = true;
                        if (versionMatched) {
                            validVersion = true;
                            mls.LogInfo($"Version Matched! Ready for Effects.");
                        } else {
                            validVersion = false;
                            mls.LogInfo($"Version Mismatch! Make sure mod version matches.");
                        }

                    }
   
                    break;
                // Add other response types here
                default:
                    mls.LogWarning($"Unknown response command: {jsonMessage.command}");
                    break;
            }
        }
        private void SendChatMessage(string message, string cmdType)
        {

            
            if (NetworkClient.isConnected && NetworkClient.connection?.identity != null)
            {
                var playerController = NetworkClient.connection.identity.GetComponent<PlayerObjectController>();
                if (playerController != null)
                {
                    var settings = new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    };
                    string jsonMessage = JsonConvert.SerializeObject(message, settings);
                    //mls.LogInfo($"Sending {cmdType} message: {jsonMessage}");
                    playerController.SendChatMsg(message);
                }
                else
                {
                    mls.LogError("PlayerObjectController not found on player object");
                }
            }
            else
            {
                mls.LogWarning("Cannot send chat message: Not connected to server or connection not ready");
            }
        }

        public static Queue<Action> ActionQueue = new Queue<Action>();

        [HarmonyPatch(typeof(PlayerNetwork), "Update")]
        [HarmonyPrefix]
        static void RunEffects()
        {
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


        [HarmonyPatch(typeof(NPC_Manager), "FixedUpdate")]
        public class NPC_Manager_FixedUpdatePatch
        {
            static void Postfix(NPC_Manager __instance)
            {
                LogAllChildObjects(__instance.customersnpcParentOBJ);
               // mls.LogInfo("SUP");
            }

            static void LogAllChildObjects(GameObject parentObject)
            {
                if (parentObject == null)
                {
                   // mls.LogInfo("NO PARENTS");
                    return;
                }

                int childCount = parentObject.transform.childCount;

                // If there are no child objects
                if (childCount == 0)
                {
                   // mls.LogInfo("NO CHILDREN BUMMER");

                    return;
                }

                // Loop through each child of the parent object
                for (int i = 0; i < childCount; i++)
                {
                    Transform childTransform = parentObject.transform.GetChild(i);


                    NPC_Info npc = childTransform.gameObject.GetComponent<NPC_Info>();

                    if (npc.NetworkNPCID >= 0)
                    {
                       // mls.LogInfo("NetworkNPCID:" + npc.NetworkNPCID);

                    }


                    if (customerDictionary.TryGetValue(npc.NetworkNPCID.ToString(), out string foundCustomerName))
                    {
                       // mls.LogInfo("Customer Name: " + foundCustomerName);
                        npc.name = foundCustomerName;
                    }
                    

                    //if (!npc.name.ToString().Contains("(Clone)"))
                    //{

                        //mls.LogInfo("Name:" + npc.name);


                        if (childTransform.transform.Find("NamePlate") != null)
                        {
                            //return;
                        }


                        GameObject namePlate = new GameObject("NamePlate");
                        namePlate.transform.SetParent(childTransform);
                        namePlate.transform.localPosition = Vector3.up * 1.9f;

                        TextMeshPro tmp = namePlate.AddComponent<TextMeshPro>();
                        tmp.text = $"<b>{npc.name}</b>";
                        tmp.alignment = TextAlignmentOptions.Center;
                        tmp.fontSize = 1;
                        tmp.fontMaterial.EnableKeyword("OUTLINE_ON");
                        tmp.outlineColor = Color.black;
                        tmp.outlineWidth = 0.2f;
                        namePlate.AddComponent<NamePlateController>();

                    //}

                }
            }
        }

        [HarmonyPatch(typeof(NPC_Info), "UserCode_RPCNotificationAboveHead__String__String")]
        public class Patch_UserCode_RPCNotificationAboveHead
        {
            // Prefix runs before the original method
            static bool Prefix(NPC_Info __instance, string message1, string messageAddon)
            {
                // Check if messageAddon is "crowdcontrol"
                if (messageAddon == "crowdcontrol")
                {
                    // Instantiate the messagePrefab
                    GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(__instance.messagePrefab, __instance.transform.position + Vector3.up * 1.8f, Quaternion.identity);

                    // Directly set text to message1
                    string text = message1;

                    // Set the text to the instantiated object's TextMeshPro component
                    gameObject.GetComponent<TextMeshPro>().text = text;

                    // Activate the instantiated object
                    gameObject.SetActive(true);

                    // Skip the original method (returning false means original method won't run)
                    return false;
                }

                // Continue running the original method for other cases
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