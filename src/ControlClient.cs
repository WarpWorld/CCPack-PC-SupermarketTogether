/*
 * ControlValley
 * Stardew Valley Support for Twitch Crowd Control
 * Copyright (C) 2021 TerribleTable
 * LGPL v2.1
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301
 * USA
 */


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;


namespace BepinControl
{
    public class ControlClient
    {
        public static readonly string CV_HOST = "127.0.0.1";
        public static readonly int CV_PORT = 51337;

        private Dictionary<string, CrowdDelegate> Delegate { get; set; }
        private IPEndPoint Endpoint { get; set; }
        private Queue<CrowdRequest> Requests { get; set; }
        private bool Running { get; set; }

        private bool paused = false;
        public static Socket Socket { get; set; }

        public bool inGame = true;

        public ControlClient()
        {
            Endpoint = new IPEndPoint(IPAddress.Parse(CV_HOST), CV_PORT);
            Requests = new Queue<CrowdRequest>();
            Running = true;
            Socket = null;

            Delegate = new Dictionary<string, CrowdDelegate>()
            {
                //when an effect comes in with the code it will call the paired function
                {"lighton", CrowdDelegates.TurnOnLights },
                {"givemoney_100", CrowdDelegates.AlterFunds },
                {"givemoney_1000", CrowdDelegates.AlterFunds },
                {"givemoney_10000", CrowdDelegates.AlterFunds },
                {"takemoney_100", CrowdDelegates.AlterFunds },
                {"takemoney_1000", CrowdDelegates.AlterFunds },
                {"takemoney_10000", CrowdDelegates.AlterFunds },
                {"open_super", CrowdDelegates.OpenSuper },
                {"storename_1", CrowdDelegates.ChangeSuperName },
                {"storename_2", CrowdDelegates.ChangeSuperName },
                {"storename_3", CrowdDelegates.ChangeSuperName },
                {"give1fp", CrowdDelegates.Give1FP },
                {"give_0", CrowdDelegates.GiveItem },
                {"give_1", CrowdDelegates.GiveItem },
                {"give_2", CrowdDelegates.GiveItem },
                {"give_3", CrowdDelegates.GiveItem },
                {"give_4", CrowdDelegates.GiveItem },
                {"give_5", CrowdDelegates.GiveItem },
                {"give_6", CrowdDelegates.GiveItem },
                {"give_7", CrowdDelegates.GiveItem },
                {"give_8", CrowdDelegates.GiveItem },
                {"give_9", CrowdDelegates.GiveItem },
                {"give_10", CrowdDelegates.GiveItem },
                {"give_11", CrowdDelegates.GiveItem },
                {"give_12", CrowdDelegates.GiveItem },
                {"give_13", CrowdDelegates.GiveItem },
                {"give_43", CrowdDelegates.GiveItem },
                {"give_44", CrowdDelegates.GiveItem },
                {"give_45", CrowdDelegates.GiveItem },
                {"give_46", CrowdDelegates.GiveItem },
                {"give_47", CrowdDelegates.GiveItem },
                {"give_48", CrowdDelegates.GiveItem },
                {"give_61", CrowdDelegates.GiveItem },
                {"give_62", CrowdDelegates.GiveItem },
                {"give_63", CrowdDelegates.GiveItem },
                {"give_64", CrowdDelegates.GiveItem },
                {"give_65", CrowdDelegates.GiveItem },
                {"give_66", CrowdDelegates.GiveItem },
                {"give_67", CrowdDelegates.GiveItem },
                {"give_140", CrowdDelegates.GiveItem },
                {"give_141", CrowdDelegates.GiveItem },
                {"give_142", CrowdDelegates.GiveItem },
                {"give_143", CrowdDelegates.GiveItem },
                {"give_144", CrowdDelegates.GiveItem },
                {"give_145", CrowdDelegates.GiveItem },
                {"spawn_employee", CrowdDelegates.GiveExtraEmployee },
                {"spawn_customer", CrowdDelegates.SpawnCustomer },
                {"spawn_trash", CrowdDelegates.SpawnTrash },
                {"complain_filth", CrowdDelegates.ComplainAboutFilth },
            };
        }

