using Microsoft.AspNetCore.Mvc;

using System.IO;
using System.Net;

using System.Diagnostics;

namespace fo.Controllers
{
    [ApiController]
    [Route("/")]
    public class FrontEndController : ControllerBase
    {
        private static readonly ActivitySource Activity = new("Tracing", "1.0.0");

        [HttpGet("hello")]
        public string Get(string name = "world")
        {
            var remoteIpAddress = HttpContext.Connection.RemoteIpAddress;
            var remoteIpPort = HttpContext.Connection.RemotePort;

            using (var activity_foo = Activity.StartActivity("foo", ActivityKind.Producer)) {
                activity_foo?.SetTag("client_port", remoteIpAddress + ":" + remoteIpPort);
                activity_foo?.AddEvent(new ActivityEvent("Starting request to backend."));

                activity_foo.AddBaggage("client_port", remoteIpAddress + ":" + remoteIpPort);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:6000/reverse?name=patrick");
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
