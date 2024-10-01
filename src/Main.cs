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
 

        public static void ResetPackChecks()
        {
            validVersion = false;
            isHost = false;
            versionResponse = false;
            ranVersionCheck = false;
        }
        private const string MESSAGE_TAG = "</b>";

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
                            return true;
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
                        if (isHost)
                        {
                            ProcessCommand(jsonMessage, playerName);
                        }
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

        }

        private static void ProcessCommand(JsonMessage jsonMessage, string playerName)
        {

            mls.LogInfo($"Running command for {playerName}");
            switch (jsonMessage.command)
            {
                case "VERSION":
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

                // addd other command types here
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