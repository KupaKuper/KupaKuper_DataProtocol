namespace KupaKuper_TcpIp.TCP_IP
{
    /// <summary>
    /// TCP 连接信息类
    /// </summary>
    public class TCP_Connection
    {
        // 私有字段
        private bool _isConnected = false;

        /// <summary>
        /// 客户端 IP 地址
        /// </summary>
        public string IP { get; set; }

        /// <summary>
        /// 客户端端口号
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 历史消息记录列表
        /// </summary>
        public List<string> Message { get; set; } = new List<string>();

        /// <summary>
        /// 连接状态（true=已连接，false=已断开）
        /// </summary>
        public bool IsConnected
        {
            get { return _isConnected; }
            set
            {
                // 只有状态真正改变时才触发事件
                if (_isConnected != value)
                {
                    _isConnected = value;
                    // 触发连接状态改变事件
                    ConnectedChanged?.Invoke(_isConnected);
                }
            }
        }

        /// <summary>
        /// 是否启用心跳检测
        /// </summary>
        public bool OpenHeartBeat { get; set; } = false;

        /// <summary>
        /// 心跳检测消息内容
        /// </summary>
        public string HeartBeatMessage { get; set; } = "HeartBeat";

        /// <summary>
        /// 连接状态改变事件
        /// </summary>
        public event Action<bool> ConnectedChanged;

        /// <summary>
        /// 发送消息事件（参数：要发送的消息内容；返回：是否发送成功）
        /// </summary>
        public event Func<string, bool> OnMessageSend;

        /// <summary>
        /// 接收消息事件（参数：接收到的消息内容）
        /// </summary>
        public event Action<string> OnMessageReceive;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ip">客户端 IP 地址</param>
        /// <param name="port">客户端端口号</param>
        public TCP_Connection(string ip, int port)
        {
            IP = ip;
            Port = port;
        }

        /// <summary>
        /// 触发发送消息事件
        /// </summary>
        /// <param name="message">要发送的消息内容</param>
        /// <returns>是否发送成功</returns>
        public bool SendMessage(string message)
        {
            if (OnMessageSend == null) return false;
            if (OnMessageSend.Invoke(message))
            {
                SaveMessage(message,true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 触发接收消息事件
        /// </summary>
        /// <param name="message">接收到的消息内容</param>
        public void ReceiveMessage(string message)
        {
            SaveMessage(message, false);
            OnMessageReceive?.Invoke(message);
        }

        /// <summary>
        /// 保存消息到历史记录列表，并添加时间戳，仅保留最近的 100 条记录（倒序：最新消息在列表首位）
        /// SendOrReceive: true=发送消息，false=接收消息
        /// </summary>
        /// <param name="message"></param>
        /// <param name="SendOrReceive"></param>
        private void SaveMessage(string message, bool SendOrReceive)
        {
            string timestampedMessage = $"{(SendOrReceive ? "S:" : "R:")}[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

            // 新消息插入到列表开头，保证最新消息在索引 0 位置
            Message.Insert(0, timestampedMessage);

            // 只保留最近的 100 条消息（即前 100 个元素）
            if (Message.Count > 100)
            {
                // 移除超出部分的最旧消息（位于列表末尾）
                Message.RemoveRange(100, Message.Count - 100);
            }
        }
    }
}
