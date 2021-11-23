package me.mkz.oteltest;

import org.springframework.stereotype.Service;

import io.opentelemetry.api.trace.Span;
import io.opentelemetry.api.trace.StatusCode;
import io.opentelemetry.context.Scope;

@Service
public class GreetingService {
    private final TracingService tracingService;
    private static final String template = "Hello, %s!";

    public GreetingService(TracingService tracingService) {
        this.tracingService = tracingService;
    }

    public String getHello(String name) {
        Span span = this.tracingService.getTracer().spanBuilder("GreetingService::getHello").startSpan();
        String out = "";

        span.setAttribute("getHello::name", name);

        try (Scope scope = span.makeCurrent()) {
            span.addEvent("starting operation");
            out = String.format(template, name);
            span.addEvent("ending operation");
        } catch (Throwable t) {
            span.setStatus(StatusCode.ERROR, "Change it to your error message");
        } finally {
            span.end();
        }

        return out;
    }
}
