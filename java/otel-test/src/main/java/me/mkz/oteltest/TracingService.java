package me.mkz.oteltest;

import java.util.concurrent.TimeUnit;

import org.springframework.stereotype.Service;

import io.grpc.ManagedChannel;
import io.grpc.ManagedChannelBuilder;

import io.opentelemetry.api.OpenTelemetry;
import io.opentelemetry.api.common.Attributes;

import io.opentelemetry.exporter.jaeger.JaegerGrpcSpanExporter;

import io.opentelemetry.sdk.OpenTelemetrySdk;
import io.opentelemetry.sdk.resources.Resource;
import io.opentelemetry.sdk.trace.SdkTracerProvider;
import io.opentelemetry.sdk.trace.export.SimpleSpanProcessor;

import io.opentelemetry.semconv.resource.attributes.ResourceAttributes;

import io.opentelemetry.api.trace.Tracer;

@Service
public class TracingService {
    private final OpenTelemetry ot;

    public TracingService() {
        this.ot = initOpenTelemetry("localhost", 14250);
    }

    public Tracer getTracer() {
        return this.ot.getTracer("hello-world-controller", "1.0.0");
    }

    public OpenTelemetry initOpenTelemetry(String jaegerHost, int jaegerPort) {
        ManagedChannel jaegerChannel =
            ManagedChannelBuilder.forAddress(jaegerHost, jaegerPort).usePlaintext().build();
    
        JaegerGrpcSpanExporter jaegerExporter =
            JaegerGrpcSpanExporter.builder()
                .setChannel(jaegerChannel)
                .setTimeout(30, TimeUnit.SECONDS)
                .build();
    
        Resource serviceNameResource =
            Resource.create(Attributes.of(ResourceAttributes.SERVICE_NAME, "otel-test"));
        
        SdkTracerProvider tracerProvider =
            SdkTracerProvider.builder()
                .addSpanProcessor(SimpleSpanProcessor.create(jaegerExporter))
                .setResource(Resource.getDefault().merge(serviceNameResource))
                .build();
    
        OpenTelemetrySdk openTelemetry =
            OpenTelemetrySdk.builder().setTracerProvider(tracerProvider).build();
    
        Runtime.getRuntime().addShutdownHook(new Thread(tracerProvider::close));
    
        return openTelemetry;           
    }    
}



