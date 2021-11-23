# Opentelemetry tests

This repository contains several tests I've made during my Opentelemetry tracing tests.
Some samples might contain also metrics, logs tests as well, but as those were still alpha/beta during the tests, they might become irrevelant someday.

# Requirements

We're testing tracing. You need a backend. I'm using jaeger with the official container image:

```sh
podman run -d --name jaeger \
  -e COLLECTOR_ZIPKIN_HOST_PORT=:9411 \
  -p 5775:5775/udp \
  -p 6831:6831/udp \
  -p 6832:6832/udp \
  -p 5778:5778 \
  -p 16686:16686 \
  -p 14268:14268 \
  -p 14250:14250 \
  -p 9411:9411 \
  jaegertracing/all-in-one:1.27
```

# Using dotnet

## Dotnet requirements

I'm testing async tracing by using rabbitmq. Don't forget to launch it as well:

```sh
podman run --name rabbitmq -d -p 5672:5672 -p 15672:15672 rabbitmq
```

## Run fo, bo & worker

In multiple terminals, run the different components:

```sh
cd bo ; dotnet build ; dotnet run
cd fo ; dotnet build ; dotnet run
cd worker ; dotnet build ; dotnet run
```

And test:

```sh
$ http --verify no https://localhost:5001/hello
HTTP/1.1 200 OK
Content-Type: text/plain; charset=utf-8
Date: Tue, 23 Nov 2021 13:41:45 GMT
Server: Kestrel
Transfer-Encoding: chunked

hello dlrow
```

