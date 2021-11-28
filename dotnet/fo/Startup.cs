using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

using OpenTelemetry.Metrics;

namespace fo
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            string TracingServiceName = this.Configuration.GetValue<string>("Tracing:ServiceName");
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "fo", Version = "v1" });
            });

            Sampler sampler = this.Configuration.GetValue<string>("Tracing:Sampler") switch {
                "AlwaysOn" => new AlwaysOnSampler(),
                "AlwaysOff" => new AlwaysOffSampler(),
                "TraceIdRatioBased" => new TraceIdRatioBasedSampler(
                    this.Configuration.GetValue<double>("Tracing:SamplingProbability")
                ),
                "ParentBasedAlwaysOn" => new ParentBasedSampler(new AlwaysOnSampler()),
                "ParentBasedAlwaysOff" => new ParentBasedSampler(new AlwaysOffSampler()),
                "WhiteListRatio" => new WhiteListRatioSampler(
                    TracingServiceName,
                    this.Configuration.GetSection("Tracing:AllowedServices").Get<List<string>>(),
                    this.Configuration.GetValue<double>("Tracing:SamplingProbability")
                ),
                "ParentBased" => new ParentBasedSampler(
                    new WhiteListRatioSampler(
                        TracingServiceName,
                        this.Configuration.GetSection("Tracing:AllowedServices").Get<List<string>>(),
                        this.Configuration.GetValue<double>("Tracing:SamplingProbability")
                    )
                ),
                _ => new AlwaysOnSampler(),
            };

            services.AddOpenTelemetryTracing(
                (builder) => builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(TracingServiceName))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("Tracing")
                    // .AddConsoleExporter()
                    .AddJaegerExporter(opts => {
                        opts.ExportProcessorType = ExportProcessorType.Simple;
                        opts.AgentHost = this.Configuration.GetValue<string>("Tracing:Host");
                        opts.AgentPort = this.Configuration.GetValue<int>("Tracing:Port");
                    })
                    // Define a sampler different that defaults
                    // Default is ParentBased(AlwaysOn)
                    .SetSampler(sampler)
            );

            services.AddOpenTelemetryMetrics(builder =>
            {
                builder.AddAspNetCoreInstrumentation();
                builder.AddMeter("Metrics"); // same name than in the controller
                builder.AddPrometheusExporter();
            });

            services.AddSingleton<IMetricReporter, MetricReporter>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "fo v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            // Enable Prometheus scraping endpoint
            app.UseOpenTelemetryPrometheusScrapingEndpoint();

            // Custom Middleware
            app.Use((context, next) => {
                var Source = new ActivitySource("Tracing", "1.0.0");
                var activityName = "Configured Middleware";

                Activity.Current.AddEvent(new ActivityEvent("testaroo"));

                using (var activity = Source.StartActivity(activityName, ActivityKind.Server, default(ActivityContext))) {
                    activity.AddEvent(
                        new ActivityEvent($"Before next invocation")
                    );

                    next.Invoke();

                    activity.AddEvent(
                        new ActivityEvent($"After invocation")
                    );

                    // Restore activity displayname as it will be overwritten for some reason.
                    activity.DisplayName = activityName;
                }

                return Task.CompletedTask;
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
