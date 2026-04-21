using KupaKuper_DataProtocol.HTTP;

namespace HttpTestConsole
{
    public class ControllerExample : ControllerBase
    {
        [Route("/api/health", "GET", requireAuth: false)]
        public HttpResponse GetHealth(HttpRequest req)
        {
            return new HttpResponse(200, "OK", "text/plain");
        }

        [Route("/api/test", "GET", requireAuth: false)]
        public HttpResponse GetEndpoint(HttpRequest req)
        {
            return new HttpResponse(200, "Test endpoint", "text/plain");
        }

        [Route("/api/test", "POST", requireAuth: false)]
        public HttpResponse PostTest(HttpRequest req)
        {
            return new HttpResponse(201, $"Received: {req.Body}", "text/plain");
        }

        [Route("/api/test", "PUT", requireAuth: false)]
        public HttpResponse PutTest(HttpRequest req)
        {
            return new HttpResponse(200, $"Updated: {req.Body}", "text/plain");
        }

        [Route("/api/test", "DELETE", requireAuth: false)]
        public HttpResponse DeleteTest(HttpRequest req)
        {
            return new HttpResponse(200, "Deleted", "text/plain");
        }

        [Route("/api/test", "PATCH", requireAuth: false)]
        public HttpResponse PatchTest(HttpRequest req)
        {
            return new HttpResponse(200, $"Patched: {req.Body}", "text/plain");
        }
    }
}
