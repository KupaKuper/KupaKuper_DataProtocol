using System.Text;
using System.Net.Http.Headers;
using System.IO;

namespace KupaKuper_DataProtocol.HTTP
{
    /// <summary>
    /// 一个简单的HttpClient帮助类，提供了发送GET和POST请求的方法，并且在请求过程中处理异常。
    /// </summary>
    public static class Http_Client
    {
        /// <summary>
        /// HttpClient 是一个线程安全的类，建议在应用程序的整个生命周期内重用一个实例。
        /// </summary>
        private static readonly HttpClient client = new HttpClient();
        private static string _logDirectory;

        /// <summary>
        /// 获取HttpClient实例
        /// </summary>
        public static HttpClient Help { get { return client; } }

        /// <summary>
        /// 日志存储目录，默认存储到执行文件夹下的/log文件夹
        /// </summary>
        public static string LogDirectory
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
        public static bool LogToFile { get; set; } = true;
        
        /// <summary>
        /// 日志输出事件，可绑定自定义日志处理
        /// </summary>
        public static event Action<string> Log;
        
        static Http_Client()
        {
            // 设置默认超时时间为30秒
            client.Timeout = TimeSpan.FromSeconds(30);

            // 设置默认日志目录
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");

            // 确保日志目录存在
            EnsureLogDirectoryExists();
        }
        
        private static void EnsureLogDirectoryExists()
        {
            if (!string.IsNullOrEmpty(_logDirectory) && !Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }
        
        private static void LogMessage(string msg)
        {
            string fullMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}";
            
            // 触发外部日志事件
            Log?.Invoke(fullMsg);
            
            // 写入文件日志
            if (LogToFile)
            {
                try
                {
                    string logFile = Path.Combine(_logDirectory, $"http_client_{DateTime.Now:yyyy-MM-dd}.log");
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
        
        /// <summary>
        /// 设置超时时间
        /// </summary>
        /// <param name="seconds">超时时间（秒）</param>
        public static void SetTimeout(int seconds)
        {
            client.Timeout = TimeSpan.FromSeconds(seconds);
        }
        
        /// <summary>
        /// 添加默认请求头
        /// </summary>
        /// <param name="name">请求头名称</param>
        /// <param name="value">请求头值</param>
        public static void AddDefaultHeader(string name, string value)
        {
            if (!client.DefaultRequestHeaders.Contains(name))
            {
                client.DefaultRequestHeaders.Add(name, value);
            }
        }
        
        /// <summary>
        /// 设置认证令牌
        /// </summary>
        /// <param name="token">JWT令牌</param>
        public static void SetAuthToken(string token)
        {
            AddDefaultHeader("Authorization", $"Bearer {token}");
        }
        
        /// <summary>
        /// 发送GET请求
        /// </summary>
        /// <param name="url">请求的URL</param>
        /// <returns>返回响应的字符串内容</returns>
        public static async Task<string> GetStringAsync(string url)
        {
            try
            {
                // GET 请求
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode(); // 如果状态码不是 2xx，则抛出异常

                string responseBody = await response.Content.ReadAsStringAsync();
                LogMessage($"响应: {responseBody}");
                return responseBody;
            }
            catch (HttpRequestException e)
            {
                string errorMsg = $"请求异常: {e.Message}";
                LogMessage(errorMsg);
                return $"err:{e.Message}";
            }
        }
        
        /// <summary>
        /// 发送POST请求
        /// </summary>
        /// <typeparam name="T">请求内容的类型</typeparam>
        /// <param name="url">请求的URL</param>
        /// <param name="content">请求的内容</param>
        /// <param name="header">请求的头部信息，默认为 "application/json"</param>
        /// <returns>返回响应的字符串内容</returns>
        public static async Task<string> PostStringAsync<T>(string url, T content, string header = "application/json")
        {
            try
            {
                string? contentString = content is string ? content as string : System.Text.Json.JsonSerializer.Serialize(content);
                if (string.IsNullOrEmpty(contentString)) return string.Empty;
                // POST 请求
                HttpContent httpContent = new StringContent(contentString, Encoding.UTF8, header);
                HttpResponseMessage response = await client.PostAsync(url, httpContent);
                response.EnsureSuccessStatusCode(); // 如果状态码不是 2xx，则抛出异常
                string responseBody = await response.Content.ReadAsStringAsync();
                LogMessage($"响应: {responseBody}");
                return responseBody;
            }
            catch (HttpRequestException e)
            {
                string errorMsg = $"请求异常: {e.Message}";
                LogMessage(errorMsg);
                return $"err:{e.Message}";
            }
        }
        
        /// <summary>
        /// 发送POST请求，内容为字符串
        /// </summary>
        /// <param name="url">请求的URL</param>
        /// <param name="content">请求的内容</param>
        /// <param name="header">请求的头部信息，默认为 "application/json"</param>
        /// <returns>返回响应的字符串内容</returns>
        public static async Task<string> PostStringAsync(string url, string content, string header = "application/json")
        {
            try
            {
                if (string.IsNullOrEmpty(content)) return string.Empty;
                // POST 请求
                HttpContent httpContent = new StringContent(content, Encoding.UTF8, header);
                HttpResponseMessage response = await client.PostAsync(url, httpContent);
                response.EnsureSuccessStatusCode(); // 如果状态码不是 2xx，则抛出异常
                string responseBody = await response.Content.ReadAsStringAsync();
                LogMessage($"响应: {responseBody}");
                return responseBody;
            }
            catch (HttpRequestException e)
            {
                string errorMsg = $"请求异常: {e.Message}";
                LogMessage(errorMsg);
                return $"err:{e.Message}";
            }
        }
        
        /// <summary>
        /// 发送PUT请求
        /// </summary>
        /// <typeparam name="T">请求内容的类型</typeparam>
        /// <param name="url">请求的URL</param>
        /// <param name="content">请求的内容</param>
        /// <param name="header">请求的头部信息，默认为 "application/json"</param>
        /// <returns>返回响应的字符串内容</returns>
        public static async Task<string> PutStringAsync<T>(string url, T content, string header = "application/json")
        {
            try
            {
                string? contentString = content is string ? content as string : System.Text.Json.JsonSerializer.Serialize(content);
                if (string.IsNullOrEmpty(contentString)) return string.Empty;
                // PUT 请求
                HttpContent httpContent = new StringContent(contentString, Encoding.UTF8, header);
                HttpResponseMessage response = await client.PutAsync(url, httpContent);
                response.EnsureSuccessStatusCode(); // 如果状态码不是 2xx，则抛出异常
                string responseBody = await response.Content.ReadAsStringAsync();
                LogMessage($"响应: {responseBody}");
                return responseBody;
            }
            catch (HttpRequestException e)
            {
                string errorMsg = $"请求异常: {e.Message}";
                LogMessage(errorMsg);
                return $"err:{e.Message}";
            }
        }
        
        /// <summary>
        /// 发送PUT请求，内容为字符串
        /// </summary>
        /// <param name="url">请求的URL</param>
        /// <param name="content">请求的内容</param>
        /// <param name="header">请求的头部信息，默认为 "application/json"</param>
        /// <returns>返回响应的字符串内容</returns>
        public static async Task<string> PutStringAsync(string url, string content, string header = "application/json")
        {
            try
            {
                if (string.IsNullOrEmpty(content)) return string.Empty;
                // PUT 请求
                HttpContent httpContent = new StringContent(content, Encoding.UTF8, header);
                HttpResponseMessage response = await client.PutAsync(url, httpContent);
                response.EnsureSuccessStatusCode(); // 如果状态码不是 2xx，则抛出异常
                string responseBody = await response.Content.ReadAsStringAsync();
                LogMessage($"响应: {responseBody}");
                return responseBody;
            }
            catch (HttpRequestException e)
            {
                string errorMsg = $"请求异常: {e.Message}";
                LogMessage(errorMsg);
                return $"err:{e.Message}";
            }
        }
        
        /// <summary>
        /// 发送DELETE请求
        /// </summary>
        /// <param name="url">请求的URL</param>
        /// <returns>返回响应的字符串内容</returns>
        public static async Task<string> DeleteAsync(string url)
        {
            try
            {
                // DELETE 请求
                HttpResponseMessage response = await client.DeleteAsync(url);
                response.EnsureSuccessStatusCode(); // 如果状态码不是 2xx，则抛出异常
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);
                return responseBody;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"请求异常: {e.Message}");
                return $"err:{e.Message}";
            }
        }
        
        /// <summary>
        /// 发送PATCH请求
        /// </summary>
        /// <typeparam name="T">请求内容的类型</typeparam>
        /// <param name="url">请求的URL</param>
        /// <param name="content">请求的内容</param>
        /// <param name="header">请求的头部信息，默认为 "application/json"</param>
        /// <returns>返回响应的字符串内容</returns>
        public static async Task<string> PatchStringAsync<T>(string url, T content, string header = "application/json")
        {
            try
            {
                string? contentString = content is string ? content as string : System.Text.Json.JsonSerializer.Serialize(content);
                if (string.IsNullOrEmpty(contentString)) return string.Empty;
                // PATCH 请求
                HttpContent httpContent = new StringContent(contentString, Encoding.UTF8, header);
                HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                {
                    Content = httpContent
                };
                HttpResponseMessage response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode(); // 如果状态码不是 2xx，则抛出异常
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);
                return responseBody;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"请求异常: {e.Message}");
                return $"err:{e.Message}";
            }
        }
        
