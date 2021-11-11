import flask
import requests
from flask import request

from opentelemetry import trace
from opentelemetry.instrumentation.flask import FlaskInstrumentor
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import (
    BatchSpanProcessor,
    ConsoleSpanExporter,
)
from opentelemetry.sdk.resources import SERVICE_NAME, Resource
from opentelemetry.exporter.jaeger.thrift import JaegerExporter
from opentelemetry.trace import Status, StatusCode
from opentelemetry.baggage import get_all

from opentelemetry import baggage
from opentelemetry.propagate import extract
import opentelemetry.instrumentation.wsgi as otel_wsgi

trace.set_tracer_provider(TracerProvider(
    resource=Resource.create({SERVICE_NAME: "backend-service"})
))
exporter = JaegerExporter(
    collector_endpoint="http://localhost:14268/api/traces"
)
trace.get_tracer_provider().add_span_processor(
    BatchSpanProcessor(exporter)
)

app = flask.Flask(__name__)
FlaskInstrumentor().instrument_app(app)

tracer = trace.get_tracer(__name__)

@app.route("/hello")
def hello():
    name = request.args.get('name', default = "", type = str)

    extracted_context = extract(request.environ, getter=otel_wsgi.wsgi_getter)
    bag_value = baggage.get_baggage('frontend_hostname', extracted_context)

    with tracer.start_as_current_span("backend-hello") as span:
        span.add_event("Doing backend here.", {
            'name': name,
            'frontend_hostname': bag_value, 
        })

        if name == 'joe':
            span.set_status(Status(StatusCode.ERROR))
            span.add_event("I don't like joe", {'anger': 'high'})

    return "hello " + name

app.run(port=5000)

