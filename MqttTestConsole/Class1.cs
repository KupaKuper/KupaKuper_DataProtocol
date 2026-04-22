using KupaKuper_DataProtocol.MQTT;
using System.Text;
using System.Text.Json;

namespace MqttTestConsole
{
    public class TestData
    {
        public string Name { get; set; } = "Test";
        public int Value { get; set; } = 123;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class MqttTestPlatform
    {
        private MQTT_Client _client;
        private MQTT_Server _server;
        private bool _isRunning = false;

        public MqttTestPlatform()
        {
            // 设置日志目录
            _client = new MQTT_Client("broker.hivemq.com", 1883);
            _client.LogToFile = true;
            
            // 订阅日志事件
            _client.Log += (msg) => Console.WriteLine($"[客户端日志] {msg}");
            _client.MessageReceived += OnMessageReceived;
            _client.ConnectionStateChanged += OnConnectionStateChanged;
        }

        /// <summary>
        /// 启动测试平台
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning) return;
            
            Console.WriteLine("MQTT 测试平台启动中...");
            Console.WriteLine("===================");
            
            // 连接到 MQTT 服务器
            await _client.ConnectAsync();
            
            _isRunning = true;
            Console.WriteLine("MQTT 测试平台已启动");
        }

        /// <summary>
        /// 停止测试平台
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning) return;
            
            Console.WriteLine("MQTT 测试平台停止中...");
            await _client.DisconnectAsync();
            
            _isRunning = false;
            Console.WriteLine("MQTT 测试平台已停止");
        }

        /// <summary>
        /// 订阅主题
        /// </summary>
        public async Task SubscribeAsync(string topic)
        {
            if (!_isRunning)
            {
                Console.WriteLine("请先启动测试平台");
                return;
            }
            
            try
            {
                await _client.SubscribeAsync(topic);
                Console.WriteLine($"已订阅主题: {topic}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"订阅失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消订阅主题
        /// </summary>
        public async Task UnsubscribeAsync(string topic)
        {
            if (!_isRunning)
            {
                Console.WriteLine("请先启动测试平台");
                return;
            }
            
            try
            {
                await _client.UnsubscribeAsync(topic);
                Console.WriteLine($"已取消订阅主题: {topic}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"取消订阅失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发布字符串消息
        /// </summary>
        public async Task PublishStringAsync(string topic, string message)
        {
            if (!_isRunning)
            {
                Console.WriteLine("请先启动测试平台");
                return;
            }
            
            try
            {
                await _client.PublishAsync(topic, message);
                Console.WriteLine($"已发布消息到 {topic}: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发布失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发布对象消息（自动序列化为 JSON）
        /// </summary>
        public async Task PublishObjectAsync<T>(string topic, T data)
        {
            if (!_isRunning)
            {
                Console.WriteLine("请先启动测试平台");
                return;
            }
            
            try
            {
                await _client.PublishAsync(topic, data);
                Console.WriteLine($"已发布对象到 {topic}: {JsonSerializer.Serialize(data)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发布失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发布字节数组消息
        /// </summary>
        public async Task PublishBytesAsync(string topic, byte[] data)
        {
            if (!_isRunning)
            {
                Console.WriteLine("请先启动测试平台");
                return;
            }
            
            try
            {
                await _client.PublishAsync(topic, data);
                Console.WriteLine($"已发布字节数据到 {topic}，长度: {data.Length}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发布失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示连接状态
        /// </summary>
        public void ShowStatus()
        {
            Console.WriteLine($"连接状态: {_client.IsConnected}");
            Console.WriteLine($"服务器: {_client.Server}:{_client.Port}");
            Console.WriteLine($"客户端 ID: {_client.ClientId}");
        }

        /// <summary>
        /// 运行自动化测试
        /// </summary>
        public async Task RunAutomatedTestAsync()
        {
            if (!_isRunning)
            {
                Console.WriteLine("请先启动测试平台");
                return;
            }
            
            Console.WriteLine("开始自动化测试...");
            Console.WriteLine("===================");
            
            string testTopic = "test/mqtt/platform";
            
            // 订阅测试主题
            await SubscribeAsync(testTopic);
            
            // 测试字符串消息
            await Task.Delay(1000);
            await PublishStringAsync(testTopic, "Hello MQTT Test Platform");
            
            // 测试对象消息
            await Task.Delay(1000);
            var testData = new TestData { Name = "Automated Test", Value = 999 };
            await PublishObjectAsync(testTopic, testData);
            
            // 测试字节数组消息
            await Task.Delay(1000);
            byte[] bytes = Encoding.UTF8.GetBytes("Binary Message Test");
            await PublishBytesAsync(testTopic, bytes);
            
            // 取消订阅
            await Task.Delay(1000);
            await UnsubscribeAsync(testTopic);
            
            Console.WriteLine("自动化测试完成");
        }

        private void OnMessageReceived(object sender, (string Topic, string Message) e)
        {
            Console.WriteLine($"[收到消息] 主题: {e.Topic} | 内容: {e.Message}");
        }

        private void OnConnectionStateChanged(object sender, bool connected)
        {
            Console.WriteLine($"[连接状态] {(connected ? "已连接" : "已断开")}");
        }
    }
}
