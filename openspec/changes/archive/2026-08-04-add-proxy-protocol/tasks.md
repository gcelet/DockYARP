## 1. Parser (AG-DEP)
- [x] 1.1 `ProxyProtocolHeader` (readonly struct, `SourceEndPoint`) + `ProxyProtocolParseStatus` enum
- [x] 1.2 `ProxyProtocolParser.Parse(span, out header, out consumed)` — v1 (TCP4/TCP6/UNKNOWN) + v2
      (INET/INET6/LOCAL/UNSPEC), with NeedMoreData / Invalid / Done

## 2. Connection middleware (AG-DEP)
- [x] 2.1 `ProxyProtocolConnectionMiddleware`: read header from `connection.Transport.Input`, set
      `RemoteEndPoint` when a source is present, advance past the header, abort on invalid; then call next

## 3. Config + wiring (AG-DEP / AG-AT)
- [x] 3.1 `ServerEndpointOptions.EnableProxyProtocol` (bool, default false)
- [x] 3.2 `KestrelTlsConfigurator`: attach the middleware to both listeners when enabled

## 4. Tests (AG-DEP)
- [x] 4.1 `ProxyProtocolParser`: v1 TCP4/TCP6/UNKNOWN, v2 INET/INET6/LOCAL, truncated→NeedMoreData, bad→Invalid
- [x] 4.2 `ProxyProtocolConnectionMiddleware`: over an in-memory connection pipe a v1 and a v2 header set
      `RemoteEndPoint` and leave the following HTTP bytes readable; an invalid header aborts

## 5. Docs (AG-DOC)
- [x] 5.1 Site configuration reference: document `Server:EnableProxyProtocol`

## 6. Verify (AG-DEP)
- [x] 6.1 Nuke `Test` gate green (unit/integration, no Docker)
