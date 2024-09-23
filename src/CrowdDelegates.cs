using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Mirror;
using HutongGames.PlayMaker;
using System.Runtime.Remoting.Channels;
using HighlightPlus;
using UnityEngine.UI;
using HutongGames.PlayMaker.Actions;
using HeathenEngineering.SteamworksIntegration;
using UnityEngine.Localization.SmartFormat.Utilities;


namespace BepinControl
{
    public delegate CrowdResponse CrowdDelegate(ControlClient client, CrowdRequest req);



    public static class CustomerChatNames
    {
        private static Dictionary<int, string> chatNames = new Dictionary<int, string>();

        public static void SetChatName(int customerId, string name)
        {
            chatNames[customerId] = name;
        }

        public static string GetChatName(int customerId)
        {
            if (chatNames.TryGetValue(customerId, out string name))
            {
                return name;
            }
            return null; // Or a default name
        }
    }



    public class CrowdDelegates
    {
        public static System.Random rnd = new System.Random();
        public static int maxBoxCount = 100;

        public static CrowdResponse TurnOnLights(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            GameData gd = GameData.Instance;
            try
            {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        foreach (Transform item in gd.lightsOBJ.transform)
                        {
                            item.transform.Find("StreetLight").GetComponent<MeshRenderer>().material = gd.lightsOn;
                            item.transform.Find("Light_1").gameObject.SetActive(value: true);
                            item.transform.Find("Light_2").gameObject.SetActive(value: true);
                        }
                    });
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }

        public static CrowdResponse AlterFunds(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            string[] Amount = req.code.Split('_');
            GameData GD = GameData.Instance;
            int Money = 0;
            if(Amount.Length == 2)
            {
                Money = int.Parse(Amount[1]);
            }
            if (Amount[0].StartsWith("take") && GD.gameFunds < Money) status = CrowdResponse.Status.STATUS_RETRY;

            try
            {
                if (Amount[0].StartsWith("give"))
                {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        GD.CmdAlterFundsWithoutExperience(Money);
                    });
                }
                if (Amount[0].StartsWith("take"))
                {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        GD.CmdAlterFundsWithoutExperience(-Money);
                    });
                }
            }
            catch(Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }

        public static CrowdResponse GiveExtraEmployee(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            NPC_Manager NPC = NPC_Manager.Instance;
            GameData gd = GameData.Instance;
            try
            {
                if (NPC.maxEmployees == NPC.NPCsEmployeesArray.Length) return new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_RETRY, "Too many Employees");
                else
                {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        NPC.maxEmployees++;
                        NPC.UpdateEmployeesNumberInBlackboard();
                    });
                }
            }
            catch (Exception e)
            {

            }
            return new CrowdResponse(req.GetReqID(), status, message);
        }

        public static CrowdResponse GiveItem(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            ManagerBlackboard NPC = GameObject.FindFirstObjectByType<ManagerBlackboard>();
            int give = 0;
            string[] enteredText = req.code.Split('_');
            if(enteredText.Length == 2)
            {
                try
                {
                    give = int.Parse(enteredText[1]);
                }
                catch
                {

                }
            }
            else
            {
                return new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_FAILURE);
            }
            try
            {
                TestMod.ActionQueue.Enqueue(() =>
                {
                    NPC.CmdSpawnBoxFromPlayer(NPC.merchandiseSpawnpoint.transform.position, give, 32, 0f) ;
                });
            }
            catch (Exception e)
            {

            }
            return new CrowdResponse(req.GetReqID(), status, message);
        }
        public static CrowdResponse OpenSuper(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            GameData gd = GameData.Instance;
            try
            {
                if (gd.isSupermarketOpen == true) return new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_RETRY, "Store Already Open");
                else
                {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        gd.isSupermarketOpen = true;
                        gd.NetworkisSupermarketOpen = true;
                        gd.CmdOpenSupermarket();
                    });
                }
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }
        public static CrowdResponse ChangeSuperName(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            NetworkSpawner NS = NetworkSpawner.FindFirstObjectByType<NetworkSpawner>();
            string[] enteredText = req.code.Split('_');
            int amount = 0;
            string newName = null;
            try
            {
                amount = int.Parse(enteredText[1]);
                switch (amount)
                {
                    case 1: newName = "CrowdControlStore"; break;
                    case 2: newName = "Streamer Megastore"; break;
                    case 3: newName = "WarpWorld Store"; break;
                }
            }
            catch
            {
                return new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_FAILURE, "WHERES THE MONEY");
            }
            try
            {
                TestMod.ActionQueue.Enqueue(() =>
                {
                    NS.CmdSetSupermarketText(newName);
                });
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }
        public static CrowdResponse Give1FP(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            GameData gd = GameData.Instance;
            try
            {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        gd.gameFranchisePoints = gd.gameFranchisePoints + 1;
                        gd.UIFranchisePointsOBJ.text = gd.gameFranchisePoints.ToString();
                    });
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }
        public static void setProperty(System.Object a, string prop, System.Object val)
        {
            var f = a.GetType().GetField(prop, BindingFlags.Instance | BindingFlags.NonPublic);
            f.SetValue(a, val);
        }

        public static System.Object getProperty(System.Object a, string prop)
        {
            var f = a.GetType().GetField(prop, BindingFlags.Instance | BindingFlags.NonPublic);
            return f.GetValue(a);
        }

        public static void setSubProperty(System.Object a, string prop, string prop2, System.Object val)
        {
            var f = a.GetType().GetField(prop, BindingFlags.Instance | BindingFlags.NonPublic);
            var f2 = f.GetType().GetField(prop, BindingFlags.Instance | BindingFlags.NonPublic);
            f2.SetValue(f, val);
        }

        public static void callSubFunc(System.Object a, string prop, string func, System.Object val)
        {
            callSubFunc(a, prop, func, new object[] { val });
        }

        public static void callSubFunc(System.Object a, string prop, string func, System.Object[] vals)
        {
            var f = a.GetType().GetField(prop, BindingFlags.Instance | BindingFlags.NonPublic);


            var p = f.GetType().GetMethod(func, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            p.Invoke(f, vals);

        }

        public static void callFunc(System.Object a, string func, System.Object val)
        {
            callFunc(a, func, new object[] { val });
        }

        public static void callFunc(System.Object a, string func, System.Object[] vals)
        {
            var p = a.GetType().GetMethod(func, BindingFlags.Instance | BindingFlags.NonPublic);
            p.Invoke(a, vals);

        }

        public static System.Object callAndReturnFunc(System.Object a, string func, System.Object val)
        {
            return callAndReturnFunc(a, func, new object[] { val });
        }

        public static System.Object callAndReturnFunc(System.Object a, string func, System.Object[] vals)
        {
            var p = a.GetType().GetMethod(func, BindingFlags.Instance | BindingFlags.NonPublic);
            return p.Invoke(a, vals);

        }

    }
}
