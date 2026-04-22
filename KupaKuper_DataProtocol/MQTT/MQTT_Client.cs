using MQTTnet;
using MQTTnet.Protocol;
using System.Collections.Concurrent;
using System.Text;
using System.IO;

namespace KupaKuper_DataProtocol.MQTT
{
    /// <summary>
    /// MQTT 客户端，支持自动重连、动态订阅、日志记录和事件通知。
    /// </summary>
    public class MQTT_Client : IDisposable
    {
        private IMqttClient _mqttClient;
        private MqttClientOptions _options;
        private bool _isDisconnecting = false;
        private CancellationTokenSource _reconnectCts;
        private Task _reconnectTask;
        private readonly ConcurrentDictionary<string, MqttQualityOfServiceLevel> _subscriptions = new();
        private string _logDirectory;
        private bool _logToFile = true;

        /// <summary>
        /// 当前连接状态
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// 连接状态变更事件 (sender, isConnected)
        /// </summary>
        public event EventHandler<bool> ConnectionStateChanged;

        /// <summary>
        /// 收到应用消息事件 (sender, 主题, 消息内容)
        /// </summary>
        public event EventHandler<(string Topic, string Message)> MessageReceived;

        /// <summary>
        /// 日志输出事件，可绑定自定义日志处理（如写入文件、数据库）
        /// </summary>
        public event Action<string> Log;

        /// <summary>
        /// 服务器地址
        /// </summary>
        public string Server { get; private set; }

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// 客户端标识符
        /// </summary>
        public string ClientId { get; private set; }

        /// <summary>
        /// 是否使用 TLS
        /// </summary>
        public bool UseTls { get; private set; }

        /// <summary>
        /// 是否每次连接建立新会话（true 时服务器不保存离线消息）
        /// </summary>
        public bool CleanSession { get; private set; }

        /// <summary>
        /// 用户名（可选）
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 密码（可选）
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 重连间隔（秒），默认 5 秒
        /// </summary>
        public int ReconnectIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// TLS 选项（如果 UseTls 为 true，则需要配置此项以启用 TLS 加密连接）
        /// </summary>
        public MqttClientTlsOptions? TlsOptions { get; set; }

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
        public bool LogToFile
        {
            get => _logToFile;
            set => _logToFile = value;
        }

        /// <summary>
        /// 构造函数，使用默认配置（无认证，非 TLS，清除会话）
        /// </summary>
        /// <param name="server">MQTT 服务器地址</param>
        /// <param name="port">端口，默认 1883</param>
        /// <param name="clientId">客户端 ID，默认自动生成</param>
        /// <param name="useTls">是否使用 TLS</param>
        /// <param name="cleanSession">是否清除会话</param>
        public MQTT_Client(string server, int port = 1883, string clientId = null, bool useTls = false, bool cleanSession = true)
        {
            Server = server;
            Port = port;
            ClientId = clientId ?? Guid.NewGuid().ToString();
            UseTls = useTls;
            CleanSession = cleanSession;

            // 设置默认日志目录
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
            EnsureLogDirectoryExists();

            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();

            // 注册内部事件处理
            _mqttClient.ConnectedAsync += OnConnectedInternal;
            _mqttClient.DisconnectedAsync += OnDisconnectedInternal;
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedInternal;
        }

        // ========== 公共方法 ==========

        /// <summary>
        /// 连接到 MQTT 服务器
        /// </summary>
        public async Task ConnectAsync()
        {
            if (IsConnected) return;

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(Server, Port)
                .WithClientId(ClientId)
                .WithCleanSession(CleanSession);

            if (UseTls && TlsOptions != null)
                optionsBuilder.WithTlsOptions(TlsOptions);

            if (!string.IsNullOrEmpty(Username))
                optionsBuilder.WithCredentials(Username, Password);

            _options = optionsBuilder.Build();

            try
            {
                await _mqttClient.ConnectAsync(_options);
                LogMessage($"[连接] 正在连接 {Server}:{Port} ...");
            }
            catch (Exception ex)
            {
                LogMessage($"[连接失败] {ex.Message}");
                StartReconnect();
                throw;
            }
        }

        /// <summary>
        /// 断开连接，并停止自动重连
        /// </summary>
        public async Task DisconnectAsync()
        {
            _isDisconnecting = true;
            StopReconnect();

            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }
            IsConnected = false;
            ConnectionStateChanged?.Invoke(this, false);
            LogMessage("[断开] 已主动断开连接");
        }

        /// <summary>
        /// 订阅主题
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="qos">服务质量等级，默认 AtLeastOnce</param>
        public async Task SubscribeAsync(string topic, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce)
        {
            if (!_mqttClient.IsConnected)
                throw new InvalidOperationException("客户端未连接，无法订阅");

            var filter = new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .WithQualityOfServiceLevel(qos)
                .Build();

            await _mqttClient.SubscribeAsync(filter);
            _subscriptions[topic] = qos;
            LogMessage($"[订阅] 主题: {topic}, QoS: {qos}");
        }

