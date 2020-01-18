## 0.1.7 (18.01.2020):

* WebRequestTransport: fixed an obscure exception that could happen due to a race condition on CancellationTokenSource when using ForkingRequestStrategy.
* WebRequestTransport: converted almost all (save for unexpected exceptions) error logging from ERROR to WARN level.

## 0.1.6 (04.12.2019):

* WebRequestTransportSettings: `Pipelined`, `TcpKeepAliveEnabled` and `ArpCacheWarmupEnabled` options are now disabled by default.

## 0.1.5 (14-08-2019):

Fixed a bug where a network error while reading content could cause the transport to return a response with headers or partial body.

## 0.1.4 (20-03-2019):

* WebRequestTransportSettings: BufferFactory and FixNonAsciiHeaders options are now public.
* WebRequestTransportSettings: FixNonAsciiHeaders option now also affects response headers.

## 0.1.3 (13-03-2019):

Fixed bug with incorrect response code after request cancellation.

## 0.1.1 (03-03-2019): 

Implemented support for composite request body contents.

## 0.1.0 (04-02-2019): 

Initial prerelease.