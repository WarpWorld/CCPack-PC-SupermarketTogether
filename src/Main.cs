using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Assertions.Must;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;
using System.Collections;
using System.Security.AccessControl;
using BepInEx.Configuration;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;
using System.Threading;
using BepinControl;
using System.Runtime.InteropServices;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using TMPro;
using UnityEngine.EventSystems;



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

        public static int OrgLanguage = 0; 
        public static int NewLanguage = 0;


        public static string currentHeldItem;

        public static string NameOverride = "";
        public static List<GameObject> nameplates = new List<GameObject>();


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
        }


        public class CustomGUIMessages : MonoBehaviour
        {
            public enum Language
            {
                English = 0,
                French = 1,
                German = 2,
                Italian = 3,
                Spanish = 4,
                Portugal = 5,
                Brazil = 6,
                Netherlands = 7,
                Turkey = 8
            }

            private readonly Dictionary<string, Dictionary<Language, string>> flagMessages = new Dictionary<string, Dictionary<Language, string>>
            {
                {
                    "ForceUseCash", new Dictionary<Language, string>
                    {
                        { Language.English, "All customers only have cash." },
                        { Language.French, "Tous les clients n'ont que de l'argent liquide." },
                        { Language.German, "Alle Kunden haben nur Bargeld." },
                        { Language.Italian, "Tutti i clienti hanno solo contanti." },
                        { Language.Spanish, "Todos los clientes solo tienen efectivo." },
                        { Language.Portugal, "Todos os clientes só têm dinheiro." },
                        { Language.Brazil, "Todos os clientes só têm dinheiro." },
                        { Language.Netherlands, "Alle klanten hebben alleen contant geld." },
                        { Language.Turkey, "Tüm müşterilerin sadece nakit parası var." }
                    }
                },
                {
                    "ForceUseCredit", new Dictionary<Language, string>
                    {
                        { Language.English, "All customers only have card." },
                        { Language.French, "Tous les clients n'ont que des cartes." },
                        { Language.German, "Alle Kunden haben nur Karten." },
                        { Language.Italian, "Tutti i clienti hanno solo carta." },
                        { Language.Spanish, "Todos los clientes solo tienen tarjeta de credito." },
                        { Language.Portugal, "Todos os clientes só têm cartão." },
                        { Language.Brazil, "Todos os clientes só têm cartão." },
                        { Language.Netherlands, "Alle klanten hebben alleen een kaart." },
                        { Language.Turkey, "Tüm müşterilerin sadece kartı var." }
                    }
                },
                {
                    "ForceExactChange", new Dictionary<Language, string>
                    {
                        { Language.English, "All customers will pay in exact change." },
                        { Language.French, "Tous les clients paieront avec l'appoint exact." },
                        { Language.German, "Alle Kunden zahlen mit genauem Wechselgeld." },
                        { Language.Italian, "Tutti i clienti pagheranno con il resto esatto." },
                        { Language.Spanish, "Todos los clientes pagarán con el cambio exacto." },
                        { Language.Portugal, "Todos os clientes pagarão com o troco exato." },
                        { Language.Brazil, "Todos os clientes pagarão com o troco exato." },
                        { Language.Netherlands, "Alle klanten betalen met precies wisselgeld." },
                        { Language.Turkey, "Tüm müşteriler tam para üstüyle ödeyecek." }
                    }
                },
                {
                    "ForceRequireChange", new Dictionary<Language, string>
                    {
                        { Language.English, "All customers will not pay in exact change." },
                        { Language.French, "Tous les clients ne paieront pas avec l'appoint exact." },
                        { Language.German, "Alle Kunden werden nicht mit dem genauen Betrag bezahlen." },
                        { Language.Italian, "Tutti i clienti non pagheranno con il resto esatto." },
                        { Language.Spanish, "Todos los clientes no pagarán con el cambio exacto." },
                        { Language.Portugal, "Todos os clientes não pagarão com o troco exato." },
                        { Language.Brazil, "Todos os clientes não vão pagar com o troco exato." },
                        { Language.Netherlands, "Niet alle klanten zullen met het exacte wisselgeld betalen." },
                        { Language.Turkey, "Tüm müşteriler tam para üstü ile ödeme yapmayacak." }
                    }
                },
                {
                    "AllowMischarge", new Dictionary<Language, string>
                    {
                        { Language.English, "You can currently overcharge card payments." },
                        { Language.French, "Vous pouvez actuellement surcharger les paiements par carte." },
                        { Language.German, "Sie können derzeit Kartenzahlungen überladen." },
                        { Language.Italian, "Attualmente puoi addebitare eccessivamente i pagamenti con carta." },
                        { Language.Spanish, "Actualmente puedes sobrecargar los pagos con tarjeta de credito." },
                        { Language.Portugal, "Atualmente, você pode sobrecarregar os pagamentos com cartão." },
                        { Language.Brazil, "Atualmente, você pode sobrecarregar os pagamentos com cartão." },
                        { Language.Netherlands, "U kunt momenteel kaartbetalingen te veel in rekening brengen." },
                        { Language.Turkey, "Şu anda kart ödemelerinden fazla ücret alabilirsiniz." }
                    }
                }
            };

            private List<string> activeMessages = new List<string>();

            void Update()
            {
                UpdateActiveMessages();
            }

            void UpdateActiveMessages()
            {
                if (CurrentLanguage > 8) CurrentLanguage = 0;
                Language currentLanguage = (Language)CurrentLanguage;

                activeMessages.Clear();
            }

            void OnGUI()
            {
                GUIStyle guiStyle = new GUIStyle();
                guiStyle.fontSize = 14;

                int yOffset = 0; // Vertical offset for each message
                foreach (string message in activeMessages)
                {
                    GUI.Label(new Rect(10, 10 + yOffset, 300, 50), message, guiStyle);
                    yOffset += 20; // Increase the offset for the next message
                }
            }
        }


        public static Queue<Action> ActionQueue = new Queue<Action>();

        //attach this to some game class with a function that runs every frame like the player's Update()
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


        [HarmonyPatch(typeof(PlayerNetwork), "Start")]
        public static class AddCustomGUIClassPatch
        {
            static void Postfix(PlayerNetwork __instance)
            {
                if (__instance.gameObject.GetComponent<CustomGUIMessages>() == null) __instance.gameObject.AddComponent<CustomGUIMessages>();
            }
        }


    }

}
