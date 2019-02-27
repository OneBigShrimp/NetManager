using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MyNetManager
{
    /// <summary>
    /// 连接对象
    /// </summary>
    public class AsyncLinker : ILinker
    {
        private ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();
        public event Action<bool> OnConnect;

        public AutoResetEvent ReceiveEvent { get; set; }
        public int Id { get; set; }

        private int _maxMilliseconds = 20000000;

        private string _host;

        private int _port;

        private Socket _socket;

        private const int _inputSize = 1024 * 16;

        /// <summary>
        /// 接收数据的二进制数组,接受线程会向其中写入数据,主线程会读取其中数据,但是二者永远不会同时进行,所以不会有线程安全问题
        /// _socket.BeginReceive(...)是在主线程还没有进行读取数据,或者主线程读取数据操作完毕才调用的,而主线程读取数据又是在_socket.BeginReceive(...)
        /// 的回调之后,加入_actions队列,在主线程调用的,所以对于_input的读写行为是没有交集的
        /// </summary>
        private readonly byte[] _input = new byte[_inputSize];

        private int _sendBufferSize;

        private int _receiveBufferSize;

        /// <summary>
        /// 是否正在发送消息的标志位,保证始终都只有一个线程会从_sendStream中取数据发送
        /// </summary>
        private bool _sendingFlag = false;


        /// <summary>
        /// 接受数据使用的流对象,对这个对象的所有操作均在主线程进行,
        /// 不同的Linker对象不能使用同一个Stream,因为一次Receive可能接受到的信息不全,
        /// 导致Stream中依然存在有用数据,要和本Link后续Receive的数据拼接解析才会正常解析
        /// 如果不同Linker对象同时向一个Stream内写数据,会导致互相污染,_sendStream同理
        /// </summary>
        private MyStream _receiveStream;
        /// <summary>
        /// 发送数据使用的流对象,对这个对象的所有操作均在主线程进行,
        /// </summary>
        private MyStream _sendStream;

        private Queue<IProtocol> _protocols = new Queue<IProtocol>();

        bool socketException = false;

        NetExceptionProcess _excProcess;

        AsyncLinker(int sendBufferSize, int receiveBufferSize, NetExceptionProcess excProcess)
        {
            this._receiveStream = new MyStream();
            this._sendStream = new MyStream();
            this._sendBufferSize = sendBufferSize;
            this._receiveBufferSize = receiveBufferSize;
            this._excProcess = excProcess;
        }

        internal AsyncLinker(string host, int port, int sendBufferSize, int receiveBufferSize, NetExceptionProcess excProcess)
            : this(sendBufferSize, receiveBufferSize, excProcess)
        {
            this._host = host;
            this._port = port;
        }

        internal AsyncLinker(Socket socket, NetExceptionProcess excProcess)
            : this(0, 0, excProcess)
        {
            this._socket = socket;
            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            _socket.NoDelay = true;
        }



        public void Connect()
        {
            if (_socket != null)
            {
                return;
            }
            //请求连接到连接成功期间阻塞消息发送
            _sendingFlag = true;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { SendBufferSize = this._sendBufferSize, ReceiveBufferSize = this._receiveBufferSize };
            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            _socket.BeginConnect(this._host, this._port, iar =>
            {
                _socket.EndConnect(iar);
                _socket.NoDelay = true;
                _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
                //连接成功后才允许发送消息
                this._sendingFlag = false;
                this.StartRecieve();
                if (_sendStream.Oct.Count > 0)
                {//连接成功后,发现存在要发送的消息,则发送
                    this._BeginSend();
                }
            }, _socket);

        }

        public void SendMsg(IProtocol proto)
        {
            SerializeControl.Instance.Serialize(_sendStream, proto);
            _BeginSend();
        }

        public void StartSend()
        {

        }

        public void StartRecieve()
        {
            try
            {
                _socket.BeginReceive(_input, 0, _inputSize, SocketFlags.None, iar => _actions.Enqueue(() =>
                {//添加到_actions列表保证在主线程对ReceiveStream执行添加操作
                    int length = 0;
                    try
                    {
                        length = _socket.EndReceive(iar);
                    }
                    catch (Exception ex)
                    {
                        ExceptionClose(NetExceptionType.Receive, ex);
                        return;
                    }

                    _receiveStream.Oct.Write(_input, 0, length);

                    try
                    {
                        SerializeControl.Instance.Deserialize(_receiveStream, _protocols);
                    }
                    catch (Exception ex)
                    {
                        ExceptionClose(NetExceptionType.Deserialize, ex);
                        return;
                    }

                    if (_receiveStream.Position != 0)
                    {
                        _receiveStream.ReCalculatePosition(0);
                    }
                    StartRecieve();

                }), null);
            }
            catch (Exception ex)
            {
                ExceptionClose(NetExceptionType.Receive, ex);
            }

        }



        /// <summary>
        /// 进行一次Tick,会调用已经还没有处理协议的处理方法,会执行一些从通信线程添加到主线程的一些Action
        /// Action1(一次数据接收行为结束的处理): 将收到的数据转为协议对象,并添加到协议对象队列,同时进行下一次的BeginReceive
        /// Action2(一次数据发送行为结束的处理): 依然有还没发送完毕的数据,则进行下一次发送行为
        /// </summary>
        public void Tick(object args)
        {
            long startStmp = Utils.CurrentTimeMillis();

            //TODO: 重连逻辑

            while (_protocols.Count > 0 && Utils.CurrentTimeMillis() - startStmp < _maxMilliseconds)
            {
                _protocols.Dequeue().Process(this, args);
            }
            Action act;
            while (_actions.TryDequeue(out act) && Utils.CurrentTimeMillis() - startStmp < _maxMilliseconds)
            {
                act.Invoke();
            }
        }

        public bool IsConnected()
        {
            return _socket.Connected;
        }



        private void _BeginSend()
        {
            if (this._sendingFlag)
            {//正在发送则无需处理,等本次发送完毕,会自行进行下次数据发送
                return;
            }
            this._sendingFlag = true;

            long curMili = Utils.CurrentTimeMillis();

            _socket.BeginSend(_sendStream.Oct.Datas, _sendStream.Position, _sendStream.RemainCount, SocketFlags.None, iar => _actions.Enqueue(() =>
            {
                int length = 0;
                try
                {
                    length = _socket.EndSend(iar);
                    //TestArray.Add(Utils.CurrentTimeMillis() - curMili);
                }
                catch (Exception)
                {
                    ExcetpionHappen();
                    return;
                }
                _sendStream.ReCalculatePosition(length);
      

                this._sendingFlag = false;
                if (_sendStream.Oct.Count > 0)
                {
                    _BeginSend();
                }
            }), null);

        }


        public void Close()
        {
            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }
        }

        private void ExcetpionHappen()
        {
            socketException = true;
            Console.WriteLine("Socket异常");
        }


        void ExceptionClose(NetExceptionType excType, Exception ex)
        {
            this.Close();
            this._excProcess(this, excType, ex);
        }
    }
}
