package me.mkz.oteltest;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

import io.opentelemetry.api.trace.Span;
import io.opentelemetry.api.trace.StatusCode;
import io.opentelemetry.context.Scope;

import org.springframework.web.bind.annotation.RequestParam;

@RestController
public class HelloWorldController {
    private final GreetingService greetingService;
    private final TracingService tracingService;

    public HelloWorldController(TracingService tracingService, GreetingService greetingService) {
        this.greetingService = greetingService;
        this.tracingService = tracingService;
    }

	@GetMapping("/")
	public String index() {
		return "Greetings from Spring Boot!";
	}

    @GetMapping("/greeting")
    public String greeting(@RequestParam(value = "name", defaultValue = "World") String name) {
        Span span = this.tracingService.getTracer().spanBuilder("greeting").startSpan();
        String output = "";

        try (Scope scope = span.makeCurrent()) {
            output = this.greetingService.getHello(name);
        } catch (Throwable t) {
            span.setStatus(StatusCode.ERROR, "Could not retrieve the string from GreetingService");
        } finally {
            span.end();
        }

        return output;
    }
}
