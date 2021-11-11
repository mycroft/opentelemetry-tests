import flask
from flask import request
import requests

from opentelemetry import baggage, trace
from opentelemetry.instrumentation.flask import FlaskInstrumentor
from opentelemetry.instrumentation.requests import RequestsInstrumentor
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import (
    BatchSpanProcessor,
    ConsoleSpanExporter,
)
from opentelemetry.sdk.resources import SERVICE_NAME, Resource
from opentelemetry.exporter.jaeger.thrift import JaegerExporter

# Used to retrieve hostname
import socket
from opentelemetry.context import get_current
from opentelemetry.propagate import inject

trace.set_tracer_provider(TracerProvider(
    resource=Resource.create({SERVICE_NAME: "web-frontend"})
))
exporter = JaegerExporter(
    collector_endpoint="http://localhost:14268/api/traces"
)
trace.get_tracer_provider().add_span_processor(
    BatchSpanProcessor(exporter)
)

app = flask.Flask(__name__)
FlaskInstrumentor().instrument_app(app)
RequestsInstrumentor().instrument()

tracer = trace.get_tracer(__name__)

@app.route("/")
def hello():
    name = request.args.get('name', default = "", type = str)
    with tracer.start_as_current_span("frontend-hello") as span:
        with tracer.start_as_current_span('frontend-with-baggage') as span:

            current_context = get_current()
            current_context = baggage.set_baggage('frontend_hostname', socket.gethostname(), current_context)
            headers = {}
            inject(headers, context=current_context)

            span.add_event("Calling backend.", attributes={'name': name})
            result = requests.get("http://localhost:5000/hello?name=" + name, headers=headers)
            return str(result.text)
    return "default response" # This should not happen.

app.run(port=8000)

