using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;

namespace KupaKuper_TcpIp.TCP_IP
{
    /// <summary>
    /// TCP 客户端通讯类
    /// </summary>
    public class TCP_Client
    {
        // ========== 私有字段 ==========

        /// <summary>
        /// TCP 客户端对象
        /// </summary>
        private TcpClient _client;

        /// <summary>
        /// 网络流
        /// </summary>
        private NetworkStream _stream;

        /// <summary>
        /// 取消令牌源，用于管理异步操作的取消
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 心跳检测任务
        /// </summary>
        private Task _heartbeatTask;


        // ========== 公共属性 ==========

        /// <summary>
        /// 服务器 IP 地址
        /// </summary>
        public string ServerIP { get; private set; }

        /// <summary>
        /// 服务器端口
        /// </summary>
        public int ServerPort { get; private set; }

        /// <summary>
        /// 客户端 ID
        /// </summary>
        public int ClientId { get; set; }

        /// <summary>
        /// 连接状态
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// 历史消息记录
        /// </summary>
        public List<string> Message { get; private set; } = new List<string>();

        /// <summary>
        /// 开启心跳检测
        /// </summary>
        public bool OpenHeartBeat { get; set; }

        /// <summary>
        /// 心跳循环时间(秒)
        /// </summary>
        public int HeartBeatCyclicTime { get; set; } = 5;

        /// <summary>
        /// 心跳检测取消令牌源
        /// </summary>
        private CancellationTokenSource _heartbeatCancellationTokenSource;
        private string _logDirectory;

        /// <summary>
        /// 心跳消息内容
        /// </summary>
        public string HeartBeatMessage { get; set; } = "HeartBeat";

        /// <summary>
        /// 本地客户端IP地址
        /// </summary>
        public string ClientIP { get; set; }=string.Empty;

        /// <summary>
        /// 本地客户端端口号
        /// </summary>
        public int ClientPort { get; set; }

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
        /// 连接状态改变事件
        /// </summary>
        public event Action<bool> ConnectedChanged;

        /// <summary>
        /// 发送消息事件
        /// </summary>
        public event Action<string> OnMessageSend;

        /// <summary>
        /// 接收消息事件
        /// </summary>
        public event Action<string> OnMessageReceive;

        /// <summary>
        /// 错误事件
        /// </summary>
        public event Action<string> OnError;

