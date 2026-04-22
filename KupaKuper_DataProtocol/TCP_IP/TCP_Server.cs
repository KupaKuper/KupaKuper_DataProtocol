using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;

namespace KupaKuper_TcpIp.TCP_IP
{
    /// <summary>
    /// TCP 服务器类
    /// </summary>
    public class TCP_Server
    {
        // ========== 私有字段 ==========
        
        /// <summary>
        /// TCP 监听器
        /// </summary>
        private TcpListener _listener;
        
        /// <summary>
        /// 取消令牌源，用于管理异步操作的取消
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;
        
        /// <summary>
        /// 连接字典，key 格式为 "IP:Port"，value 为 TCP_Connection 对象
        /// </summary>
        private Dictionary<string, TCP_Connection> _connections;
        
        /// <summary>
        /// TcpClient 字典，key 格式为 "IP:Port"，value 为 TcpClient 对象
        /// </summary>
        private Dictionary<string, TcpClient> _tcpClients;
        
        /// <summary>
        /// 连接操作锁对象，确保字典操作的线程安全
        /// </summary>
        private readonly object _connectionLock = new object();
        
        /// <summary>
        /// 心跳检测任务
        /// </summary>
        private Task _heartbeatTask;
        private string _logDirectory;

        /// <summary>
        /// 服务器监听端口
        /// </summary>
        private readonly int _port;

        // ========== 公共属性 ==========
        
        /// <summary>
        /// 服务器完整 IP 地址
        /// </summary>
        public string ServerIP { get; private set; } = string.Empty;
        /// <summary>
        /// 心跳循环时间(秒)
        /// </summary>
        public int HeartBeatCyclicTime { get; set; } = 5;

        /// <summary>
        /// 日志存储目录，默认存储到执行文件夹下的/log文件夹
        /// </summary>
        public string LogDirectory
        {
            get => _logDirectory;
            set
            {
                _logDirectory = value;
                EnsureLogDirectoryExists();
            }
        }

        /// <summary>
        /// 是否启用文件日志，默认 true
        /// </summary>
        public bool LogToFile { get; set; } = true;
        
        /// <summary>
        /// 日志输出事件，可绑定自定义日志处理
        /// </summary>
        public event Action<string> Log;

        // ========== 公共事件 ==========

        /// <summary>
        /// 有新连接事件，参数为新连接的 TCP_Connection 对象
        /// </summary>
        public event Action<TCP_Connection> HaveNewConnection;
        
        /// <summary>
        /// 有连接断开事件，参数为断开连接的 TCP_Connection 对象
        /// </summary>
        public event Action<TCP_Connection> HaveConnectionClose;

        // ========== 构造函数 ==========
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="port">服务器监听端口</param>
        public TCP_Server(int port)
        {
            _port = port;
            _connections = new Dictionary<string, TCP_Connection>();
            _tcpClients = new Dictionary<string, TcpClient>();
            _cancellationTokenSource = new CancellationTokenSource();

            // 设置默认日志目录
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
        }

        private void EnsureLogDirectoryExists()
        {
            if (!string.IsNullOrEmpty(_logDirectory) && !Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }
        
        private void LogMessage(string msg)
        {
            string fullMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [TCP_Server] {msg}";
            
            // 触发外部日志事件
            Log?.Invoke(fullMsg);
            
            // 写入文件日志
            if (LogToFile)
            {
                try
                {
                    string logFile = Path.Combine(_logDirectory, $"tcp_server_{DateTime.Now:yyyy-MM-dd}.log");
                    File.AppendAllText(logFile, fullMsg + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    // 日志写入失败时，输出到控制台
                    Console.WriteLine($"[日志错误] {ex.Message}");
                }
            }
            
            // 默认输出到控制台
            Console.WriteLine(fullMsg);
        }

        // ========== 公共方法 ==========
        
        /// <summary>
        /// 启动服务器
        /// </summary>
        public async Task StartAsync()
        {
            // 初始化 TCP 监听器，监听所有网络接口
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            LogMessage($"服务器已启动，监听端口 {_port}");

            // 获取并设置服务器本地 IP 地址
            ServerIP = GetLocalIPAddress();
            LogMessage($"服务器 IP 地址: {ServerIP}");

            // 启动心跳检测任务
            _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            LogMessage("心跳检测任务已启动");

            // 开始接受客户端连接
            LogMessage("开始接受客户端连接");
            await AcceptConnectionsAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public async Task StopAsync()
        {
            LogMessage("正在停止服务器...");
            // 发送取消信号
            _cancellationTokenSource.Cancel();

            // 等待心跳任务安全结束
            if (_heartbeatTask != null)
            {
                try
                {
                    await _heartbeatTask;
                }
                catch (OperationCanceledException)
                {
                    // 任务被取消是正常的
                }
                catch (Exception ex)
                {
                    LogMessage($"心跳任务结束异常: {ex.Message}");
                }
            }

            // 停止 TCP 监听器
            _listener?.Stop();
            LogMessage("TCP 监听器已停止");

            // 关闭所有客户端连接
            lock (_connectionLock)
            {
                int count = _tcpClients.Count;
                LogMessage($"关闭 {count} 个客户端连接");
                foreach (var client in _tcpClients.Values)
                {
                    try
                    {
                        client.Close();
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"关闭连接异常: {ex.Message}");
                    }
                }
                
                // 清空连接字典
                _connections.Clear();
                _tcpClients.Clear();
                LogMessage("连接字典已清空");
            }
            LogMessage("服务器已停止");
        }

        // ========== 私有方法 ==========
        
        /// <summary>
        /// 接受客户端连接循环
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            // 持续接受连接，直到被取消
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 异步接受新的客户端连接
                    var client = await _listener.AcceptTcpClientAsync();
                    
                    // 处理新连接（不等待，避免阻塞接受下一个连接）
                    _ = HandleClientAsync(client, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // 操作被取消，退出循环
                    break;
                }
                catch (Exception ex)
            {
                string errorMsg = $"接受连接时出错: {ex.Message}";
                LogMessage(errorMsg);
            }
            }
        }

