using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace worker
{
    class Program
    {
        private static readonly ActivitySource Source = new(nameof(Program));
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

        private static TracerProvider SetupOpenTelemetry()
        {
            return Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("worker"))
                .AddSource(nameof(Program))
                .AddJaegerExporter(opts =>
                {
                    opts.AgentHost = "localhost";
                    opts.AgentPort = 6831;
                    opts.ExportProcessorType = ExportProcessorType.Simple;
                })
                .AddConsoleExporter()
                .Build();
        }

        private static IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
        {
            try
            {
                if (props.Headers.TryGetValue(key, out var value))
                {
                    var bytes = value as byte[];
                    return new[] { Encoding.UTF8.GetString(bytes) };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to extract trace context: {ex}");
            }

            return Enumerable.Empty<string>();
        }

        private static void ProcessMessage(BasicDeliverEventArgs ea, IModel rabbitMqChannel)
        {
            var parentContext = Propagator.Extract(
                default,
                ea.BasicProperties,
                ExtractTraceContextFromBasicProperties);

            Baggage.Current = parentContext.Baggage;

            using (var activity = Source.StartActivity("Worker: Processing Message", ActivityKind.Consumer, parentContext.ActivityContext))
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    foreach(var (key, value) in Baggage.Current) {
                        activity?.SetBaggage(key, value);
                    }

                    activity?.SetTag("messaging.system", "rabbitmq");
                    activity?.SetTag("messaging.destination_kind", "queue");
                    activity?.SetTag("messaging.rabbitmq.queue", "sample");

                    var baggage = activity?.Baggage.ToDictionary(item => item.Key);
                    var client = "unknown";

                    if (baggage.ContainsKey("client_port")) {
                        activity?.SetTag("client_port", baggage["client_port"].Value);
                        client = baggage["client_port"].Value;
                    } 

                    activity?.AddEvent(new ActivityEvent($"Recieved a message \"{message}\" from client:{client}"));

                    rabbitMqChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch(Exception ex)
                {
                    Console.Write($"Got an error while processing message: {ex}");
                }
            }
        }

        public static void RunQueue()
        {
            var factory = new ConnectionFactory() { HostName = "localhost", DispatchConsumersAsync = true };
            var rabbitMqConnection = factory.CreateConnection();
            var rabbitMqChannel = rabbitMqConnection.CreateModel();

            rabbitMqChannel.QueueDeclare(queue: "sample",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            rabbitMqChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new AsyncEventingBasicConsumer(rabbitMqChannel);

            // To fix
            consumer.Received += async (model, ea) =>
                ProcessMessage(ea, rabbitMqChannel);

            rabbitMqChannel.BasicConsume(queue: "sample",
                autoAck: false,
                consumer: consumer);
        }

        static void Main(string[] args)
        {
            using var openTelemetry = SetupOpenTelemetry();

            Console.WriteLine("Hello World!");
            RunQueue();

            System.Console.WriteLine("Press [enter] to exit.");
            System.Console.ReadLine();
        }
    }
}
