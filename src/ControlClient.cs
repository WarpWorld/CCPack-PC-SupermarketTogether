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
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using static BepinControl.CrowdResponse;


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
        
        private bool m_hasLoggedNoProcessFound = false;

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
                {"jailplayer", CrowdDelegates.JailPlayer },
                {"complain_filth", CrowdDelegates.ComplainAboutFilth },
                {"forcemath", CrowdDelegates.ForceMath },
                {"event-hype-train", CrowdDelegates.SpawnHypeTrain }

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


        public static void TimeoutUser(string twitchName, int duration, string reason )
        {

            if (Socket == null) return;
            var message = new GenericMessage(
                type: 16,
                internalFlag: true,
                eventType: "timeoutUser",
                data: new { twitchName, duration, reason });

            message.Send(Socket);

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

        /// <summary>
        /// Checks if any CrowdControl process is running.
        /// Looks for processes with names containing "crowdcontrol" (case-insensitive).
        /// Handles cases where processes might be running with different privilege levels.
        /// </summary>
        /// <returns>True if a CrowdControl process is found, false otherwise.</returns>
        private static bool IsCrowdControlProcessRunning()
        {
            try
            {
                var processes = Process.GetProcesses();
                int accessibleProcesses = 0;
                int inaccessibleProcesses = 0;
                
                foreach (var process in processes)
                {
                    try
                    {
                        if (process.ProcessName.IndexOf("crowdcontrol", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            TestMod.mls.LogInfo($"Found CrowdControl process: {process.ProcessName} (PID: {process.Id})");
                            return true;
                        }
                        accessibleProcesses++;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Process is running with different privileges (e.g., admin vs regular user)
                        inaccessibleProcesses++;
                    }
                    catch (Exception ex)
                    {
                        // Other access issues
                        TestMod.mls.LogInfo($"Could not access process: {ex.Message}");
                        inaccessibleProcesses++;
                    }
                }
                
                // If we have inaccessible processes, it's possible CrowdControl is running with different privileges
                if (inaccessibleProcesses > 0)
                {
                    TestMod.mls.LogInfo($"Found {inaccessibleProcesses} inaccessible processes (possibly running with different privileges). Attempting connection anyway.");
                    // This handles the case where CrowdControl is running as admin but game is not
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                TestMod.mls.LogError($"Error checking for CrowdControl processes: {ex.Message}");
                // If we can't check processes at all, assume CrowdControl might be running and attempt connection
                return true;
            }
        }

        public void NetworkLoop()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            while (Running)
            {
                // Check if CrowdControl process is running before attempting to connect
                if (!IsCrowdControlProcessRunning())
                {
                    if (!m_hasLoggedNoProcessFound)
                    {
                        TestMod.mls.LogInfo("No CrowdControl process found, skipping connection attempt");
                        m_hasLoggedNoProcessFound = true;
                    }
                    Thread.Sleep(5000); // Wait longer when no process is found
                    continue;
                }
                
                // Reset the flag when we find a process (in case it was lost and found again)
                m_hasLoggedNoProcessFound = false;

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

                Thread.Sleep(2000); // Reduced from 10000 to 2000 for faster reconnection when process is found
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
