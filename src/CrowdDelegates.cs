using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UI;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;
using static UnityEngine.EventSystems.EventTrigger;
using static UnityEngine.GraphicsBuffer;
using JetBrains.Annotations;
using MyBox;
using Mirror;


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

        public static CrowdResponse TurnOnLights(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            GameData gd = GameData.Instance;
            try
            {
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

                }
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }

        public static CrowdResponse GiveCash100(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            GameData gd = GameData.Instance;
            try
            {
                try
                {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        gd.CmdAlterFunds(100.0f);
                    });
                }
                catch (Exception e)
                {

                }
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Crowd Control Error: {e.ToString()}");
                status = CrowdResponse.Status.STATUS_RETRY;
            }

            return new CrowdResponse(req.GetReqID(), status, message);
        }
        public static CrowdResponse GiveCash1000(ControlClient client, CrowdRequest req)
        {
            CrowdResponse.Status status = CrowdResponse.Status.STATUS_SUCCESS;
            string message = "";
            GameData gd = GameData.Instance;
            try
            {
                try
                {
                    TestMod.ActionQueue.Enqueue(() =>
                    {
                        gd.CmdAlterFunds(1000.0f);
                    });
                }
                catch (Exception e)
                {

                }
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

                }
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
