// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Http
{
    internal enum HttpConnectionKind : byte
    {
        Http,               // Non-secure connection with no proxy. 没有代理的非安全连接。

        Https,              // Secure connection with no proxy.无代理的安全连接。

        Proxy,              // HTTP proxy usage for non-secure (HTTP) requests.
                            // 非安全（HTTP）请求的HTTP代理使用。


        ProxyTunnel,        // Non-secure websocket (WS) connection using CONNECT tunneling through proxy.
                            // 通过代理使用CONNECT隧道的非安全websocket（WS）连接。
                            // HTTP 代理隧道

        SslProxyTunnel,     // HTTP proxy usage for secure (HTTPS/WSS) requests using SSL and proxy CONNECT.
                            // 使用SSL和代理CONNECT进行安全（HTTPS/WSS）请求的HTTP代理使用。

        ProxyConnect,       // Connection used for proxy CONNECT. Tunnel will be established on top of this.
                            // 用于代理CONNECT的连接。隧道将建在上面。

        SocksTunnel,        // SOCKS proxy usage for HTTP requests.HTTP请求的SOCKS代理使用情况。

        SslSocksTunnel      // SOCKS proxy usage for HTTPS requests. HTTPS请求的SOCKS代理使用情况。
    }
}
