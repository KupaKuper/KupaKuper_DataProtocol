using KupaKuper_DataProtocol.HTTP;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HttpTestConsole
{
    public class HttpTestPlatform
    {
        private Http_Server _server;
        private bool _isRunning = false;
        private int _port = 8888;

        public HttpTestPlatform(int port = 8888)
        {
            _port = port;
            _server = new Http_Server(port: _port);
            
            // 设置日志目录
            _server.LogDirectory = "D:\\HTTP_Logs";
            _server.LogToFile = true;
            
            // 订阅日志事件
            _server.Log += (msg) => Console.WriteLine($"[服务器日志] {msg}");
        }

        /// <summary>
        /// 启动测试平台
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            
            Console.WriteLine($"HTTP 测试平台启动中...");
            Console.WriteLine("===================");
            
            // 注册控制器路由
            _server.RegisterController<ControllerExample>();
            
            // 启动服务器
            _server.Start();
            
            _isRunning = true;
            Console.WriteLine($"HTTP 测试平台已启动，监听端口 {_port}");
        }

        /// <summary>
        /// 停止测试平台
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;
            
            Console.WriteLine("HTTP 测试平台停止中...");
            _server.Stop();
            
            _isRunning = false;
            Console.WriteLine("HTTP 测试平台已停止");
        }

        /// <summary>
        /// 测试 GET 请求
        /// </summary>
        public async Task TestGetAsync(string url = null)
        {
            if (!_isRunning)
            {
                Console.WriteLine("请先启动测试平台");
                return;
            }
            
            url = url ?? $"http://localhost:{_port}/api/health";
            
            try
            {
                string response = await Http_Client.GetStringAsync(url);
                Console.WriteLine($"GET 请求成功");
                Console.WriteLine($"URL: {url}");
                Console.WriteLine($"响应: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GET 请求失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试 POST 请求
        /// </summary>
        public async Task TestPostAsync(string url = null, object data = null)
        {
            if (!_isRunning)
            {
                Console.WriteLine("请先启动测试平台");
                return;
            }
            
            url = url ?? $"http://localhost:{_port}/api/test";
            data = data ?? new { Name = "Test", Value = 123, Timestamp = DateTime.Now };
            
            try
            {
                string response = await Http_Client.PostStringAsync(url, data);
                Console.WriteLine($"POST 请求成功");
                Console.WriteLine($"URL: {url}");
                Console.WriteLine($"数据: {System.Text.Json.JsonSerializer.Serialize(data)}");
                Console.WriteLine($"响应: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"POST 请求失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试 PUT 请求
        /// </summary>
        public async Task TestPutAsync(string url = null, object data = null)
        {
            if (!_isRunning)
            {
                Console.WriteLine("请先启动测试平台");
                return;
            }
            
            url = url ?? $"http://localhost:{_port}/api/test";
            data = data ?? new { Name = "Updated Test", Value = 456, Timestamp = DateTime.Now };
            
            try
            {
                string response = await Http_Client.PutStringAsync(url, data);
                Console.WriteLine($"PUT 请求成功");
                Console.WriteLine($"URL: {url}");
                Console.WriteLine($"数据: {System.Text.Json.JsonSerializer.Serialize(data)}");
                Console.WriteLine($"响应: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PUT 请求失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试 DELETE 请求
        /// </summary>
        public async Task TestDeleteAsync(string url = null)
        {
            if (!_isRunning)
            {
                Console.WriteLine("请先启动测试平台");
                return;
            }
            
            url = url ?? $"http://localhost:{_port}/api/test";
            
            try
            {
                string response = await Http_Client.DeleteAsync(url);
                Console.WriteLine($"DELETE 请求成功");
                Console.WriteLine($"URL: {url}");
                Console.WriteLine($"响应: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DELETE 请求失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试 PATCH 请求
        /// </summary>
        public async Task TestPatchAsync(string url = null, object data = null)
        {
            if (!_isRunning)
            {
                Console.WriteLine("请先启动测试平台");
                return;
            }
            
            url = url ?? $"http://localhost:{_port}/api/test";
            data = data ?? new { Value = 789, Timestamp = DateTime.Now };
            
            try
            {
                string response = await Http_Client.PatchStringAsync(url, data);
                Console.WriteLine($"PATCH 请求成功");
                Console.WriteLine($"URL: {url}");
                Console.WriteLine($"数据: {System.Text.Json.JsonSerializer.Serialize(data)}");
                Console.WriteLine($"响应: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PATCH 请求失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试带自定义头的请求
        /// </summary>
        public async Task TestWithHeadersAsync(string method = "GET", string url = null, Dictionary<string, string> headers = null)
        {
            if (!_isRunning)
            {
                Console.WriteLine("请先启动测试平台");
                return;
            }
            
            url = url ?? $"http://localhost:{_port}/api/health";
            headers = headers ?? new Dictionary<string, string>
            {
                { "X-Custom-Header", "TestValue" },
                { "User-Agent", "HTTP-Test-Platform/1.0" }
            };
            
            try
            {
                string response = await Http_Client.GetStringAsync(url, headers);
                Console.WriteLine($"{method} 请求成功（带自定义头）");
                Console.WriteLine($"URL: {url}");
                Console.WriteLine($"自定义头: {System.Text.Json.JsonSerializer.Serialize(headers)}");
                Console.WriteLine($"响应: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{method} 请求失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示服务器状态
        /// </summary>
        public void ShowStatus()
        {
            Console.WriteLine($"服务器状态: {(_isRunning ? "运行中" : "已停止")}");
            Console.WriteLine($"监听端口: {_port}");
            Console.WriteLine($"服务器IP: {_server.ServerIP}");
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
            
            // 测试 GET 请求
            await Task.Delay(500);
            await TestGetAsync();
            
            // 测试 POST 请求
            await Task.Delay(500);
            await TestPostAsync();
            
            // 测试 PUT 请求
            await Task.Delay(500);
            await TestPutAsync();
            
            // 测试 DELETE 请求
            await Task.Delay(500);
            await TestDeleteAsync();
            
            // 测试 PATCH 请求
            await Task.Delay(500);
            await TestPatchAsync();
            
            // 测试带自定义头的请求
            await Task.Delay(500);
            await TestWithHeadersAsync();
            
            Console.WriteLine("自动化测试完成");
        }
    }

    class Program
    {
        private static HttpTestPlatform _testPlatform = new HttpTestPlatform();

        static async Task Main(string[] args)
        {
            Console.WriteLine("HTTP 测试平台");
            Console.WriteLine("===============");
            Console.WriteLine("1. 启动平台");
            Console.WriteLine("2. 停止平台");
            Console.WriteLine("3. 测试 GET 请求");
            Console.WriteLine("4. 测试 POST 请求");
            Console.WriteLine("5. 测试 PUT 请求");
            Console.WriteLine("6. 测试 DELETE 请求");
            Console.WriteLine("7. 测试 PATCH 请求");
            Console.WriteLine("8. 测试带自定义头请求");
            Console.WriteLine("9. 显示状态");
            Console.WriteLine("10. 自动化测试");
            Console.WriteLine("0. 退出");
            Console.WriteLine("===============");

            bool running = true;
            while (running)
            {
                Console.Write("请选择操作 (0-10): ");
                string input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        StartPlatform();
                        break;
                    case "2":
                        StopPlatform();
                        break;
                    case "3":
                        await TestGetAsync();
                        break;
                    case "4":
                        await TestPostAsync();
                        break;
                    case "5":
                        await TestPutAsync();
                        break;
                    case "6":
                        await TestDeleteAsync();
                        break;
                    case "7":
                        await TestPatchAsync();
                        break;
                    case "8":
                        await TestWithHeadersAsync();
                        break;
                    case "9":
                        ShowStatus();
                        break;
                    case "10":
                        await RunAutomatedTestAsync();
                        break;
                    case "0":
                        running = false;
                        StopPlatform();
                        break;
                    default:
                        Console.WriteLine("无效选择，请重新输入");
                        break;
                }

                Console.WriteLine();
            }

            Console.WriteLine("HTTP 测试平台已退出");
        }

        static void StartPlatform()
        {
            try
            {
                _testPlatform.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"启动失败: {ex.Message}");
            }
        }

        static void StopPlatform()
        {
            try
            {
                _testPlatform.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"停止失败: {ex.Message}");
            }
        }

        static async Task TestGetAsync()
        {
            Console.Write("请输入 URL (回车使用默认): ");
            string url = Console.ReadLine();
            await _testPlatform.TestGetAsync(string.IsNullOrEmpty(url) ? null : url);
        }

        static async Task TestPostAsync()
        {
            Console.Write("请输入 URL (回车使用默认): ");
            string url = Console.ReadLine();
            await _testPlatform.TestPostAsync(string.IsNullOrEmpty(url) ? null : url);
        }

        static async Task TestPutAsync()
        {
            Console.Write("请输入 URL (回车使用默认): ");
            string url = Console.ReadLine();
            await _testPlatform.TestPutAsync(string.IsNullOrEmpty(url) ? null : url);
        }

        static async Task TestDeleteAsync()
        {
            Console.Write("请输入 URL (回车使用默认): ");
            string url = Console.ReadLine();
            await _testPlatform.TestDeleteAsync(string.IsNullOrEmpty(url) ? null : url);
        }

        static async Task TestPatchAsync()
        {
            Console.Write("请输入 URL (回车使用默认): ");
            string url = Console.ReadLine();
            await _testPlatform.TestPatchAsync(string.IsNullOrEmpty(url) ? null : url);
        }

        static async Task TestWithHeadersAsync()
        {
            Console.Write("请输入 URL (回车使用默认): ");
            string url = Console.ReadLine();
            
            var headers = new Dictionary<string, string>();
            Console.WriteLine("输入自定义头 (格式: 键=值，输入空行结束):");
            
            while (true)
            {
                Console.Write("头信息: ");
                string header = Console.ReadLine();
                if (string.IsNullOrEmpty(header)) break;
                
                var parts = header.Split('=');
                if (parts.Length == 2)
                {
                    headers[parts[0].Trim()] = parts[1].Trim();
                }
            }
            
            await _testPlatform.TestWithHeadersAsync("GET", string.IsNullOrEmpty(url) ? null : url, headers);
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