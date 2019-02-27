using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MyNetManager
{
    public interface ILinker
    {
        AutoResetEvent ReceiveEvent { set; }

        event Action<bool> OnConnect;
        int Id { get; set; }
        void StartRecieve();
        void StartSend();
        void Connect();
        void SendMsg(IProtocol proto);
        void Tick(object args);
        void Close();
        bool IsConnected();
    }
}
