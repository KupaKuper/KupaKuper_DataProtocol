using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace KupaKuper_DataProtocol.HTTP
{
    /// <summary>
    /// HTTP 服务器类，基于 MVC 模式实现
    /// </summary>
    public class HttpServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly string _basePath;
        private readonly string _logDirectory;
        private readonly Dictionary<string, RouteInfo> _routes;
        private readonly Dictionary<string, int> _requestRateLimit;
        private readonly object _rateLimitLock = new object();
        private bool _disposed = false;

        /// <summary>
        /// 获取服务器是否正在运行
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// 服务器配置
        /// </summary>
        public ServerConfig Config { get; }

        /// <summary>
        /// 初始化 HTTP 服务器
        /// </summary>
        /// <param name="basePath">服务器基础路径</param>
        /// <param name="port">服务器端口</param>
        /// <param name="logDirectory">日志目录，默认为运行目录</param>
        /// <param name="useHttps">是否使用 HTTPS</param>
        /// <param name="certificatePath">TLS 证书路径</param>
        /// <param name="certificatePassword">TLS 证书密码</param>
        public HttpServer(string basePath = "", int port = 8080, string logDirectory = null, bool useHttps = false, string certificatePath = null, string certificatePassword = null)
        {
            _basePath = basePath.TrimEnd('/');
            _logDirectory = logDirectory ?? Directory.GetCurrentDirectory();
            _routes = new Dictionary<string, RouteInfo>();
            _requestRateLimit = new Dictionary<string, int>();

            // 确保日志目录存在
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // 配置服务器
            Config = new ServerConfig
            {
                Port = port,
                UseHttps = useHttps,
                CertificatePath = certificatePath,
                CertificatePassword = certificatePassword
            };

            // 初始化监听器
            string prefix = useHttps ? "https://" : "http://";
            _listener = new HttpListener();
            string path = _basePath.TrimStart('/');
            string prefixPath = string.IsNullOrEmpty(path) ? "" : $"/{path}";
            string localhostPrefix = $"{prefix}localhost:{port}{prefixPath}";
            string ipPrefix = $"{prefix}127.0.0.1:{port}{prefixPath}";
            // 确保前缀以斜杠结尾
            if (!localhostPrefix.EndsWith('/'))
                localhostPrefix += "/";
            if (!ipPrefix.EndsWith('/'))
                ipPrefix += "/";
            _listener.Prefixes.Add(localhostPrefix);
            _listener.Prefixes.Add(ipPrefix);

            if (useHttps && !string.IsNullOrEmpty(certificatePath))
            {
                // 配置 TLS 证书
                ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;
            }

            // 注册默认路由
            RegisterDefaultRoutes();
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        public void Start()
        {
            if (IsRunning)
                return;

            _listener.Start();
            IsRunning = true;
            Log("Server started", LogLevel.Info);

            // 开始监听请求
            Task.Run(async () => await ListenAsync());
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            if (!IsRunning)
                return;

            _listener.Stop();
            IsRunning = false;
            Log("Server stopped", LogLevel.Info);
        }

        /// <summary>
        /// 注册路由
        /// </summary>
        /// <param name="path">路由路径</param>
        /// <param name="handler">请求处理函数</param>
        /// <param name="method">HTTP 方法</param>
        /// <param name="requireAuth">是否需要认证</param>
        public void RegisterRoute(string path, Func<HttpRequest, HttpResponse> handler, string method = "GET", bool requireAuth = false)
        {
            string key = $"{method.ToUpper()}:{path.ToLower()}";
            _routes[key] = new RouteInfo
            {
                Path = path,
                Handler = handler,
                Method = method.ToUpper(),
                RequireAuth = requireAuth
            };
        }

        /// <summary>
        /// 注册控制器
        /// </summary>
        /// <typeparam name="T">控制器类型</typeparam>
        public void RegisterController<T>() where T : ControllerBase, new()
        {
            var controller = new T();
            var methods = typeof(T).GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes<RouteAttribute>();
                foreach (var attribute in attributes)
                {
                    RegisterRoute(attribute.Path, (req) => controller.InvokeMethod(method.Name, req), attribute.Method, attribute.RequireAuth);
                }
            }
        }

        /// <summary>
        /// 获取所有注册的接口
        /// </summary>
        /// <returns>接口列表</returns>
        public IEnumerable<RouteInfo> GetAllRoutes()
        {
            return _routes.Values;
        }

        /// <summary>
        /// 监听并处理请求
        /// </summary>
        private async Task ListenAsync()
        {
            while (IsRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequestAsync(context));
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995) // 操作已被取消
                {
                    // 服务器停止时的正常异常
                }
                catch (Exception ex)
                {
                    Log($"Error in listener: {ex.Message}", LogLevel.Error);
                }
            }
        }

        /// <summary>
        /// 处理请求
        /// </summary>
        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            string requestId = Guid.NewGuid().ToString();
            DateTime startTime = DateTime.Now;
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            try
            {
                // 检查请求频率
                if (!CheckRateLimit(request.RemoteEndPoint.ToString()))
                {
                    await SendResponseAsync(response, 429, "Too Many Requests");
                    LogRequest(requestId, request, string.Empty, 429, "Too Many Requests", DateTime.Now - startTime);
                    return;
                }

                // 构建请求对象
                var httpRequest = await BuildHttpRequestAsync(request);
                string requestBody = httpRequest.Body;

                // 查找路由
                string routeKey = $"{request.HttpMethod}:{request.Url.LocalPath.ToLower()}";
                if (_routes.TryGetValue(routeKey, out var route))
                {
                    // 检查认证
                    if (route.RequireAuth && !AuthenticateRequest(request))
                    {
                        await SendResponseAsync(response, 401, "Unauthorized");
                        LogRequest(requestId, request, requestBody, 401, "Unauthorized", DateTime.Now - startTime);
                        return;
                    }

                    // 执行处理函数
                    var httpResponse = route.Handler(httpRequest);
                    string responseBody = httpResponse.Content;
                    await SendResponseAsync(response, httpResponse.StatusCode, responseBody, httpResponse.ContentType);
                    LogRequest(requestId, request, requestBody, httpResponse.StatusCode, responseBody, DateTime.Now - startTime);
                }
                else
                {
                    await SendResponseAsync(response, 404, "Not Found");
                    LogRequest(requestId, request, requestBody, 404, "Not Found", DateTime.Now - startTime);
                }
            }
            catch (Exception ex)
            {
                await SendResponseAsync(response, 500, "Internal Server Error");
                Log($"Error processing request: {ex.Message}", LogLevel.Error);
                LogRequest(requestId, request, string.Empty, 500, "Internal Server Error", DateTime.Now - startTime);
            }
            finally
            {
                response.Close();
            }
        }

        /// <summary>
        /// 构建 HTTP 请求对象
        /// </summary>
        private async Task<HttpRequest> BuildHttpRequestAsync(HttpListenerRequest request)
        {
            string body = string.Empty;
            if (request.HasEntityBody)
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    body = await reader.ReadToEndAsync();
                }
            }

            // 解析查询参数
            var queryParams = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(request.Url.Query))
            {
                var query = request.Url.Query.Substring(1);
                foreach (var param in query.Split('&'))
                {
                    var parts = param.Split('=');
                    if (parts.Length == 2)
                    {
                        queryParams[parts[0]] = Uri.UnescapeDataString(parts[1]);
                    }
                }
            }

            return new HttpRequest
            {
                Method = request.HttpMethod,
                Path = request.Url.LocalPath,
                QueryParams = queryParams,
                Body = body,
                Headers = request.Headers.AllKeys.ToDictionary(key => key, key => request.Headers[key])
            };
        }

        /// <summary>
        /// 发送响应
        /// </summary>
        private async Task SendResponseAsync(HttpListenerResponse response, int statusCode, string content, string contentType = "text/plain")
        {
            response.StatusCode = statusCode;
            response.ContentType = contentType;
            
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            
            using (var output = response.OutputStream)
            {
                await output.WriteAsync(buffer, 0, buffer.Length);
            }
        }

        /// <summary>
        /// 检查请求频率限制
        /// </summary>
        private bool CheckRateLimit(string clientIP)
        {
            lock (_rateLimitLock)
            {
                if (_requestRateLimit.TryGetValue(clientIP, out int count))
                {
                    if (count > 100) // 每分钟限制 100 个请求
                        return false;
                    
                    _requestRateLimit[clientIP] = count + 1;
                }
                else
                {
                    _requestRateLimit[clientIP] = 1;
                }
            }

            // 定期清理过期的请求计数
            Task.Run(() =>
            {
                Thread.Sleep(60000); // 1 分钟后清理
                lock (_rateLimitLock)
                {
                    _requestRateLimit.Remove(clientIP);
                }
            });

            return true;
        }

        /// <summary>
        /// 认证请求
        /// </summary>
        private bool AuthenticateRequest(HttpListenerRequest request)
        {
            // 简单的 JWT 认证实现
            string authHeader = request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authHeader))
            {
                if (authHeader.StartsWith("Bearer "))
                {
                    string token = authHeader.Substring(7);
                    // 这里应该验证 JWT token
                    // 简化实现，实际应用中应该使用完整的 JWT 验证
                    return token.Length > 0;
                }
            }
            return false;
        }

        /// <summary>
        /// 验证服务器证书
        /// </summary>
        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // 生产环境应该进行更严格的证书验证
            return true;
        }

        /// <summary>
        /// 记录请求
        /// </summary>
        private void LogRequest(string requestId, HttpListenerRequest request, string requestBody, int statusCode, string responseBody, TimeSpan duration)
        {
            string logMessage = $"[{DateTime.Now}] RequestID: {requestId} | {request.HttpMethod} {request.Url.LocalPath} | Status: {statusCode} | Duration: {duration.TotalMilliseconds}ms | Client: {request.RemoteEndPoint}";
            Log(logMessage, LogLevel.Info);
            
            // 记录请求体
            if (!string.IsNullOrEmpty(requestBody))
            {
                Log($"Request Body: {requestBody}", LogLevel.Info);
            }
            
            // 记录响应体
            if (!string.IsNullOrEmpty(responseBody))
            {
                Log($"Response Body: {responseBody}", LogLevel.Info);
            }
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        private void Log(string message, LogLevel level)
        {
            string logFile = Path.Combine(_logDirectory, $"server_{DateTime.Now:yyyy-MM-dd}.log");
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
            
            try
            {
                File.AppendAllText(logFile, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log: {ex.Message}");
            }
        }

        /// <summary>
        /// 注册默认路由
        /// </summary>
        private void RegisterDefaultRoutes()
        {
            RegisterRoute("/api/routes", (req) =>
            {
                var routes = GetAllRoutes();
                string json = JsonSerializer.Serialize(routes);
                return new HttpResponse(200, json, "application/json");
            });

            RegisterRoute("/api/health", (req) =>
            {
                return new HttpResponse(200, "OK", "text/plain");
            });
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Stop();
                    _listener.Close();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~HttpServer()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// 服务器配置
    /// </summary>
    public class ServerConfig
    {
        /// <summary>
        /// 服务器端口
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 是否使用 HTTPS
        /// </summary>
        public bool UseHttps { get; set; }

        /// <summary>
        /// TLS 证书路径
        /// </summary>
        public string CertificatePath { get; set; }

        /// <summary>
        /// TLS 证书密码
        /// </summary>
        public string CertificatePassword { get; set; }
    }

    /// <summary>
    /// 路由信息
    /// </summary>
    public class RouteInfo
    {
        /// <summary>
        /// 路由路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// HTTP 方法
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 请求处理函数
        /// </summary>
        public Func<HttpRequest, HttpResponse> Handler { get; set; }

        /// <summary>
        /// 是否需要认证
        /// </summary>
        public bool RequireAuth { get; set; }
    }

    /// <summary>
    /// HTTP 请求
    /// </summary>
    public class HttpRequest
    {
        /// <summary>
        /// HTTP 方法
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 请求路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 查询参数
        /// </summary>
        public Dictionary<string, string> QueryParams { get; set; }

        /// <summary>
        /// 请求体
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// 请求头
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }
    }

    /// <summary>
    /// HTTP 响应
    /// </summary>
    public class HttpResponse
    {
        /// <summary>
        /// 状态码
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 响应内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 内容类型
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// 初始化 HTTP 响应
        /// </summary>
        /// <param name="statusCode">状态码</param>
        /// <param name="content">响应内容</param>
        /// <param name="contentType">内容类型</param>
        public HttpResponse(int statusCode, string content, string contentType = "text/plain")
        {
            StatusCode = statusCode;
            Content = content;
            ContentType = contentType;
        }
    }

    /// <summary>
    /// 控制器基类
    /// </summary>
    public abstract class ControllerBase
    {
        /// <summary>
        /// 调用方法
        /// </summary>
        /// <param name="methodName">方法名</param>
        /// <param name="request">HTTP 请求</param>
        /// <returns>HTTP 响应</returns>
        public HttpResponse InvokeMethod(string methodName, HttpRequest request)
        {
            var method = GetType().GetMethod(methodName);
            if (method == null)
                return new HttpResponse(404, "Method not found");

            try
            {
                var result = method.Invoke(this, new object[] { request });
                return result as HttpResponse;
            }
            catch (Exception ex)
            {
                return new HttpResponse(500, $"Internal server error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 路由属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RouteAttribute : Attribute
    {
        /// <summary>
        /// 路由路径
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// HTTP 方法
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// 是否需要认证
        /// </summary>
        public bool RequireAuth { get; }

        /// <summary>
        /// 初始化路由属性
        /// </summary>
        /// <param name="path">路由路径</param>
        /// <param name="method">HTTP 方法</param>
        /// <param name="requireAuth">是否需要认证</param>
        public RouteAttribute(string path, string method = "GET", bool requireAuth = false)
        {
            Path = path;
            Method = method.ToUpper();
            RequireAuth = requireAuth;
        }
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// HTTP 服务器扩展方法
    /// </summary>
    public static class HttpServerExtensions
    {
        /// <summary>
        /// 注册 GET 路由
        /// </summary>
        public static void Get(this HttpServer server, string path, Func<HttpRequest, HttpResponse> handler, bool requireAuth = false)
        {
            server.RegisterRoute(path, handler, "GET", requireAuth);
        }

        /// <summary>
        /// 注册 POST 路由
        /// </summary>
        public static void Post(this HttpServer server, string path, Func<HttpRequest, HttpResponse> handler, bool requireAuth = false)
        {
            server.RegisterRoute(path, handler, "POST", requireAuth);
        }

        /// <summary>
        /// 注册 PUT 路由
        /// </summary>
        public static void Put(this HttpServer server, string path, Func<HttpRequest, HttpResponse> handler, bool requireAuth = false)
        {
            server.RegisterRoute(path, handler, "PUT", requireAuth);
        }

        /// <summary>
        /// 注册 DELETE 路由
        /// </summary>
        public static void Delete(this HttpServer server, string path, Func<HttpRequest, HttpResponse> handler, bool requireAuth = false)
        {
            server.RegisterRoute(path, handler, "DELETE", requireAuth);
        }

        /// <summary>
        /// 注册 PATCH 路由
        /// </summary>
        public static void Patch(this HttpServer server, string path, Func<HttpRequest, HttpResponse> handler, bool requireAuth = false)
        {
            server.RegisterRoute(path, handler, "PATCH", requireAuth);
        }
    }
}
