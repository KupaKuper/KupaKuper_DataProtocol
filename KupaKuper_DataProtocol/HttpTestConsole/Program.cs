using KupaKuper_DataProtocol.HTTP;
namespace HttpTestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("HTTP 测试工具");
            Console.WriteLine("================");

            // 初始化 HTTP 服务器
            var httpServer = new HttpServer(port: 8888);

            // 注册测试路由
            httpServer.RegisterController<ControllerExample>();

            // 启动服务器
            Console.WriteLine("启动 HTTP 服务器...");
            httpServer.Start();
            Console.WriteLine("服务器已启动，监听端口 8888");

            // 等待服务器启动
            await Task.Delay(1000);

            // 测试客户端请求
            Console.WriteLine("\n测试客户端请求:");
            Console.WriteLine("================");

            // 测试 GET 请求
            await TestGetRequest();

            // 测试 POST 请求
            await TestPostRequest();

            // 测试 PUT 请求
            await TestPutRequest();

            // 测试 DELETE 请求
            await TestDeleteRequest();

            // 测试 PATCH 请求
            await TestPatchRequest();

            // 停止服务器
            Console.WriteLine("\n停止 HTTP 服务器...");
            httpServer.Stop();
            Console.WriteLine("服务器已停止");

            Console.WriteLine("\n测试完成，按任意键退出...");
            Console.ReadKey();
        }
        public static Data data = new Data();
        static void RegisterTestRoutes(HttpServer server)
        {
            // 健康检查路由
            server.Get("/api/health", (req) =>
            {
                return new HttpResponse(200, "OK", "text/plain");
            });

            // 测试路由
            server.Get("/api/test", (req) =>
            {
                return new HttpResponse(200, "Test endpoint", "text/plain");
            });

            // POST 测试路由
            server.Post("/api/test", (req) =>
            {
                return new HttpResponse(201, $"Received: {req.Body}", "text/plain");
            });

            // PUT 测试路由
            server.Put("/api/test", (req) =>
            {
                return new HttpResponse(200, $"Updated: {req.Body}", "text/plain");
            });

            // DELETE 测试路由
            server.Delete("/api/test", (req) =>
            {
                return new HttpResponse(200, "Deleted", "text/plain");
            });

            // PATCH 测试路由
            server.Patch("/api/test", (req) =>
            {
                return new HttpResponse(200, $"Patched: {req.Body}", "text/plain");
            });
        }

        static async Task TestGetRequest()
        {
            Console.WriteLine("\n测试 GET 请求:");
            string url = "http://localhost:8888/api/health";
            string response = await HttpClientHelp.GetStringAsync(url);
            Console.WriteLine($"URL: {url}");
            Console.WriteLine($"响应: {response}");

            url = "http://localhost:8888/api/test";
            response = await HttpClientHelp.GetStringAsync(url);
            Console.WriteLine($"URL: {url}");
            Console.WriteLine($"响应: {response}");
        }

        static async Task TestPostRequest()
        {
            Console.WriteLine("\n测试 POST 请求:");
            string url = "http://localhost:8888/api/test";
            data.Name = "Test";
            data.Value = 123;
            string response = await HttpClientHelp.PostStringAsync<Data>(url, data);
            Console.WriteLine($"URL: {url}");
            Console.WriteLine($"数据: {data}");
            Console.WriteLine($"响应: {response}");
        }

        static async Task TestPutRequest()
        {
            Console.WriteLine("\n测试 PUT 请求:");
            string url = "http://localhost:8888/api/test";
            data.Name = "Updated Test";
            data.Value = 456;
            string response = await HttpClientHelp.PutStringAsync<Data>(url, data);
            Console.WriteLine($"URL: {url}");
            Console.WriteLine($"数据: {data}");
            Console.WriteLine($"响应: {response}");
        }

        static async Task TestDeleteRequest()
        {
            Console.WriteLine("\n测试 DELETE 请求:");
            string url = "http://localhost:8888/api/test";
            string response = await HttpClientHelp.DeleteAsync(url);
            Console.WriteLine($"URL: {url}");
            Console.WriteLine($"响应: {response}");
        }

        static async Task TestPatchRequest()
        {
            Console.WriteLine("\n测试 PATCH 请求:");
            string url = "http://localhost:8888/api/test";
            data.Value = 789;
            string response = await HttpClientHelp.PatchStringAsync<Data>(url, data);
            Console.WriteLine($"URL: {url}");
            Console.WriteLine($"数据: {data}");
            Console.WriteLine($"响应: {response}");
        }
    }
    public class Data
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }
}