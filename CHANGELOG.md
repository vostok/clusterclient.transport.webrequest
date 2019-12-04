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