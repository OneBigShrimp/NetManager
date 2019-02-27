using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MyNetManager
{
    public enum NetExceptionType
    {
        /// <summary>
        /// 连接异常
        /// </summary>
        Connect = 1,
        /// <summary>
        /// 接受数据异常
        /// </summary>
        Receive = 2,
        /// <summary>
        /// 发送异常
        /// </summary>
        Send = 3,
        /// <summary>
        /// 长时间没心跳
        /// </summary>
        Alive = 4,
        /// <summary>
        /// 对方关闭Socket
        /// </summary>
        OtherSideClose = 5,
        /// <summary>
        /// 反序列化异常
        /// </summary>
        Deserialize = 6,
    }

    public delegate void NetExceptionProcess(ILinker linker, NetExceptionType excType, Exception ex);


}
