# NetManager
轻量级网络通信库,
1.客户端使用ClientNetManager绑定协议和请求连接,调用ClientNetManager的Tick方法可对缓存在队列的协议进行处理
2.服务器使用ServerNetManager初始化连接回调,绑定协议和监听连接请求,连接成功回调会返回一个ILinker对象用于处理收发协议逻辑.
调用每个ILinker的Tick方法可以对其缓存在队列中的协议进行处理,设置ILinker对象的ReceiveEvent属性,并监听此信号量可以屏蔽掉无效Tick轮训
3.协议对象实现IProtocol接口,协议中可以嵌套结构,结构需要实现ISerObj接口(空接口,约定作用)

详细使用方式可以参照Tetris的两个仓库
