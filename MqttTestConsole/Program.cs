using System;
using System.Threading.Tasks;

namespace MqttTestConsole
{
    class Program
    {
        private static MqttTestPlatform _testPlatform = new MqttTestPlatform();

        static async Task Main(string[] args)
        {
            Console.WriteLine("MQTT 测试平台");
            Console.WriteLine("===============");
            Console.WriteLine("1. 启动平台");
            Console.WriteLine("2. 停止平台");
            Console.WriteLine("3. 订阅主题");
            Console.WriteLine("4. 取消订阅");
            Console.WriteLine("5. 发布消息");
            Console.WriteLine("6. 发布对象");
            Console.WriteLine("7. 发布字节数据");
            Console.WriteLine("8. 显示状态");
            Console.WriteLine("9. 自动化测试");
            Console.WriteLine("0. 退出");
            Console.WriteLine("===============");

            bool running = true;
            while (running)
            {
                Console.Write("请选择操作 (0-9): ");
                string input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        await StartPlatformAsync();
                        break;
                    case "2":
                        await StopPlatformAsync();
                        break;
                    case "3":
                        await SubscribeTopicAsync();
                        break;
                    case "4":
                        await UnsubscribeTopicAsync();
                        break;
                    case "5":
                        await PublishMessageAsync();
                        break;
                    case "6":
                        await PublishObjectAsync();
                        break;
                    case "7":
                        await PublishBytesAsync();
                        break;
                    case "8":
                        ShowStatus();
                        break;
                    case "9":
                        await RunAutomatedTestAsync();
                        break;
                    case "0":
                        running = false;
                        await StopPlatformAsync();
                        break;
                    default:
                        Console.WriteLine("无效选择，请重新输入");
                        break;
                }

                Console.WriteLine();
            }

            Console.WriteLine("MQTT 测试平台已退出");
        }

        static async Task StartPlatformAsync()
        {
            try
            {
                await _testPlatform.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"启动失败: {ex.Message}");
            }
        }

        static async Task StopPlatformAsync()
        {
            try
            {
                await _testPlatform.StopAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"停止失败: {ex.Message}");
            }
        }

        static async Task SubscribeTopicAsync()
        {
            Console.Write("请输入要订阅的主题: ");
            string topic = Console.ReadLine();
            if (!string.IsNullOrEmpty(topic))
            {
                await _testPlatform.SubscribeAsync(topic);
            }
        }

        static async Task UnsubscribeTopicAsync()
        {
            Console.Write("请输入要取消订阅的主题: ");
            string topic = Console.ReadLine();
            if (!string.IsNullOrEmpty(topic))
            {
                await _testPlatform.UnsubscribeAsync(topic);
            }
        }

        static async Task PublishMessageAsync()
        {
            Console.Write("请输入主题: ");
            string topic = Console.ReadLine();
            Console.Write("请输入消息内容: ");
            string message = Console.ReadLine();
            
            if (!string.IsNullOrEmpty(topic) && !string.IsNullOrEmpty(message))
            {
                await _testPlatform.PublishStringAsync(topic, message);
            }
        }

        static async Task PublishObjectAsync()
        {
            Console.Write("请输入主题: ");
            string topic = Console.ReadLine();
            
            if (!string.IsNullOrEmpty(topic))
            {
                var testData = new TestData
                {
                    Name = "手动测试",
                    Value = new Random().Next(100, 1000),
                    Timestamp = DateTime.Now
                };
                await _testPlatform.PublishObjectAsync(topic, testData);
            }
        }

        static async Task PublishBytesAsync()
        {
            Console.Write("请输入主题: ");
            string topic = Console.ReadLine();
            Console.Write("请输入要转换为字节的消息: ");
            string message = Console.ReadLine();
            
            if (!string.IsNullOrEmpty(topic) && !string.IsNullOrEmpty(message))
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message);
                await _testPlatform.PublishBytesAsync(topic, bytes);
            }
        }

        static void ShowStatus()
        {
            _testPlatform.ShowStatus();
        }

        static async Task RunAutomatedTestAsync()
        {
            await _testPlatform.RunAutomatedTestAsync();
        }
    }
}