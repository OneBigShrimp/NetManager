using System;
using System.Collections.Generic;
using System.Text;

namespace MyNetManager
{
    /// <summary>
    /// 客户端网络管理类
    /// </summary>
    public class ClientNetManager
    {
        public static readonly ClientNetManager Instance = new ClientNetManager();

        ILinker _linker;

        Action<bool> _onConnect;

        public ILinker Linker
        {
            get
            {
                return _linker;
            }
        }

        private ClientNetManager() { }

        /// <summary>
        /// 注册协议对象和Id,要保证和服务器注册的类型与Id匹配关系一致
        /// </summary>
        /// <param name="type"></param>
        /// <param name="typeId"></param>
        public void Regist(Type type)
        {
            SerializeControl.Instance.Regist(type);
        }

        /// <summary>
        /// 连接到对应主机
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="sendBufferSize"></param>
        /// <param name="receiveBufferSize"></param>
        /// <param name="useAsyncSocket">true: 使用异步发送和接受, false:另开线程同步发送</param>
        public void Connect(string host, int port, int sendBufferSize, int receiveBufferSize, Action<bool> onConnect, bool useAsyncSocket = false)
        {
            if (this._linker != null)
            {
                return;
            }
            if (useAsyncSocket)
            {
                this._linker = new AsyncLinker(host, port, sendBufferSize, receiveBufferSize, this.ExceptionProcess);
            }
            else
            {
                this._linker = new ThdLinker(host, port, sendBufferSize, receiveBufferSize, this.ExceptionProcess);
            }
            this._onConnect = onConnect;
            this._linker.OnConnect += OnConnect;
            this._linker.Connect();
        }

        /// <summary>
        /// 发送一个协议对象到服务器
        /// </summary>
        /// <param name="proto"></param>
        public void SendMsg(IProtocol proto)
        {
            this._linker.SendMsg(proto);
        }


        /// <summary>
        /// 进行一次Tick
        /// </summary>
        /// <param name="delta"></param>
        public void Tick()
        {
            if (this._linker != null)
            {
                this._linker.Tick(null);
            }
        }

        public void Close()
        {
            if (this._linker != null)
            {
                this._linker.Close();
            }
        }

        void OnConnect(bool success)
        {
            if (!success)
            {
                this._linker = null;
            }
            if (this._onConnect != null)
            {
                this._onConnect(success);
            }
        }

        void ExceptionProcess(ILinker linker, NetExceptionType excType, Exception exc)
        {

        }
    }
}
