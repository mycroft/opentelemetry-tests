using Microsoft.AspNetCore.Mvc;

using System.IO;
using System.Net;

using System.Diagnostics;
using System.Diagnostics.Metrics;

using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace fo.Controllers
{
    [ApiController]
    [Route("/")]
    public class FrontEndController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly IMetricReporter _reporter;
        private static readonly ActivitySource Activity = new("Tracing", "1.0.0");

        public FrontEndController(ILogger<FrontEndController> logger, IMetricReporter reporter)
        {
            _logger = logger;
            _reporter = reporter;
        }

        [HttpGet("hello")]
        public string Get(string name = "world")
        {
            var remoteIpAddress = HttpContext.Connection.RemoteIpAddress;
            var remoteIpPort = HttpContext.Connection.RemotePort;

            _reporter.GetCounter().Add(1);
            _reporter.GetCounter().Add(1, new KeyValuePair<string, object>("name", name));

            using (var activity_foo = Activity.StartActivity("foo", ActivityKind.Producer)) {
                _logger.LogInformation($"Hello form the span! My name is {name}");
                activity_foo?.SetTag("client_port", remoteIpAddress + ":" + remoteIpPort);
                activity_foo?.AddEvent(new ActivityEvent("Starting request to backend."));

                activity_foo?.AddBaggage("client_port", remoteIpAddress + ":" + remoteIpPort);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:6000/reverse?name=" + name);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                var response_body = new StreamReader(response.GetResponseStream()).ReadToEnd();

                var event_tags = new ActivityTagsCollection();
                event_tags.Add("response.length", response_body.Length);
                event_tags.Add("response.body", response_body.ToString());
                
                activity_foo?.AddEvent(
                    new ActivityEvent("Got http response for the request.", default, event_tags)
                );

                return "hello " + response_body;
            };
        }
    }
}