        /// <summary>
        /// 处理客户端连接
        /// </summary>
        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            var endPoint = (IPEndPoint)client.Client.RemoteEndPoint;
            var clientIp = endPoint.Address.ToString();
            var clientPort = endPoint.Port;
            var connectionKey = $"{clientIp}:{clientPort}";
            LogMessage($"收到新连接: {connectionKey}");

            // 创建 TCP_Connection
            var connection = new TCP_Connection(clientIp, clientPort);
            connection.ConnectedChanged += (connected) =>
            {
                lock (_connectionLock)
                {
                    if (!connected && _connections.ContainsKey(connectionKey))
                    {
                        _connections.Remove(connectionKey);
                        _tcpClients.Remove(connectionKey);
                        LogMessage($"连接断开: {connectionKey}");
                    }
                    else
                    {
                        connection.SendMessage($"{ServerIP}:{_port},ConnectedSuccess");
                        LogMessage($"连接成功: {connectionKey}");
                    }
                }
                if (!connected)
                {
                    HaveConnectionClose?.Invoke(connection);
                }
            };
            connection.OnMessageSend += (message) =>
            {
                bool sendResult = Task.Run(async () =>
                {
                    try
                    {
                        if (_tcpClients.TryGetValue(connectionKey, out var tcpClient) && tcpClient.Connected)
                        {
                            var stream = tcpClient.GetStream();
                            var data = Encoding.UTF8.GetBytes(message);
                            await stream.WriteAsync(data, 0, data.Length, cancellationToken);
                            return true;
                        }
                        return false;
                    }
                    catch
                    {
                        return false;
                    }
                }).Result;
                return sendResult;
            };

            // 添加到连接字典
            lock (_connectionLock)
            {
                if (_connections.ContainsKey(connectionKey))
                {
                    _connections[connectionKey] = connection;
                    _tcpClients[connectionKey] = client;
                    LogMessage($"更新连接: {connectionKey}");
                }
                else
                {
                    _connections.Add(connectionKey, connection);
                    _tcpClients.Add(connectionKey, client);
                    LogMessage($"添加新连接: {connectionKey}");
                }
            }
            connection.IsConnected = true;
            HaveNewConnection?.Invoke(connection);
            LogMessage($"新连接已处理: {connectionKey}");
            // 接收数据
            await ReceiveDataAsync(client, connection, cancellationToken);
        }