        // ========== 构造函数 ==========

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serverIp">服务器 IP 地址</param>
        /// <param name="serverPort">服务器端口</param>
        public TCP_Client(string serverIp, int serverPort)
        {
            ServerIP = serverIp;
            ServerPort = serverPort;

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
            string fullMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [TCP_Client] {msg}";
            
            // 触发外部日志事件
            Log?.Invoke(fullMsg);
            
            // 写入文件日志
            if (LogToFile)
            {
                try
                {
                    string logFile = Path.Combine(_logDirectory, $"tcp_client_{DateTime.Now:yyyy-MM-dd}.log");
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
        /// 连接到服务器
        /// </summary>
        public async Task ConnectAsync()
        {
            try
            {
                LogMessage($"正在连接服务器 {ServerIP}:{ServerPort}...");
                // 连接到服务器
                _cancellationTokenSource = new CancellationTokenSource();
                _client = new TcpClient();
                await _client.ConnectAsync(ServerIP, ServerPort);
                _stream = _client.GetStream();
                // 1. 获取本地终结点（客户端本机的 IP 和端口）
                IPEndPoint? localEndPoint = _client.Client.LocalEndPoint as IPEndPoint;
                if (localEndPoint != null)
                {
                    ClientIP = localEndPoint.Address.ToString();
                    ClientPort = localEndPoint.Port;
                    LogMessage($"连接成功，本地地址: {ClientIP}:{ClientPort}");
                }
                // 设置连接状态
                IsConnected = true;
                ConnectedChanged?.Invoke(true);
                // 启动心跳检测任务
                if (OpenHeartBeat)
                {
                    _heartbeatCancellationTokenSource = new CancellationTokenSource();
                    _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_heartbeatCancellationTokenSource.Token));
                    LogMessage("已启动心跳检测");
                }
                // 开始接收数据
                _ = ReceiveDataAsync(_cancellationTokenSource.Token);
                LogMessage("开始接收数据");
            }
            catch (Exception ex)
            {
                string errorMsg = $"连接服务器失败: {ex.Message}";
                LogMessage(errorMsg);
                OnError?.Invoke(errorMsg);
                IsConnected = false;
                ConnectedChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            // 1. 立即取消心跳令牌（假设心跳循环用的是这个）
            if (_heartbeatCancellationTokenSource != null)
            {
                _heartbeatCancellationTokenSource.Cancel();
                // 注意：不要在这里 Dispose，因为心跳任务可能还在使用它
            }

            // 2. 等待心跳任务结束（它收到取消信号后会退出循环）
            if (_heartbeatTask != null)
            {
                try
                {
                    await _heartbeatTask;
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，忽略
                }
                catch
                {
                    // 其他异常可记录
                }
                _heartbeatTask = null;
            }

            // 3. 现在心跳已停止，可以安全清理资源
            if (_stream != null)
            {
                try { _stream.Close(); } catch { }
                _stream = null;
            }
            if (_client != null)
            {
                try { _client.Close(); } catch { }
                _client = null;
            }

            // 4. 更新状态
            IsConnected = false;
            ConnectedChanged?.Invoke(false);

            // 5. 最后释放 CancellationTokenSource
            _heartbeatCancellationTokenSource?.Dispose();
            _heartbeatCancellationTokenSource = null;

            // 如果还有别的 _cancellationTokenSource，同样处理
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="message">消息内容</param>
        public async Task<bool> SendMessageAsync(string message)
        {
            if (!IsConnected || _stream == null)
            {
                return false;
            }
            
            try
            {
                var data = Encoding.UTF8.GetBytes(message);
                await _stream.WriteAsync(data, 0, data.Length);
                Message.Add($"发送: {message}");
                LogMessage($"发送消息: {message}");
                OnMessageSend?.Invoke(message);
                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = $"发送消息失败: {ex.Message}";
                LogMessage(errorMsg);
                OnError?.Invoke(errorMsg);
                // 发送失败，异常断开连接
                IsConnected = false;
                ConnectedChanged.Invoke(false);
                return false;
            }
        }

        // ========== 私有方法 ==========

        /// <summary>
        /// 接收数据
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task ReceiveDataAsync(CancellationToken cancellationToken)
        {
            if (_stream == null)
            {
                return;
            }
            
            var buffer = new byte[1024];
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                    {
                        // 连接已关闭
                        break;
                    }
                    
                    var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Message.Add($"接收: {message}");
                LogMessage($"接收消息: {message}");
                OnMessageReceive?.Invoke(message);
                }
            }
            catch (OperationCanceledException)
            {
                // 操作被取消是正常的
            }
            catch (Exception ex)
            {
                string errorMsg = $"接收数据失败: {ex.Message}";
                LogMessage(errorMsg);
                OnError?.Invoke(errorMsg);
            }
            finally
            {
                // 连接关闭
                if (IsConnected)
                {
                    IsConnected = false;
                    LogMessage("连接已关闭");
                    ConnectedChanged?.Invoke(false);
                }
            }
        }

        /// <summary>
        /// 心跳检测循环
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (IsConnected)
                {
                    try
                    {
                        // 发送心跳消息
                        await SendMessageAsync(HeartBeatMessage);
                        LogMessage($"发送心跳消息: {HeartBeatMessage}");
                        
                        // 延时 5 秒
                        await Task.Delay(TimeSpan.FromSeconds(HeartBeatCyclicTime), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        string errorMsg = $"心跳检测失败: {ex.Message}";
                        LogMessage(errorMsg);
                        OnError?.Invoke(errorMsg);
                        // 发生异常，异常断开连接
                        IsConnected = false;
                        ConnectedChanged.Invoke(false);
                        // 等待 3 秒后尝试重连
                        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                    }
                }
                else if (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await ConnectAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        break; // 被取消时直接退出循环
                    }
                    catch (Exception ex)
                    {
                        string errorMsg = $"重连失败: {ex.Message}";
                        LogMessage(errorMsg);
                        OnError?.Invoke(errorMsg);
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
        }
    }
}