        public bool isReady()
        {
            try
            {
                //make sure the game is in focus otherwise don't let effects trigger
                if (!TestMod.isFocused) return false;
                if (!TestMod.validVersion && !TestMod.isHost) return false;
                var player = GameObject.Find("LocalGamePlayer");
                if (player == null) return false;
            }
            catch (Exception e)
            {
                TestMod.mls.LogError(e.ToString());
                return false;
            }

            return true;
        }

        public static void HideEffect(string code)
        {
            CrowdResponse res = new CrowdResponse(0, CrowdResponse.Status.STATUS_NOTVISIBLE);
            res.type = 1;
            res.code = code;
            res.Send(Socket);
        }

        public static void ShowEffect(string code)
        {
            CrowdResponse res = new CrowdResponse(0, CrowdResponse.Status.STATUS_VISIBLE);
            res.type = 1;
            res.code = code;
            res.Send(Socket);
        }

        public static void DisableEffect(string code)
        {
            CrowdResponse res = new CrowdResponse(0, CrowdResponse.Status.STATUS_NOTSELECTABLE);
            res.type = 1;
            res.code = code;
            res.Send(Socket);
        }

        public static void EnableEffect(string code)
        {
            CrowdResponse res = new CrowdResponse(0, CrowdResponse.Status.STATUS_SELECTABLE);
            res.type = 1;
            res.code = code;
            res.Send(Socket);
        }

        private void ClientLoop()
        {

            TestMod.mls.LogInfo("Connected to Crowd Control");

            var timer = new Timer(timeUpdate, null, 0, 200);

            try
            {
                while (Running)
                {
                    CrowdRequest req = CrowdRequest.Recieve(this, Socket);
                    if (req == null || req.IsKeepAlive()) continue;

                    lock (Requests)
                        Requests.Enqueue(req);
                }
            }
            catch (Exception e)
            {
                TestMod.mls.LogInfo($"Disconnected from Crowd Control. {e.ToString()}");
                Socket.Close();
            }
        }

        public void timeUpdate(System.Object state)
        {
            inGame = true;

            if (!isReady()) inGame = false;

            if (!inGame)
            {
                TimedThread.addTime(200);
                paused = true;
            }
            else if (paused)
            {
                paused = false;
                TimedThread.unPause();
                TimedThread.tickTime(200);
            }
            else
            {
                TimedThread.tickTime(200);
            }
        }

        public bool IsRunning() => Running;

        public void NetworkLoop()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            while (Running)
            {

                TestMod.mls.LogInfo("Attempting to connect to Crowd Control");

                try
                {
                    Socket = new Socket(Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    if (Socket.BeginConnect(Endpoint, null, null).AsyncWaitHandle.WaitOne(10000, true) && Socket.Connected)
                        ClientLoop();
                    else
                        TestMod.mls.LogInfo("Failed to connect to Crowd Control");
                    Socket.Close();
                }
                catch (Exception e)
                {
                    TestMod.mls.LogInfo(e.GetType().Name);
                    TestMod.mls.LogInfo("Failed to connect to Crowd Control");
                }

                Thread.Sleep(10000);
            }
        }

        public void RequestLoop()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            while (Running)
            {
                try
                {

                    CrowdRequest req = null;
                    lock (Requests)
                    {
                        if (Requests.Count == 0)
                            continue;
                        req = Requests.Dequeue();
                    }

                    string code = req.GetReqCode();
                    try
                    {
                        CrowdResponse res;
                        if (!isReady())
                            res = new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_RETRY);
                        else
                            res = Delegate[code](this, req);
                        if (res == null)
                        {
                            new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_FAILURE, $"Request error for '{code}'").Send(Socket);
                        }

                        res.Send(Socket);
                    }
                    catch (KeyNotFoundException)
                    {
                        new CrowdResponse(req.GetReqID(), CrowdResponse.Status.STATUS_FAILURE, $"Request error for '{code}'").Send(Socket);
                    }
                }
                catch (Exception)
                {
                    TestMod.mls.LogInfo("Disconnected from Crowd Control");
                    Socket.Close();
                }
            }
        }

        public void Stop()
        {
            Running = false;
        }

    }
}
