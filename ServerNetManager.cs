using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MyNetManager
{
    public class ServerNetManager
    {
        public static readonly ServerNetManager Instance = new ServerNetManager();

        Action<ILinker> _onLinkerAdd;

        NetExceptionProcess _excProcess;

        Socket listenSocket;

        Thread _listenThread;

        private ServerNetManager() { }

        public void Init(Action<ILinker> onLinkerAdd, NetExceptionProcess excProcess)
        {
            this._onLinkerAdd = onLinkerAdd;
            this._excProcess = excProcess;
        }

        public void Regist(Type type)
        {
            SerializeControl.Instance.Regist(type);
        }

        public void StartListen(string host, int port)
        {
            if (this._listenThread != null)
            {
                return;
            }

            IPAddress ip = IPAddress.Parse(host);
            listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(ip, port));
            listenSocket.Listen(10);

            _listenThread = new Thread(() =>
            {
                while (true)
                {
                    Socket clientSoc = listenSocket.Accept();
                    clientSoc.NoDelay = true;
                    ILinker linker = new ThdLinker(clientSoc, this._excProcess);
                    linker.StartRecieve();
                    linker.StartSend();
                    if (_onLinkerAdd != null)
                    {
                        _onLinkerAdd.Invoke(linker);
                    }
                    else
                    {
                        linker.Close();
                    }
                }
            });
            _listenThread.Start();
            _listenThread.IsBackground = true;
        }

        public void StopListen()
        {
            if (this._listenThread == null)
            {
                return;
            }
            this._listenThread.Abort();
            this._listenThread = null;
        }

    }
}