        /// <summary>
        /// 接收客户端数据
        /// </summary>
        /// <param name="client">TCP 客户端对象</param>
        /// <param name="connection">连接信息对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task ReceiveDataAsync(TcpClient client, TCP_Connection connection, CancellationToken cancellationToken)
        {
            // 缓冲区大小 1024 字节
            var buffer = new byte[1024];
            var stream = client.GetStream();

            try
            {
                // 持续接收数据，直到连接断开或被取消
                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    // 异步读取数据
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                    if (bytesRead == 0)
                    {
                        // 读取到 0 字节，表示连接已关闭
                        break;
                    }

                    // 将字节数组转换为字符串
                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                LogMessage($"从 {connection.IP}:{connection.Port} 接收消息: {message}");
                
                // 触发接收消息事件
                connection.ReceiveMessage(message);
                }
            }
            catch (OperationCanceledException)
            {
                // 操作被取消是正常的
            }
            catch (Exception ex)
            {
                string errorMsg = $"接收数据时出错: {ex.Message}";
                LogMessage(errorMsg);
            }
            finally
            {
                // 设置连接状态为断开
                connection.IsConnected = false;
                LogMessage($"连接 {connection.IP}:{connection.Port} 已关闭");
                
                // 关闭客户端连接
                try
                {
                    client.Close();
                }
                catch (Exception)
                {
                    // 忽略关闭连接时的异常
                }
            }
        }

        /// <summary>
        /// 心跳检测循环
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
        {
            // 持续进行心跳检测，直到被取消
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 保存需要移除的断开连接
                    var disconnectedConnections = new List<TCP_Connection>();
                    var disconnectedKeys = new List<string>();

                    lock (_connectionLock)
                    {
                        foreach (var kvp in _connections)
                        {
                            var key = kvp.Key;
                            var connection = kvp.Value;

                            // 仅对启用心跳检测的连接进行心跳检测
                            if (connection.OpenHeartBeat)
                            {
                                // 发送心跳消息
                        if (!connection.SendMessage(connection.HeartBeatMessage))
                        {
                            // 心跳发送失败，记录该连接
                            disconnectedKeys.Add(key);
                            disconnectedConnections.Add(connection);
                            LogMessage($"心跳检测失败，连接断开: {key}");
                        }
                            }
                        }

                        // 移除断开的连接
                        foreach (var key in disconnectedKeys)
                        {
                            _connections.Remove(key);
                            _tcpClients.Remove(key);
                        }
                    }
                    
                    // 在锁外触发连接断开事件
                    foreach (var connection in disconnectedConnections)
                    {
                        HaveConnectionClose?.Invoke(connection);
                    }

                    // 延时 5 秒后进行下一次检测
                    await Task.Delay(TimeSpan.FromSeconds(HeartBeatCyclicTime), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // 操作被取消，退出循环
                    break;
                }
                catch (Exception ex)
            {
                string errorMsg = $"心跳检测出错: {ex.Message}";
                LogMessage(errorMsg);
                
                // 发生异常后，延时 10 秒继续
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            }
        }

        /// <summary>
        /// PTP 发送（点对点）- 指定 IP 和端口
        /// </summary>
        /// <param name="ip">目标 IP 地址</param>
        /// <param name="port">目标端口号</param>
        /// <param name="message">要发送的消息内容</param>
        /// <returns>是否发送成功</returns>
        public bool SendTo(string ip, int port, string message)
        {
            var connectionKey = $"{ip}:{port}";
            lock (_connectionLock)
            {
                if (_connections.TryGetValue(connectionKey, out var connection))
                {
                    bool result = connection.SendMessage(message);
                    LogMessage($"发送消息到 {connectionKey}: {message} (结果: {result})");
                    return result;
                }
                LogMessage($"发送失败，连接不存在: {connectionKey}");
                return false;
            }
        }

        /// <summary>
        /// PTP 发送（点对点）- 使用 IP:Port 格式的字符串
        /// </summary>
        /// <param name="ipPort">目标地址，格式为 "IP:Port"</param>
        /// <param name="message">要发送的消息内容</param>
        /// <returns>是否发送成功</returns>
        public bool SendTo(string ipPort, string message)
        {
            lock (_connectionLock)
            {
                if (_connections.TryGetValue(ipPort, out var conn))
                {
                    bool result = conn.SendMessage(message);
                    LogMessage($"发送消息到 {ipPort}: {message} (结果: {result})");
                    return result;
                }
                LogMessage($"发送失败，连接不存在: {ipPort}");
                return false;
            }
        }

        /// <summary>
        /// 广播发送 - 向所有连接的客户端发送消息
        /// </summary>
        /// <param name="message">要发送的消息内容</param>
        public void Broadcast(string message)
        {
            lock (_connectionLock)
            {
                int count = _connections.Count;
                LogMessage($"广播消息到 {count} 个连接: {message}");
                foreach (var connection in _connections.Values)
                {
                    connection.SendMessage(message);
                }
            }
        }

        /// <summary>
        /// 批量发送 - 向指定的多个连接发送消息
        /// </summary>
        /// <param name="listIpPort">目标地址列表，每个元素格式为 "IP:Port"</param>
        /// <param name="message">要发送的消息内容</param>
        public void SendToMultiple(IEnumerable<string> listIpPort, string message)
        {
            lock (_connectionLock)
            {
                int sentCount = 0;
                foreach (var ipPort in listIpPort)
                {
                    if (_connections.TryGetValue(ipPort, out var conn))
                    {
                        conn.SendMessage(message);
                        sentCount++;
                    }
                    else
                    {
                        LogMessage($"发送失败，连接不存在: {ipPort}");
                    }
                }
                LogMessage($"批量发送消息到 {sentCount} 个连接: {message}");
            }
        }

        /// <summary>
        /// 获取所有当前连接
        /// </summary>
        /// <returns>返回连接字典的副本，key 为 "IP:Port" 格式</returns>
        public Dictionary<string, TCP_Connection> GetConnections()
        {
            lock (_connectionLock)
            {
                int count = _connections.Count;
                LogMessage($"获取当前连接数: {count}");
                return new Dictionary<string, TCP_Connection>(_connections);
            }
        }

        /// <summary>
        /// 获取本地 IPv4 地址
        /// </summary>
        /// <returns>本地 IP 地址字符串，如果无法获取则返回 127.0.0.1</returns>
        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                
                // 遍历所有 IP 地址，返回第一个 IPv4 地址
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception)
            {
                // 获取失败时使用默认地址
            }
            
            // 默认返回本地回环地址
            return "127.0.0.1";
        }
    }
}