﻿using BepInEx;
using HutongGames.PlayMaker.Actions;
using J4F;
using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
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


        public AssetBundle bundle; // Make sure this is assigned when the plugin loads

        private static GameObject hypetrainPrefab;


        // Static flag to ensure assets are loaded only once
        private static bool loaded = false;

        // Load all assets from the bundle and store them
        public void LoadAssetsFromBundle()
        {
            if (loaded) return; // Only load once

            //TestMod.mls.LogDebug("PATH " + System.IO.Path.Combine(Paths.PluginPath, "CrowdControl", "food"));

  

     

            HypeTrainBoxData boxData = new HypeTrainBoxData();// Do this to load the dll... maybe do something different, but this works for now
            bundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(Paths.PluginPath, "CrowdControl", "warpworld.hypetrain"));
            if (bundle == null)
            {
                Debug.LogError("Failed to load AssetBundle.");
                return;
            }

            hypetrainPrefab = bundle.LoadAsset<GameObject>("HypeTrain");

            if (hypetrainPrefab == null)
            {
                Debug.LogError("hypetrain prefab not found in AssetBundle.");
            }

            loaded = true;
        }

        public static Color ConvertUserNameToColor(string userName)
        {

            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(userName));

                float r = hashBytes[0] / 255f;
                float g = hashBytes[1] / 255f;
                float b = hashBytes[2] / 255f;

                return new Color(r, g, b);
            }
        }

        public static void Spawn_HypeTrain(CrowdRequest.SourceDetails sourceDetails, Vector3 playerPosition, Quaternion playerRotation, Vector3 playerForward, Transform playerTransform)
        {
            TestMod.ActionQueue.Enqueue(() =>
            {

                if (sourceDetails.top_contributions.Length == 0)
                {
                    TestMod.mls.LogInfo("No top_contributions?");
                    return;
                }


                CrowdDelegates crowdDelegatesInstance = new CrowdDelegates();
                crowdDelegatesInstance.LoadAssetsFromBundle();

                // Calculate the left direction using the player's right vector
                Vector3 leftDirection = -playerTransform.right;  // Get the player's left direction by negating the right direction
                float leftOffset = 5f;  // Distance to the left of the player
                float heightOffset = 0.5f;  // Set a small positive offset to ensure it is not underground

                // The final spawn position will be to the left of the player and slightly above the ground level
                Vector3 spawnPosition = playerPosition + leftDirection * leftOffset + new Vector3(0, heightOffset, 0);

                // Ensure the rotation is aligned with the player's forward direction
                Quaternion spawnRotation = Quaternion.LookRotation(playerForward, Vector3.up);  // Align the train's forward direction with the player's forward direction


                if (hypetrainPrefab != null)
                {
                    // Instantiate the HypeTrain at the calculated position with the adjusted rotation
                    HypeTrain hypeTrain = UnityEngine.Object.Instantiate(hypetrainPrefab, spawnPosition, spawnRotation).GetComponent<HypeTrain>();
                    if (hypeTrain == null)
                    {
                        TestMod.mls.LogInfo("No Train?");
                        return;
                    }

                    // Setup the hype train data (contributions)
                    List<HypeTrainBoxData> hypeTrainBoxDataList = new List<HypeTrainBoxData>();
                    foreach (var contribution in sourceDetails.top_contributions)
                    {
                        hypeTrainBoxDataList.Add(new HypeTrainBoxData()
                        {
                            name = contribution.user_name,
                            box_color = ConvertUserNameToColor(contribution.user_name),
                            bit_amount = contribution.type == "bits" ? contribution.total : 0
                        });
                    }

                    bool isLastContributionInTop = sourceDetails.last_contribution != null;

                    if (isLastContributionInTop)
                    {
                        hypeTrainBoxDataList.Add(new HypeTrainBoxData()
                        {
                            name = sourceDetails.last_contribution.user_name,
                            box_color = ConvertUserNameToColor(sourceDetails.last_contribution.user_name),
                            bit_amount = sourceDetails.last_contribution.type == "bits" ? sourceDetails.last_contribution.total : 0
                        });
                    }

                    // Set speed of the Hype Train based on the level
                    float defaultSpeed = 1f;
                    float speedIncrease = sourceDetails.level * 0.1f;
                    float distance_per_second = Mathf.Min(defaultSpeed + speedIncrease, 10f);

                    // Define the stop position to be in front of the player, using their forward direction
                    Vector3 stopPosition = playerPosition + playerForward * 10;

                    // Start the HypeTrain moving from the left spawn position to the stop position
                    hypeTrain.StartHypeTrain(spawnPosition, stopPosition, hypeTrainBoxDataList.ToArray(), playerTransform,
                    new HypeTrainOptions()
                    {
                        max_bits_per_car = 100,
                        distance_per_second = distance_per_second
                    });

                    TestMod.mls.LogInfo($"Spawned Hype Train at level {sourceDetails.level}!");
                    TestMod.SendHudMessage($"Level {sourceDetails.level} Hype Train!", "green");
                }
            });
        }








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


                        gd.timeOfDay = gd.timeOfDay + 1f;

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
            TestMod.mls.LogInfo($"Has {GD.gameFunds.ToString()} give/take: {Money}");
            if (Amount[0].StartsWith("take") && GD.gameFunds < Money)
            {
                return new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_RETRY, "");
            }

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
                    TestMod.SendHudMessage($"{req.viewer} just spawned some inventory!", "green");

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
                            TestMod.JailPlayer(req.id, playerInfo.PlayerSteamID, req.viewer);
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



        public static CrowdResponse ForceMath(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";

            try
            {

                
                TestMod.ActionQueue.Enqueue(() =>
                {
                    TestMod.forceMath = true;
                    // this doenst turn off yet, need to make it timed and needs proper checks
                });

                
                


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


                if (tcs.Task.Result.ToString() == "STATUS_SUCCESS")
                {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        TestMod.SendHudMessage($"{req.viewer} just threw some trash on the ground!", "red");
                    });
                }

                TestMod.RemoveResponder(req.id);
               
                
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
                    
                });

   
                status = tcs.Task.Wait(SERVER_TIMEOUT) ? tcs.Task.Result : CrowdResponse.Status.STATUS_RETRY;
                if (tcs.Task.Result.ToString() == "STATUS_SUCCESS")
                {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        TestMod.SendHudMessage($"{req.viewer} spawned a employee!", "green");
                    });
                }
                TestMod.RemoveResponder(req.id);
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }

        public static CrowdResponse SpawnHypeTrain(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";


            PlayerObjectController playerRef = LobbyController.FindFirstObjectByType<LobbyController>().LocalplayerController;
            Vector3 position = playerRef.transform.position;
            Quaternion rotation = playerRef.transform.rotation;
            Transform playerCamera = Camera.main?.transform ?? UnityEngine.Object.FindObjectOfType<Camera>()?.transform;
            Vector3 forwardDirection = playerCamera.forward;


            if (!playerRef || !playerCamera) return new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_FAILURE, "Unable to spawn hype train.");

            var serverObjects = NetworkClient.spawned;
            PlayerObjectController localPlayerInfo = null;

            foreach (var kvp in serverObjects)
            {
                NetworkIdentity serverIdentity = kvp.Value;

                if (serverIdentity.gameObject.name.Contains("Player"))
                {
                    PlayerObjectController playerInfo = serverIdentity.gameObject.GetComponentInChildren<PlayerObjectController>();

                    TestMod.mls.LogInfo($"player PlayerSteamID: {playerInfo.PlayerSteamID}");

                    if (playerInfo != null && serverIdentity.isLocalPlayer)  // Check if this is the local player
                    {
                        if (playerInfo.PlayerSteamID >= 1)
                        {
                            localPlayerInfo = playerInfo;
                            break;  // Exit the loop as we've found the local player
                        }
                    }
                }
            }

     

            if (!localPlayerInfo) return new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_FAILURE, "Unable to spawn hype train.");




            TestMod.SendSpawnTrain(req.id, localPlayerInfo.PlayerSteamID, req);

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

                            if (!TestMod.spawnedCustomers.Contains(req.viewer))
                            {
                                TestMod.spawnedCustomers.Add(req.viewer.ToLower());
                            }
                        }
                        else
                        {
                            TestMod.SendSpawnCustomer(req.id, req.viewer);
                        }
                    }

                });

                //this part that waits for the response from the server
                //DO NOT PUT THIS INSIDE ANYTHING PASSED TO ActionQueue.Enqueue
                //IT COULD EASILY DEADLOCK SOMETHING MAYBE DEPENDING ON THE GAME
                status = tcs.Task.Wait(SERVER_TIMEOUT) ? tcs.Task.Result : CrowdResponse.Status.STATUS_RETRY;


                if (tcs.Task.Result.ToString() == "STATUS_SUCCESS")
                {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        TestMod.SendHudMessage($"{req.viewer} spawned a customer!", "green");
                    });
                }

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