        /// <summary>
        /// 取消订阅主题
        /// </summary>
        public async Task UnsubscribeAsync(string topic)
        {
            if (!_mqttClient.IsConnected)
                throw new InvalidOperationException("客户端未连接，无法取消订阅");

            await _mqttClient.UnsubscribeAsync(topic);
            _subscriptions.TryRemove(topic, out _);
            LogMessage($"[取消订阅] 主题: {topic}");
        }

        /// <summary>
        /// 发布消息（字符串负载）
        /// </summary>
        public async Task PublishAsync(string topic, string payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, bool retain = false)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(retain)
                .Build();

            await PublishInternalAsync(message, topic, payload);
        }

        /// <summary>
        /// 发布消息（字节数组负载）
        /// </summary>
        public async Task PublishAsync(string topic, byte[] payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, bool retain = false)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(retain)
                .Build();

            // 字节数组转为可读字符串用于日志（可选，可能产生大量输出）
            string payloadPreview = payload.Length > 100 ? Convert.ToBase64String(payload).Substring(0, 100) + "..." : Convert.ToBase64String(payload);
            await PublishInternalAsync(message, topic, payloadPreview);
        }

        /// <summary>
        /// 发布消息（对象，自动序列化为 JSON）
        /// </summary>
        public async Task PublishAsync<T>(string topic, T payloadObj, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, bool retain = false)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(payloadObj);
            await PublishAsync(topic, json, qos, retain);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _mqttClient?.DisconnectAsync().Wait();
            _mqttClient?.Dispose();
        }

        // ========== 私有方法 ==========

        private async Task PublishInternalAsync(MqttApplicationMessage message, string topic, string logPayload)
        {
            if (!_mqttClient.IsConnected)
                throw new InvalidOperationException("客户端未连接，无法发布消息");

            await _mqttClient.PublishAsync(message);
            LogMessage($"[发送] 主题: {topic} | 内容: {logPayload}");
        }

        private Task OnConnectedInternal(MqttClientConnectedEventArgs args)
        {
            IsConnected = true;
            ConnectionStateChanged?.Invoke(this, true);
            LogMessage("[连接成功]");
            StopReconnect(); // 连接成功时取消重连任务
            return Task.CompletedTask;
        }

        private async Task OnDisconnectedInternal(MqttClientDisconnectedEventArgs args)
        {
            IsConnected = false;
            ConnectionStateChanged?.Invoke(this, false);
            LogMessage($"[断开] 原因: {args.Reason}");

            if (!_isDisconnecting)
            {
                StartReconnect();
            }
            else
            {
                _isDisconnecting = false; // 重置标志
            }
        }

        private Task OnMessageReceivedInternal(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
            LogMessage($"[接收] 主题: {topic} | 内容: {payload}");
            MessageReceived?.Invoke(this, (topic, payload));
            return Task.CompletedTask;
        }

        private void StartReconnect()
        {
            if (_reconnectCts != null && !_reconnectCts.IsCancellationRequested)
                return; // 已经在重连中

            _reconnectCts = new CancellationTokenSource();
            _reconnectTask = Task.Run(() => ReconnectLoop(_reconnectCts.Token));
        }

        private void StopReconnect()
        {
            _reconnectCts?.Cancel();
            _reconnectTask?.Wait(1000);
            _reconnectCts?.Dispose();
            _reconnectCts = null;
        }

        private async Task ReconnectLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && !IsConnected)
            {
                try
                {
                    LogMessage($"[重连] 等待 {ReconnectIntervalSeconds} 秒后尝试重连...");
                    await Task.Delay(ReconnectIntervalSeconds * 1000, token);
                    if (token.IsCancellationRequested) break;

                    LogMessage("[重连] 正在尝试重新连接...");
                    await _mqttClient.ConnectAsync(_options);

                    // 重连成功后恢复所有订阅
                    if (_mqttClient.IsConnected)
                    {
                        foreach (var sub in _subscriptions)
                        {
                            await SubscribeAsync(sub.Key, sub.Value);
                            LogMessage($"[恢复订阅] 主题: {sub.Key}");
                        }
                        break; // 重连成功，退出循环
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage($"[重连失败] {ex.Message}");
                }
            }
        }

        private void LogMessage(string msg)
        {
            string fullMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}";
            
            // 触发外部日志事件
            Log?.Invoke(fullMsg + Environment.NewLine);
            
            // 写入文件日志
            if (_logToFile)
            {
                try
                {
                    string logFile = Path.Combine(_logDirectory, $"mqtt_client_{DateTime.Now:yyyy-MM-dd}.log");
                    File.AppendAllText(logFile, fullMsg + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    // 日志写入失败时，输出到控制台
                    Console.WriteLine($"[日志错误] {ex.Message}");
                }
            }
        }

        private void EnsureLogDirectoryExists()
        {
            if (!string.IsNullOrEmpty(_logDirectory) && !Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }
    }
}