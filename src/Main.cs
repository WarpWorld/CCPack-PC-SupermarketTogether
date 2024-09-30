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

namespace BepinControl
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class TestMod : BaseUnityPlugin
    {
        // Mod Details
        private const string modGUID = "WarpWorld.CrowdControl";
        private const string modName = "Crowd Control";
        private const string modVersion = "1.0.12.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        public static ManualLogSource mls;

        internal static TestMod Instance = null;
        private ControlClient client = null;
        public static bool isFocused = true;

        public static int CurrentLanguage = 0;
        public static bool hasPrintedItems = false;

        public static int OrgLanguage = 0;
        public static int NewLanguage = 0;

        public static string currentHeldItem;

        public static string NameOverride = "";
        public static List<GameObject> nameplates = new List<GameObject>();

        private const string CC_CMD_PREFIX = "CC_CMD:";
        private bool versionChecked = false;
        private bool versionMatched = false;

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

            mls = Logger;

            StartCoroutine(CheckForConnection());
        }

        private IEnumerator CheckForConnection()
        {
            mls.LogInfo("Starting connection check coroutine");
            while (true)
            {
                if (NetworkClient.isConnected && NetworkClient.connection != null && NetworkClient.connection.isReady)
                {
                    yield return new WaitForSeconds(10f);
                    StartCoroutine(CheckVersion());
                    yield break; 
                }
                yield return new WaitForSeconds(2f);
            }
        }

        public static Queue<Action> ActionQueue = new Queue<Action>();

        [HarmonyPatch(typeof(PlayerNetwork), "Update")]
        [HarmonyPrefix]
        static void RunEffects()
        {
            foreach (Transform item in ManagerBlackboard.FindFirstObjectByType<ManagerBlackboard>(FindObjectsInactive.Include).shopItemsParent.transform)
            {
                int productID = item.GetComponent<Data_Product>().productID;
                int productMax = item.GetComponent<Data_Product>().maxItemsPerBox;
                TestMod.mls.LogInfo("Product ID: " + productID + ", Max Per Box: " + productMax);
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

        private static bool CheckVersionPrefix(string playerName, string message)
        {
            if (message.StartsWith(CC_CMD_PREFIX))
            {
                string receivedVersion = message.Substring(CC_CMD_PREFIX.Length).Replace("</b>", "");
                Instance.versionMatched = (receivedVersion == modVersion);
                Instance.versionChecked = true;

                TestMod.mls.LogInfo($"{playerName} - Sent check: {receivedVersion}, Matched: {Instance.versionMatched}");

                return false;
            }
            TestMod.mls.LogInfo($"{playerName} : {message}");

            return true; 
        }

        private void SendVersionCheck()
        {
            string versionMessage = $"{CC_CMD_PREFIX}{modVersion}</b>";
            SendChatMessage(versionMessage);
        }

        private IEnumerator CheckVersion()
        {
        
            SendVersionCheck();
            float startTime = Time.time;

            while (!versionChecked && Time.time - startTime < 5f)
            {
                yield return null;
            }

            if (!versionChecked)
            {
                mls.LogWarning("Version check timed out");
            }
            else if (versionMatched)
            {
                mls.LogInfo("Version matched");
            }
            else
            {
                mls.LogWarning("Version mismatch detected");
            }
        }

        private void SendChatMessage(string message)
        {
            if (!NetworkClient.isConnected)
            {
                mls.LogWarning("Cannot send chat message: Not connected to server");
                return;
            }

            if (NetworkClient.connection == null)
            {
                mls.LogWarning("Cannot send chat message: NetworkClient.connection is null");
                return;
            }

            if (NetworkClient.connection.identity == null)
            {
                mls.LogWarning("Cannot send chat message: NetworkClient.connection.identity is null");
                return;
            }

            var playerController = NetworkClient.connection.identity.GetComponent<PlayerObjectController>();
            if (playerController == null)
            {
                mls.LogError("PlayerObjectController not found on player object");
                return;
            }

            playerController.SendChatMsg(message);
        }
    }
}