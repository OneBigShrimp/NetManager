using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MyNetManager
{
    public class ThdLinker : ILinker
    {
        static MyStream _sendMarshalStream;

        public AutoResetEvent ReceiveEvent { get; set; }

        public event Action<bool> OnConnect;

        int _id = -1;
        public int Id
        {
            get
            {
                return this._id;
            }
            set
            {
                this._id = value;
            }
        }

        Socket _socket;

        string _host;

        int _port;

        ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();

        Queue<IProtocol> _protocols = new Queue<IProtocol>();

        AutoResetEvent _sendEvent;

        Thread _sendThd;

        int _sendBufferSize;

        /// <summary>
        /// 发送中转数据,主线程将要发送的添加进来,发送线程将其取到发送流
        /// </summary>
        MyOctets _sendTransitOct;
        /// <summary>
        /// 发送流,只有发送线程会访问
        /// </summary>
        MyStream _sendBuffer;

        int _receiveBufferSize;

        MyStream _receiveTransitStream;

        byte[] _receiveBuffer;

        Thread _receiveThd;

        NetExceptionProcess _excProcess;
        static ThdLinker()
        {
            _sendMarshalStream = new MyStream();
        }

        ThdLinker(int sendBufferSize, int receiveBufferSize, NetExceptionProcess excProcess)
        {
            this._sendTransitOct = new MyOctets();
            this._sendBuffer = new MyStream();

            this._receiveTransitStream = new MyStream(1024);
            this._receiveBuffer = new byte[1024];

            this._sendEvent = new AutoResetEvent(false);

            this._sendBufferSize = sendBufferSize;
            this._receiveBufferSize = receiveBufferSize;
            if (excProcess == null)
            {
                throw new Exception("parameter 'excProcess' can't be null!!");
            }
            this._excProcess = excProcess;
        }

        public ThdLinker(string host, int port, int sendBufferSize, int receiveBufferSize, NetExceptionProcess excProcess)
            : this(sendBufferSize, receiveBufferSize, excProcess)
        {
            this._host = host;
            this._port = port;
        }

        public ThdLinker(Socket soc, NetExceptionProcess excProcess)
            : this(0, 0, excProcess)
        {
            this._socket = soc;
        }

        public void Connect()
        {
            if (_socket != null)
            {
                return;
            }

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { SendBufferSize = this._sendBufferSize, ReceiveBufferSize = this._receiveBufferSize };
            _socket.BeginConnect(this._host, this._port, iar =>
            {
                _actions.Enqueue(() =>
                {
                    Socket soc = iar.AsyncState as Socket;
                    try
                    {
                        soc.EndConnect(iar);
                    }
                    catch (Exception e)
                    {
                        if (OnConnect != null)
                        {
                            OnConnect(false);
                        }
                        return;
                    }

                    StartSend();
                    StartRecieve();
                    if (OnConnect != null)
                    {
                        OnConnect(true);
                    }
                });
            }, _socket);
        }

        public void SendMsg(IProtocol proto)
        {
            SerializeControl.Instance.Serialize(_sendMarshalStream, proto);
            lock (_sendTransitOct)
            {
                _sendTransitOct.Write(_sendMarshalStream.Oct);
            }
            _sendMarshalStream.Clear();
            _sendEvent.Set();
        }

        public void SendMsg(byte[] bts)
        {
            lock (_sendTransitOct)
            {
                _sendTransitOct.Write(bts);
            }
            _sendEvent.Set();
        }


        public void Tick(object args)
        {
            lock (_receiveTransitStream)
            {
                if (_receiveTransitStream.RemainCount > 0)
                {
                    SerializeControl.Instance.Deserialize(_receiveTransitStream, _protocols);
                    if (_receiveTransitStream.Position != 0)
                    {
                        _receiveTransitStream.ReCalculatePosition(0);
                    }
                }
            }

            while (_protocols.Count > 0)
            {
                IProtocol p = _protocols.Dequeue();
                p.Process(this, args);
            }

            Action action;
            if (_actions.TryDequeue(out action))
            {
                action();
            }
            else
            {
                //break;
            }
        }

        public void StartSend()
        {
            this._sendThd = new Thread(Send);
            _sendThd.IsBackground = true;
            _sendThd.Start(this._socket);
        }

        public void StartRecieve()
        {
            _receiveThd = new Thread(Receive);
            _receiveThd.IsBackground = true;
            _receiveThd.Start(this._socket);
        }

        public void Close()
        {
            _receiveTransitStream.Clear();
            _protocols.Clear();

            //先关闭线程,再关闭socket,这样Receive方法中就不会因为捕获到异常而再次将Close请求放回主线程
            CloseSendThd();
            CloseReceiveThd();

            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }

            _actions.Clear();

        }

        public bool IsConnected()
        {
            return _socket.Connected;
        }

        void Close(NetExceptionType excType, Exception ex)
        {
            Close();
            _excProcess(this, excType, ex);
        }

        void CloseReceiveThd()
        {
            if (_receiveThd != null)
            {
                if ((_receiveThd.ThreadState & ThreadState.Stopped) != 0)
                {
                    _receiveThd.Abort();
                }
                _receiveThd = null;
            }
        }


        void CloseSendThd()
        {
            if (_sendThd != null)
            {
                if ((_sendThd.ThreadState & ThreadState.Stopped) != 0)
                {
                    _sendThd.Abort();
                }
                _sendThd = null;
            }
        }



        void Receive(object socketObj)
        {
            Socket socket = socketObj as Socket;
            while (true)
            {
                int receiveCount;
                try
                {
                    receiveCount = socket.Receive(_receiveBuffer);

                }
                catch (Exception e)
                {
                    //关闭Link
                    Close(NetExceptionType.Receive, e);
                    return;
                }

                if (receiveCount == 0)
                {
                    //关闭Link
                    Close(NetExceptionType.OtherSideClose, new Exception("OtherSideClose"));
                    return;
                }
                lock (_receiveTransitStream)
                {
                    _receiveTransitStream.Oct.Write(_receiveBuffer, 0, receiveCount);
                }
                if (ReceiveEvent != null)
                {
                    ReceiveEvent.Set();
                }
            }
        }


        void Send(object socketObj)
        {
            //不直接访问主线程的_socket字段,因为Close的时候会清空该字段,虽然同时会停止发送线程,安全起见,线程内部保存了一个对象
            //接收同理
            Socket socket = socketObj as Socket;
            while (true)
            {
                _sendEvent.WaitOne();
                lock (_sendTransitOct)
                {
                    _sendBuffer.Oct.Write(_sendTransitOct);
                    _sendTransitOct.Clear();
                }

                SocketError error;

                int saveFlag = 0;

                while (_sendBuffer.RemainCount > 0)
                {
                    if (saveFlag++ > 100)
                    {
                        //throw new Exception("Send loop is over 100 times!");
                        //关闭Link
                        Close(NetExceptionType.Send, new Exception("Maybe send thread is dead loop"));
                        return;
                    }
                    int sendCount = 0;
                    try
                    {
                        sendCount = socket.Send(_sendBuffer.Oct.Datas, _sendBuffer.Position, _sendBuffer.RemainCount, SocketFlags.None, out error);
                    }
                    catch (Exception e)
                    {
                        Close(NetExceptionType.Send, e);
                        return;
                    }
                    _sendBuffer.ReCalculatePosition(sendCount);
                }
            }
        }

        void ExceptionClose(NetExceptionType excType, Exception ex)
        {
            this.Close();
            this._excProcess(this, excType, ex);
        }


    }
}
