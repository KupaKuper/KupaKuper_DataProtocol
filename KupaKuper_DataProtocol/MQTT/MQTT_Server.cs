using MQTTnet.Server;
using System.Security.Cryptography.X509Certificates;

namespace KupaKuper_DataProtocol.MQTT
{
    /// <summary>
    /// MQTT 服务器类，封装了 MQTTnet 库的服务器功能
    /// </summary>
    public class MQTT_Server
    {
        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning { get; private set; }
        /// <summary>
        /// MQTT 服务器选项构建器
        /// </summary>
        public MqttServerOptionsBuilder MqttServerOptionsBuilder { get; private set; }
        /// <summary>
        /// TLS 证书
        /// </summary>
        public X509Certificate2? X509Certificate2 { get; set; }
        /// <summary>
        /// MQTT 服务器实例
        /// </summary>
        public MqttServer? MqttServer { get; private set; }
        /// <summary>
        /// MQTT 服务器类，封装了 MQTTnet 库的服务器功能
        /// </summary>
        /// <param name="port">服务器监听端口</param>
        /// <param name="backlog">连接队列大小</param>
        /// <param name="useTls">是否使用 TLS 加密</param>
        public MQTT_Server(int port, int backlog, bool useTls = false)
        {
            // 1. 配置服务器选项
            MqttServerOptionsBuilder = new MqttServerOptionsBuilder()
            .WithConnectionBacklog(backlog)// 设置连接队列大小
            .WithDefaultEndpointPort(port);// 监听默认的 MQTT 端口
            if (useTls && X509Certificate2 != null)
            {
                // 配置 TLS 加密（示例，实际使用时需要提供证书等信息）
                MqttServerOptionsBuilder.WithEncryptedEndpoint()
                    .WithEncryptionCertificate(X509Certificate2);
            }
        }
        /// <summary>
        /// 启动 MQTT 服务器
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            if (MqttServer == null)
            {
                // 2. 使用工厂创建并启动服务器实例
                MqttServer = new MqttServerFactory().CreateMqttServer(MqttServerOptionsBuilder.Build());
            }
            await MqttServer.StartAsync();
            IsRunning = true;
            Console.WriteLine($"MQTT 服务器已启动，监听端口 {MqttServerOptionsBuilder.Build().DefaultEndpointOptions.Port}...");
        }
        /// <summary>
        /// 关闭 MQTT 服务器
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            if (MqttServer == null || !IsRunning)
            {
                Console.WriteLine("MQTT 服务器未运行，无需停止。");
                return;
            }
            // 3. 停止服务器
            await MqttServer.StopAsync();
            IsRunning = false;
            Console.WriteLine("MQTT 服务器已停止。");
        }
    }
}