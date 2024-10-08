﻿using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

namespace BepinControl
{
    public class CrowdRequest
    {
        public static readonly int RECV_BUF = 4096;
        public static readonly int RECV_TIME = 5000000;

        public string code;
        public int id;
        public int duration;
        public string type;
        public string viewer;
        public Target[] targets;

        public static CrowdRequest Recieve(ControlClient client, Socket socket)
        {
            byte[] buf = new byte[RECV_BUF];
            string content = "";
            int read = 0;

            do
            {
                if (!client.IsRunning()) return null;

                if (socket.Poll(RECV_TIME, SelectMode.SelectRead))
                {
                    read = socket.Receive(buf);
                    if (read < 0) return null;

                    content += Encoding.ASCII.GetString(buf);
                }
                else
                    CrowdResponse.KeepAlive(socket);
            } while (read == 0 || (read == RECV_BUF && buf[RECV_BUF - 1] != 0));

            return JsonConvert.DeserializeObject<CrowdRequest>(content);
        }

        public enum Type
        {
            REQUEST_TEST,
            REQUEST_START,
            REQUEST_STOP,
            REQUEST_KEEPALIVE=255
        }

        public string GetReqCode()
        {
            return this.code;
        }

        public int GetReqID()
        {
            return this.id;
        }

        public int GetReqDuration()
        {
            return this.duration;
        }

        public Type GetReqType()
        {
            string value = this.type;
            if (value == "1")
                return Type.REQUEST_START;
            else if (value == "2")
                return Type.REQUEST_STOP;
            return Type.REQUEST_TEST;
        }

        public string GetReqViewer()
        {
            return this.viewer;
        }

        public bool IsKeepAlive()
        {
            return id == 0 && type == "255";
        }

        public class Target
        {
            public string service; // E.g., Twitch, YouTube, etc.
            public string id;      // Target ID (e.g., viewer or streamer's ID)
            public string name;    // Target's name
            public string avatar;  // Optional avatar URL

            public override string ToString()
            {
                return $"Target(Service: {service}, ID: {id}, Name: {name}, Avatar: {avatar})";
            }
        }

    }
}