        /// <summary>
        /// 发送PATCH请求，内容为字符串
        /// </summary>
        /// <param name="url">请求的URL</param>
        /// <param name="content">请求的内容</param>
        /// <param name="header">请求的头部信息，默认为 "application/json"</param>
        /// <returns>返回响应的字符串内容</returns>
        public static async Task<string> PatchStringAsync(string url, string content, string header = "application/json")
        {
            try
            {
                if (string.IsNullOrEmpty(content)) return string.Empty;
                // PATCH 请求
                HttpContent httpContent = new StringContent(content, Encoding.UTF8, header);
                HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                {
                    Content = httpContent
                };
                HttpResponseMessage response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode(); // 如果状态码不是 2xx，则抛出异常
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);
                return responseBody;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"请求异常: {e.Message}");
                return $"err:{e.Message}";
            }
        }
        
        /// <summary>
        /// 发送带自定义请求头的GET请求
        /// </summary>
        /// <param name="url">请求的URL</param>
        /// <param name="headers">自定义请求头</param>
        /// <returns>返回响应的字符串内容</returns>
        public static async Task<string> GetStringAsync(string url, Dictionary<string, string> headers)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
                HttpResponseMessage response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode(); // 如果状态码不是 2xx，则抛出异常
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);
                return responseBody;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"请求异常: {e.Message}");
                return $"err:{e.Message}";
            }
        }
    }
}
