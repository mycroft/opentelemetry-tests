using Microsoft.AspNetCore.Mvc;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using RabbitMQ.Client;

using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace bo.Controllers
{
    [ApiController]
    [Route("/")]
    public class BackEndController : ControllerBase
    {
        private static readonly ActivitySource Source = new("Tracing", "1.0.0");
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

        private void InjectContextIntoHeader(IBasicProperties props, string key, string value)
        {
            try
            {
                props.Headers ??= new Dictionary<string, object>();
                props.Headers[key] = value;

                Console.WriteLine(key);
                Console.WriteLine(value);
            }
            catch (Exception)
            {
                Console.WriteLine("Failed to inject trace context.");
            }
        }

        [HttpGet("reverse")]
        public string Get(string name = "world")
        {
            string output;
    
            // Sending all baggages into this activity tags
            foreach (var (key, value) in Activity.Current?.Baggage)
            {
                Activity.Current?.SetTag(key, value);
            }

            using (var activity_foo = Source.StartActivity("foo", ActivityKind.Producer)) {
                activity_foo?.SetTag("input", name);

                // Extracting a single baggage item & inserting it as a tag.
                var baggage = Activity.Current?.Baggage.ToDictionary(item => item.Key);

                if (baggage.ContainsKey("client_port")) {
                    activity_foo?.SetTag("client_port", baggage["client_port"].Value);
                } 

                char[] nameArray = name.ToCharArray();
                Array.Reverse(nameArray);

                output = new string(nameArray);

                var event_tags = new ActivityTagsCollection();
                event_tags.Add("request.input", name);
                event_tags.Add("request.output", output);
                
                activity_foo?.AddEvent(
                    new ActivityEvent("Got http response for the reverse on backend request.", default, event_tags)
                );

                activity_foo?.SetTag("output", output);
            };

            using (var activity = Source.StartActivity("Sending message to Rabbitmq", ActivityKind.Producer))
            {
                var factory = new ConnectionFactory { HostName = "localhost" };
                using (var connection = factory.CreateConnection())
                using (var channel = connection.CreateModel())
                {
                    activity?.SetTag("messaging.system", "rabbitmq");
                    activity?.SetTag("messaging.destination_kind", "queue");
                    activity?.SetTag("messaging.rabbitmq.queue", "sample");

                    Console.WriteLine(activity.Baggage);
                    Console.WriteLine(Baggage.Current);

                    Baggage currentBaggage = Baggage.Current;

                    foreach (var (key, value) in Activity.Current?.Baggage)
                    {
                        Baggage.SetBaggage(key, value);
                    }

                    var props = channel.CreateBasicProperties();
                    Propagator.Inject(
                        new PropagationContext(
                            activity.Context,
                            Baggage.Current),
                        props,
                        InjectContextIntoHeader);

                    channel.QueueDeclare(queue: "sample",
                        durable: false,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    channel.BasicPublish(exchange: "",
                        routingKey: "sample",
                        basicProperties: props,
                        body: Encoding.UTF8.GetBytes(output));
                }
            };

            return output;
        }
    }
}
