using HutongGames.PlayMaker.Actions;
using Mirror;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

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
        public static uint msgid = 0;
        
        public static readonly TimeSpan SERVER_TIMEOUT = TimeSpan.FromSeconds(5);

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
            if (Amount.Length == 2)
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
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }


        public static CrowdResponse ComplainAboutFilth(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            int item = UnityEngine.Random.Range(0, 190);
            NPC_Info NPC = GameObject.FindFirstObjectByType<NPC_Info>();
            GameData gd = GameData.Instance;
            float newPrice = UnityEngine.Random.Range(0, 30);
            if (!gd.isSupermarketOpen) return new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_RETRY, "Supermarket is Closed");
            else
                try
                {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        NPC.ComplainAboutFilth();
                    });
                }
                catch (Exception e)
                {
                    TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                    status = CrowdResponse.Status.STATUS_RETRY;
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
            if (enteredText.Length == 2)
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
                    NPC.CmdSpawnBoxFromPlayer(NPC.merchandiseSpawnpoint.transform.position, give, 32, 0f);
                    TestMod.SendHudMessage($"{req.viewer} just spawned some inventory!");

                  
                });
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }
            return new CrowdResponse(req.GetReqID(), status, message);
        }
        public static CrowdResponse GiveItemToPlayer(ControlClient client, CrowdRequest req)
        {

            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            PlayerObjectController playerRef = LobbyController.FindFirstObjectByType<LobbyController>().LocalplayerController;
            ManagerBlackboard MB = GameObject.FindFirstObjectByType<ManagerBlackboard>();
            int give = 0;
            string[] enteredText = req.code.Split('_');
            if (enteredText.Length == 2)
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
                    MB.CmdSpawnBoxFromPlayer(playerRef.transform.position, give, 32, 0f);
                });
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
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
        public static CrowdResponse CloseSuper(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            GameData gd = GameData.Instance;
            try
            {
                if (gd.isSupermarketOpen == false) return new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_RETRY, "Store Already Closed");
                else
                {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        gd.isSupermarketOpen = false;
                        gd.NetworkisSupermarketOpen = false;
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

        //ChangeSuperName
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
                return new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_FAILURE, "Unable to change name.");
            }
            try
            {
  
                TestMod.ActionQueue.Enqueue(() =>
                {
                    NS.CmdSetSupermarketText(newName);


                  

                   // TrashSpawn trashSpawn = TrashSpawn.FindFirstObjectByType<TrashSpawn>();
                    //trashSpawn.OnStartClient();
                    


                    TrashSpawn trashSpawn = TrashSpawn.FindObjectOfType<TrashSpawn>();
                    if (trashSpawn != null)
                    {
                        TestMod.mls.LogInfo("Spawn some trash?");
                        trashSpawn.OnStartClient();
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


     
        public static CrowdResponse JailPlayer(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";

            try
            {

                TaskCompletionSource<CrowdResponse.Status> tcs = new();
                TestMod.AddResponder(req.id, s => tcs.SetResult(s));

                TestMod.ActionQueue.Enqueue(() =>
                {
                    LobbyController lobbyController = LobbyController.Instance;

                    if (lobbyController != null)
                    {
                        PlayerObjectController playerInfo = lobbyController.LocalPlayerObject.GetComponentInChildren<PlayerObjectController>();
                        if (playerInfo != null)
                        {
                            TestMod.JailPlayer(req.id, playerInfo.PlayerName, req.viewer);
                        }
                    }
                });

                status = tcs.Task.Wait(SERVER_TIMEOUT) ? tcs.Task.Result : CrowdResponse.Status.STATUS_RETRY;
                TestMod.RemoveResponder(req.id);
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }

        public static CrowdResponse SpawnTrash(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";

            try
            {

                TaskCompletionSource<CrowdResponse.Status> tcs = new();
                TestMod.AddResponder(req.id, s => tcs.SetResult(s));

                TestMod.ActionQueue.Enqueue(() =>
                {
                    TestMod.SpawnTrash(req.id, req.viewer);
                });

                status = tcs.Task.Wait(SERVER_TIMEOUT) ? tcs.Task.Result : CrowdResponse.Status.STATUS_RETRY;
                TestMod.RemoveResponder(req.id);
                if (status.ToString() == "STATUS_SUCCESS")
                {
                    TestMod.SendHudMessage($"{req.viewer} spawned a employee!");
                }
            }
            catch (Exception e)
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

            try
            {

                TaskCompletionSource<CrowdResponse.Status> tcs = new();
                TestMod.AddResponder(req.id, s => tcs.SetResult(s));

                TestMod.ActionQueue.Enqueue(() =>
                {
                    TestMod.SpawnTrash(req.id, req.viewer);
                });

                status = tcs.Task.Wait(SERVER_TIMEOUT) ? tcs.Task.Result : CrowdResponse.Status.STATUS_RETRY;
                TestMod.RemoveResponder(req.id);
                
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }


        public static CrowdResponse GiveExtraEmployee2(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";

            try
            {
   
                TaskCompletionSource<CrowdResponse.Status> tcs = new();
                TestMod.AddResponder(req.id, s => tcs.SetResult(s));

                TestMod.ActionQueue.Enqueue(() =>
                {
                    if (req.targets != null)
                    {
                        if (req.targets[0].service == "twitch")
                        {
                            TestMod.SendSpawnEmployee(req.id, req.viewer, req.targets[0].name);
                        }
                        else
                        {
                            TestMod.SendSpawnEmployee(req.id, req.viewer);
                        }
                    }
                    TestMod.SendHudMessage($"{req.viewer} spawned a employee!");
                });

   
                status = tcs.Task.Wait(SERVER_TIMEOUT) ? tcs.Task.Result : CrowdResponse.Status.STATUS_RETRY;
                TestMod.RemoveResponder(req.id);
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }

        public static CrowdResponse SpawnCustomer(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";

            try
            {
                //this creates the callback context that should be created
                //before the message is sent out to the server
                TaskCompletionSource<CrowdResponse.Status> tcs = new();
                TestMod.AddResponder(req.id, s => tcs.SetResult(s));

                TestMod.ActionQueue.Enqueue(() =>
                {
                    if (req.targets != null)
                    {
                        if (req.targets[0].service == "twitch")
                        {
                            TestMod.SendSpawnCustomer(req.id, req.viewer, req.targets[0].name);
                        }
                        else
                        {
                            TestMod.SendSpawnCustomer(req.id, req.viewer);
                        }
                    }
                    TestMod.SendHudMessage($"{req.viewer} spawned a customer!");
                });

                //this part that waits for the response from the server
                //DO NOT PUT THIS INSIDE ANYTHING PASSED TO ActionQueue.Enqueue
                //IT COULD EASILY DEADLOCK SOMETHING MAYBE DEPENDING ON THE GAME
                status = tcs.Task.Wait(SERVER_TIMEOUT) ? tcs.Task.Result : CrowdResponse.Status.STATUS_RETRY;
                TestMod.RemoveResponder(req.id);
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
                    TestMod.UpdateFranchisePoints(req.id, req.viewer);
                });
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }

        public static void SendCCMessage(string message)
        {

            TestMod.ActionQueue.Enqueue(() =>
            {
                string value = ("<color=green>CrowdControl:</color> " + message);
                PlayMakerFSM chatFSM = LobbyController.Instance.ChatContainerOBJ.GetComponent<PlayMakerFSM>();
                chatFSM.FsmVariables.GetFsmString("Message").Value = value;
                chatFSM.SendEvent("Send_Data");
            });

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
