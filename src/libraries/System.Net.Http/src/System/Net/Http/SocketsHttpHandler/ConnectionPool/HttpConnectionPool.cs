// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http.HPack;
using System.Net.Http.QPack;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http
{
    /// <summary>Provides a pool of connections to the same endpoint.提供到同一端点的连接池</summary>
    internal sealed partial class HttpConnectionPool : IDisposable
    {
        /// <summary>The maximum number of times to retry a request after a failure on an established connection.
        /// 在已建立的连接失败后重试请求的最大次数。
        /// </summary>
        private const int MaxConnectionFailureRetries = 3;
        public const int DefaultHttpPort = 80;
        public const int DefaultHttpsPort = 443;

        private static readonly List<SslApplicationProtocol> s_http3ApplicationProtocols = new List<SslApplicationProtocol>() { SslApplicationProtocol.Http3 };
        private static readonly List<SslApplicationProtocol> s_http2ApplicationProtocols = new List<SslApplicationProtocol>() { SslApplicationProtocol.Http2, SslApplicationProtocol.Http11 };
        private static readonly List<SslApplicationProtocol> s_http2OnlyApplicationProtocols = new List<SslApplicationProtocol>() { SslApplicationProtocol.Http2 };

        private readonly HttpConnectionPoolManager _poolManager;
        private readonly HttpConnectionKind _kind;
        private readonly Uri? _proxyUri;

        /// <summary>The origin authority used to construct the <see cref="HttpConnectionPool"/>.
        ///用于构造<see cref="HttpConnectionPool"/>的源机构。
        /// </summary>
        private readonly HttpAuthority _originAuthority;

        /// <summary>
        /// The User-Agent header to use when creating a CONNECT tunnel.
        /// 创建CONNECT隧道时使用的User-Agent标头
        /// </summary>
        private string? _connectTunnelUserAgent;

        // These settings are advertised by the server via SETTINGS_MAX_HEADER_LIST_SIZE and SETTINGS_MAX_FIELD_SECTION_SIZE.
        // 服务器通过settings_MAX_HEADER_LIST_SIZE和settings_MAX_FIELD_SECTION_SIZE通告这些设置
        // If we had previous connections to the same host in this pool, memorize the last value seen.
        // 如果我们之前连接过此池中的同一主机，请记住最后一个看到的值
        // This value is used as an initial value for new connections before they have a chance to observe the SETTINGS frame.
        // 在新连接有机会观察SETTINGS帧之前，此值用作新连接的初始值。
        // Doing so avoids immediately exceeding the server limit on the first request, potentially causing the connection to be torn down.
        //这样做可以避免在第一次请求时立即超过服务器限制，从而可能导致连接中断。
        // 0 means there were no previous connections, or they hadn't advertised this limit.
        // 0表示以前没有连接，或者他们没有通告此限制。
        // There is no need to lock when updating these values - we're only interested in saving _a_ value, not necessarily the min/max/last.
        // 更新这些值时不需要锁定-我们只对保存_a_值感兴趣，不一定是min/max/last。
        internal uint _lastSeenHttp2MaxHeaderListSize;
        internal uint _lastSeenHttp3MaxHeaderListSize;

        /// <summary>Options specialized and cached for this pool and its key.
        /// 为此池及其密钥专门化和缓存的选项。</summary>
        private readonly SslClientAuthenticationOptions? _sslOptionsHttp11;
        private readonly SslClientAuthenticationOptions? _sslOptionsHttp2;
        private readonly SslClientAuthenticationOptions? _sslOptionsHttp2Only;
        private SslClientAuthenticationOptions? _sslOptionsHttp3;
        private readonly SslClientAuthenticationOptions? _sslOptionsProxy;

        private readonly PreAuthCredentialCache? _preAuthCredentials;

        /// <summary>Whether the pool has been used since the last time a cleanup occurred.
        /// 自上次清理以来，该池是否已被使用。</summary>
        private bool _usedSinceLastCleanup = true;
        /// <summary>Whether the pool has been disposed.游泳池是否已处理。</summary>
        private bool _disposed;

        /// <summary>Initializes the pool.</summary>
        /// <param name="poolManager">全局连接池管理器，提供共享配置（如超时、证书）</param>
        /// <param name="kind">连接类型（普通 HTTP/HTTPS、代理隧道等）</param>
        /// <param name="host">目标主机名（非代理场景必填）</param>
        /// <param name="port">	目标端口（非代理场景必填）</param>
        /// <param name="sslHostName">TLS SNI 主机名（HTTPS 必填）</param>
        /// <param name="proxyUri">代理地址（代理场景必填）</param>
        public HttpConnectionPool(HttpConnectionPoolManager poolManager, HttpConnectionKind kind, string? host, int port, string? sslHostName, Uri? proxyUri)
        {
            _poolManager = poolManager;
            _kind = kind;
            _proxyUri = proxyUri;
            _maxHttp11Connections = Settings._maxConnectionsPerServer;//从全局设置获取最大连接数
            //连接数限制：_maxHttp11Connections 控制 HTTP/1.1 连接池大小（HTTP/2/3 多路复用不受此限）

            // The only case where 'host' will not be set is if this is a Proxy connection pool.
            Debug.Assert(host is not null || (kind == HttpConnectionKind.Proxy && proxyUri is not null));
            _originAuthority = new HttpAuthority(host ?? proxyUri!.IdnHost, port);// ？

            #region 协议支持检测
            _http2Enabled = _poolManager.Settings._maxHttpVersion >= HttpVersion.Version20; //判断http2是否启用

            //判断http3是否启用
            if (IsHttp3Supported()) 
            { 
                _http3Enabled = _poolManager.Settings._maxHttpVersion >= HttpVersion.Version30;
            }
            #endregion

            //通过 switch(kind) 对每种连接类型的参数进行严格校验
            //典型场景：
            //普通 HTTPS：host + sslHostName
            //代理隧道：proxyUri + host
            //SOCKS：proxyUri + host + port
            switch (kind)
            {
                case HttpConnectionKind.Http:
                    Debug.Assert(host != null);
                    Debug.Assert(port != 0);
                    Debug.Assert(sslHostName == null);
                    Debug.Assert(proxyUri == null);

                    _http3Enabled = false;
                    break;

                case HttpConnectionKind.Https:
                    Debug.Assert(host != null);
                    Debug.Assert(port != 0);
                    Debug.Assert(sslHostName != null);
                    Debug.Assert(proxyUri == null);
                    break;

                case HttpConnectionKind.Proxy:
                    Debug.Assert(host == null);
                    Debug.Assert(port == 0);
                    Debug.Assert(sslHostName == null);
                    Debug.Assert(proxyUri != null);

                    _http2Enabled = false;
                    _http3Enabled = false;
                    break;

                case HttpConnectionKind.ProxyTunnel:
                    Debug.Assert(host != null);
                    Debug.Assert(port != 0);
                    Debug.Assert(sslHostName == null);
                    Debug.Assert(proxyUri != null);

                    _http2Enabled = false;
                    _http3Enabled = false;
                    break;

                case HttpConnectionKind.SslProxyTunnel:
                    Debug.Assert(host != null);
                    Debug.Assert(port != 0);
                    Debug.Assert(sslHostName != null);
                    Debug.Assert(proxyUri != null);

                    _http3Enabled = false; // TODO: how do we tunnel HTTP3?
                    break;

                case HttpConnectionKind.ProxyConnect:
                    Debug.Assert(host != null);
                    Debug.Assert(port != 0);
                    Debug.Assert(sslHostName == null);
                    Debug.Assert(proxyUri != null);
                    
                    _maxHttp11Connections = int.MaxValue;  //代理隧道不限制连接数

                    _http2Enabled = false; //代理不支持 HTTP/2+
                    _http3Enabled = false;  //代理不支持 HTTP/2+ 
                    break;

                case HttpConnectionKind.SocksTunnel:
                case HttpConnectionKind.SslSocksTunnel:
                    Debug.Assert(host != null);
                    Debug.Assert(port != 0);
                    Debug.Assert(proxyUri != null);

                    _http3Enabled = false; // TODO: SOCKS supports UDP and may be used for HTTP3
                    break;

                default:
                    Debug.Fail("Unknown HttpConnectionKind in HttpConnectionPool.ctor");
                    break;
            }

            if (!_http3Enabled)
            {
                // Avoid parsing Alt-Svc headers if they won't be used. 如果不会使用Alt-Svc标头，请避免解析它们。
                _altSvcEnabled = false;
            }

            string? hostHeader = null;
            if (host is not null)
            {
                // Precalculate ASCII bytes for Host header 预先计算主机标头的ASCII字节
                // Note that if _host is null, this is a (non-tunneled) proxy connection, and we can't cache the hostname.
                // 请注意，如果_host为null，则这是一个（非隧道）代理连接，我们无法缓存主机名。
                hostHeader = IsDefaultPort
                    ? _originAuthority.HostValue
                    : $"{_originAuthority.HostValue}:{_originAuthority.Port}";

                // Host 头预处理 Note the IDN hostname should always be ASCII, since it's already been IDNA encoded.
                byte[] hostHeaderLine = new byte[6 + hostHeader.Length + 2]; // Host: foo\r\n
                "Host: "u8.CopyTo(hostHeaderLine);
                Encoding.ASCII.GetBytes(hostHeader, hostHeaderLine.AsSpan(6));
                hostHeaderLine[^2] = (byte)'\r';
                hostHeaderLine[^1] = (byte)'\n';
                _hostHeaderLineBytes = hostHeaderLine;

                Debug.Assert(Encoding.ASCII.GetString(_hostHeaderLineBytes) == $"Host: {hostHeader}\r\n");
            }
            //sslHostName  TLS SNI 主机名（HTTPS 必填）
            if (sslHostName != null)
            {
                _sslOptionsHttp11 = ConstructSslOptions(poolManager, sslHostName);
                _sslOptionsHttp11.ApplicationProtocols = null;

                if (_http2Enabled)
                {
                    _sslOptionsHttp2 = ConstructSslOptions(poolManager, sslHostName);
                    _sslOptionsHttp2.ApplicationProtocols = s_http2ApplicationProtocols;
                    _sslOptionsHttp2Only = ConstructSslOptions(poolManager, sslHostName);
                    _sslOptionsHttp2Only.ApplicationProtocols = s_http2OnlyApplicationProtocols;

                    // Note:
                    // The HTTP/2 specification states:
                    //   "A deployment of HTTP/2 over TLS 1.2 MUST disable renegotiation.
                    //    An endpoint MUST treat a TLS renegotiation as a connection error (Section 5.4.1)
                    //    of type PROTOCOL_ERROR."
                    // which suggests we should do:
                    //   _sslOptionsHttp2.AllowRenegotiation = false;
                    // However, if AllowRenegotiation is set to false, that will also prevent
                    // renegotation if the server denies the HTTP/2 request and causes a
                    // downgrade to HTTP/1.1, and the current APIs don't provide a mechanism
                    // by which AllowRenegotiation could be set back to true in that case.
                    // For now, if an HTTP/2 server erroneously issues a renegotiation, we'll
                    // allow it.
                }
            }

            //HPack/QPack 头压缩
            if (hostHeader is not null)
            {
                if (_http2Enabled)
                {
                    _http2EncodedAuthorityHostHeader = HPackEncoder.EncodeLiteralHeaderFieldWithoutIndexingToAllocatedArray(H2StaticTable.Authority, hostHeader);
                }

                if (IsHttp3Supported() && _http3Enabled)
                {
                    _http3EncodedAuthorityHostHeader = QPackEncoder.EncodeLiteralHeaderFieldWithStaticNameReferenceToArray(H3StaticTable.Authority, hostHeader);
                }
            }

            // 预认证缓存 Set up for PreAuthenticate.  Access to this cache is guarded by a lock on the cache itself.
            if (_poolManager.Settings._preAuthenticate)
            {
                _preAuthCredentials = new PreAuthCredentialCache();
            }

            // 请求队列初始化
            _http11RequestQueue = new RequestQueue<HttpConnection>();
            if (_http2Enabled)
            {
                _http2RequestQueue = new RequestQueue<Http2Connection?>();
            }
            if (IsHttp3Supported() && _http3Enabled)
            {
                _http3RequestQueue = new RequestQueue<Http3Connection?>();
            }

            //TLS 配置复用
            if (_proxyUri != null && HttpUtilities.IsSupportedSecureScheme(_proxyUri.Scheme))
            {
                _sslOptionsProxy = ConstructSslOptions(poolManager, _proxyUri.IdnHost);
                _sslOptionsProxy.ApplicationProtocols = null;
            }

            if (NetEventSource.Log.IsEnabled()) Trace($"{this}");
        }

        private static SslClientAuthenticationOptions ConstructSslOptions(HttpConnectionPoolManager poolManager, string sslHostName)
        {
            Debug.Assert(sslHostName != null);

            SslClientAuthenticationOptions sslOptions = poolManager.Settings._sslOptions?.ShallowClone() ?? new SslClientAuthenticationOptions();

            // This is only set if we are underlying handler for HttpClientHandler
            if (poolManager.Settings._clientCertificateOptions == ClientCertificateOption.Manual && sslOptions.LocalCertificateSelectionCallback != null &&
                    (sslOptions.ClientCertificates == null || sslOptions.ClientCertificates.Count == 0))
            {
                // If we have no client certificates do not set callback when internal selection is used.
                // It breaks TLS resume on Linux
                sslOptions.LocalCertificateSelectionCallback = null;
            }

            // Set TargetHost for SNI
            sslOptions.TargetHost = sslHostName;

            return sslOptions;
        }

        public HttpAuthority OriginAuthority => _originAuthority;
        public HttpConnectionSettings Settings => _poolManager.Settings;
        public HttpConnectionKind Kind => _kind;
        public bool IsSecure => _kind == HttpConnectionKind.Https || _kind == HttpConnectionKind.SslProxyTunnel || _kind == HttpConnectionKind.SslSocksTunnel;
        public Uri? ProxyUri => _proxyUri;
        public ICredentials? ProxyCredentials => _poolManager.ProxyCredentials;
        public PreAuthCredentialCache? PreAuthCredentials => _preAuthCredentials;
        public bool IsDefaultPort => OriginAuthority.Port == (IsSecure ? DefaultHttpsPort : DefaultHttpPort);
        private bool DoProxyAuth => (_kind == HttpConnectionKind.Proxy || _kind == HttpConnectionKind.ProxyConnect);

        /// <summary>用于同步对池中状态的访问的对象 Object used to synchronize access to state in the pool.
        /// 定义了一个同步锁对象（SyncObj）和其状态检查属性（HasSyncObjLock），用于控制对 HttpConnectionPool 内部资源的线程安全访问
        /// </summary>
        private object SyncObj
        {
            //_http11Connections 是一个存储空闲 HTTP/1.1 连接的集合（实际为 Stack<HttpConnection>）。
            //多线程场景下（如并发请求），需确保对连接的获取/归还操作是原子的
            get
            {
                Debug.Assert(!Monitor.IsEntered(_http11Connections));
                return _http11Connections;
            }
        }

        public bool HasSyncObjLock => Monitor.IsEntered(_http11Connections);

        // Overview of connection management (mostly HTTP version independent):
        //
        // Each version of HTTP (1.1, 2, 3) has its own connection pool, and each of these work in a similar manner,
        // allowing for differences between the versions (most notably, HTTP/1.1 is not multiplexed.)
        //
        // When a request is submitted for a particular version (e.g. HTTP/1.1), we first look in the pool for available connections.
        // An "available" connection is one that is (hopefully) usable for a new request.
        //      For HTTP/1.1, this is just an idle connection.
        //      For HTTP2/3, this is a connection that (hopefully) has available streams to use for new requests.
        // If we find an available connection, we will attempt to validate it and then use it.
        //      We check the lifetime of the connection and discard it if the lifetime is exceeded.
        //      We check that the connection has not shut down; if so we discard it.
        //      For HTTP2/3, we reserve a stream on the connection. If this fails, we cannot use the connection right now.
        // If validation fails, we will attempt to find a different available connection.
        //
        // Once we have found a usable connection, we use it to process the request.
        //      For HTTP/1.1, a connection can handle only a single request at a time, thus it is immediately removed from the list of available connections.
        //      For HTTP2/3, a connection is only removed from the available list when it has no more available streams.
        //      In either case, the connection still counts against the total associated connection count for the pool.
        //
        // If we cannot find a usable available connection, then the request is added the to the request queue for the appropriate version.
        //
        // Whenever a request is queued, or an existing connection shuts down, we will check to see if we should inject a new connection.
        // Injection policy depends on both user settings and some simple heuristics.
        // See comments on the relevant routines for details on connection injection policy.
        //
        // When a new connection is successfully created, or an existing unavailable connection becomes available again,
        // we will attempt to use this connection to handle any queued requests (subject to lifetime restrictions on existing connections).
        // This may result in the connection becoming unavailable again, because it cannot handle any more requests at the moment.
        // If not, we will return the connection to the pool as an available connection for use by new requests.
        //
        // When a connection shuts down, either gracefully (e.g. GOAWAY) or abortively (e.g. IOException),
        // we will remove it from the list of available connections, if it is present there.
        // If not, then it must be unavailable at the moment; we will detect this and ensure it is not added back to the available pool.

        public ValueTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool async, bool doRequestAuth, CancellationToken cancellationToken)
        {
            // We need the User-Agent header when we send a CONNECT request to the proxy.
            // 当我们向代理发送CONNECT请求时，我们需要User-Agent标头。
            // We must read the header early, before we return the ownership of the request back to the user.
            // 在将请求的所有权返还给用户之前，我们必须尽早阅读标头。
            if ((Kind is HttpConnectionKind.ProxyTunnel or HttpConnectionKind.SslProxyTunnel) &&
                request.HasHeaders &&
                request.Headers.NonValidated.TryGetValues(HttpKnownHeaderNames.UserAgent, out HeaderStringValues userAgent))
            {
                _connectTunnelUserAgent = userAgent.ToString();
            }

            if (doRequestAuth && Settings._credentials != null)
            {
                return AuthenticationHelper.SendWithRequestAuthAsync(request, async, Settings._credentials, Settings._preAuthenticate, this, cancellationToken);
            }

            return SendWithProxyAuthAsync(request, async, doRequestAuth, cancellationToken);
        }

        public ValueTask<HttpResponseMessage> SendWithProxyAuthAsync(HttpRequestMessage request, bool async, bool doRequestAuth, CancellationToken cancellationToken)
        {
            if (DoProxyAuth && ProxyCredentials is not null)
            {
                return AuthenticationHelper.SendWithProxyAuthAsync(request, _proxyUri!, async, ProxyCredentials, doRequestAuth, this, cancellationToken);
            }

            return SendWithVersionDetectionAndRetryAsync(request, async, doRequestAuth, cancellationToken);
        }

        private Task<HttpResponseMessage> SendWithNtConnectionAuthAsync(HttpConnection connection, HttpRequestMessage request, bool async, bool doRequestAuth, CancellationToken cancellationToken)
        {
            if (doRequestAuth && Settings._credentials != null)
            {
                return AuthenticationHelper.SendWithNtConnectionAuthAsync(request, async, Settings._credentials, Settings._impersonationLevel, connection, this, cancellationToken);
            }

            return SendWithNtProxyAuthAsync(connection, request, async, cancellationToken);
        }

        public Task<HttpResponseMessage> SendWithNtProxyAuthAsync(HttpConnection connection, HttpRequestMessage request, bool async, CancellationToken cancellationToken)
        {
            if (DoProxyAuth && ProxyCredentials is not null)
            {
                return AuthenticationHelper.SendWithNtProxyAuthAsync(request, ProxyUri!, async, ProxyCredentials, HttpHandlerDefaults.DefaultImpersonationLevel, connection, this, cancellationToken);
            }

            return connection.SendAsync(request, async, cancellationToken);
        }

        public async ValueTask<HttpResponseMessage> SendWithVersionDetectionAndRetryAsync(HttpRequestMessage request, bool async, bool doRequestAuth, CancellationToken cancellationToken)
        {
            _usedSinceLastCleanup = true;

            // Loop on connection failures (or other problems like version downgrade) and retry if possible.
            int retryCount = 0;
            while (true)
            {
                HttpConnectionWaiter<HttpConnection>? http11ConnectionWaiter = null;
                HttpConnectionWaiter<Http2Connection?>? http2ConnectionWaiter = null;
                try
                {
                    HttpResponseMessage? response = null;

                    // Use HTTP/3 if possible.
                    if (IsHttp3Supported() && // guard to enable trimming HTTP/3 support
                        _http3Enabled &&
                        (request.Version.Major >= 3 || (request.VersionPolicy == HttpVersionPolicy.RequestVersionOrHigher && IsSecure)) &&
                        !request.IsExtendedConnectRequest)
                    {
                        Debug.Assert(async);
                        if (QuicConnection.IsSupported)
                        {
                            if (_sslOptionsHttp3 == null)
                            {
                                // deferred creation. We use atomic exchange to be sure all threads point to single object to mimic ctor behavior.
                                SslClientAuthenticationOptions sslOptionsHttp3 = ConstructSslOptions(_poolManager, _sslOptionsHttp11!.TargetHost!);
                                sslOptionsHttp3.ApplicationProtocols = s_http3ApplicationProtocols;
                                Interlocked.CompareExchange(ref _sslOptionsHttp3, sslOptionsHttp3, null);
                            }

                            response = await TrySendUsingHttp3Async(request, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            _altSvcEnabled = false;
                            _http3Enabled = false;
                        }
                    }

                    if (response is null)
                    {
                        // We could not use HTTP/3. Do not continue if downgrade is not allowed.
                        if (request.Version.Major >= 3 && request.VersionPolicy != HttpVersionPolicy.RequestVersionOrLower)
                        {
                            ThrowGetVersionException(request, 3);
                        }

                        // Use HTTP/2 if possible.
                        if (_http2Enabled &&
                            (request.Version.Major >= 2 || (request.VersionPolicy == HttpVersionPolicy.RequestVersionOrHigher && IsSecure)) &&
                            (request.VersionPolicy != HttpVersionPolicy.RequestVersionOrLower || IsSecure)) // prefer HTTP/1.1 if connection is not secured and downgrade is possible
                        {
                            if (!TryGetPooledHttp2Connection(request, out Http2Connection? connection, out http2ConnectionWaiter) &&
                                http2ConnectionWaiter != null)
                            {
                                connection = await http2ConnectionWaiter.WaitForConnectionAsync(request, this, async, cancellationToken).ConfigureAwait(false);
                            }

                            Debug.Assert(connection is not null || !_http2Enabled);
                            if (connection is not null)
                            {
                                if (request.IsExtendedConnectRequest)
                                {
                                    await connection.InitialSettingsReceived.WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
                                    if (!connection.IsConnectEnabled)
                                    {
                                        HttpRequestException exception = new(HttpRequestError.ExtendedConnectNotSupported, SR.net_unsupported_extended_connect);
                                        exception.Data["SETTINGS_ENABLE_CONNECT_PROTOCOL"] = false;
                                        throw exception;
                                    }
                                }

                                response = await connection.SendAsync(request, async, cancellationToken).ConfigureAwait(false);
                            }
                        }

                        if (response is null)
                        {
                            // We could not use HTTP/2. Do not continue if downgrade is not allowed.
                            if (request.Version.Major >= 2 && request.VersionPolicy != HttpVersionPolicy.RequestVersionOrLower)
                            {
                                ThrowGetVersionException(request, 2);
                            }

                            // Use HTTP/1.x.
                            if (!TryGetPooledHttp11Connection(request, async, out HttpConnection? connection, out http11ConnectionWaiter))
                            {
                                connection = await http11ConnectionWaiter.WaitForConnectionAsync(request, this, async, cancellationToken).ConfigureAwait(false);
                            }

                            connection.Acquire(); // In case we are doing Windows (i.e. connection-based) auth, we need to ensure that we hold on to this specific connection while auth is underway.
                            try
                            {
                                response = await SendWithNtConnectionAuthAsync(connection, request, async, doRequestAuth, cancellationToken).ConfigureAwait(false);
                            }
                            finally
                            {
                                connection.Release();
                            }
                        }
                    }

                    ProcessAltSvc(response);
                    return response;
                }
                catch (HttpRequestException e) when (e.AllowRetry == RequestRetryType.RetryOnConnectionFailure)
                {
                    Debug.Assert(retryCount >= 0 && retryCount <= MaxConnectionFailureRetries);

                    if (retryCount == MaxConnectionFailureRetries)
                    {
                        if (NetEventSource.Log.IsEnabled())
                        {
                            Trace($"MaxConnectionFailureRetries limit of {MaxConnectionFailureRetries} hit. Retryable request will not be retried. Exception: {e}");
                        }

                        throw;
                    }

                    retryCount++;

                    if (NetEventSource.Log.IsEnabled())
                    {
                        Trace($"Retry attempt {retryCount} after connection failure. Connection exception: {e}");
                    }

                    // Eat exception and try again.
                }
                catch (HttpRequestException e) when (e.AllowRetry == RequestRetryType.RetryOnLowerHttpVersion)
                {
                    // Throw if fallback is not allowed by the version policy.
                    if (request.VersionPolicy != HttpVersionPolicy.RequestVersionOrLower)
                    {
                        throw new HttpRequestException(HttpRequestError.VersionNegotiationError, SR.Format(SR.net_http_requested_version_server_refused, request.Version, request.VersionPolicy), e);
                    }

                    if (NetEventSource.Log.IsEnabled())
                    {
                        Trace($"Retrying request because server requested version fallback: {e}");
                    }

                    // Eat exception and try again on a lower protocol version.
                    request.Version = HttpVersion.Version11;
                }
                catch (HttpRequestException e) when (e.AllowRetry == RequestRetryType.RetryOnStreamLimitReached)
                {
                    if (NetEventSource.Log.IsEnabled())
                    {
                        Trace($"Retrying request on another HTTP/2 connection after active streams limit is reached on existing one: {e}");
                    }

                    // Eat exception and try again.
                }
                finally
                {
                    // We never cancel both attempts at the same time. When downgrade happens, it's possible that both waiters are non-null,
                    // but in that case http2ConnectionWaiter.ConnectionCancellationTokenSource shall be null.
                    Debug.Assert(http11ConnectionWaiter is null || http2ConnectionWaiter?.ConnectionCancellationTokenSource is null);
                    http11ConnectionWaiter?.SetTimeoutToPendingConnectionAttempt(this, cancellationToken.IsCancellationRequested);
                    http2ConnectionWaiter?.SetTimeoutToPendingConnectionAttempt(this, cancellationToken.IsCancellationRequested);
                }
            }
        }

        private async ValueTask<(Stream, TransportContext?, Activity?, IPEndPoint?)> ConnectAsync(HttpRequestMessage request, bool async, CancellationToken cancellationToken)
        {
            Stream? stream = null;
            IPEndPoint? remoteEndPoint = null;
            Exception? exception = null;
            TransportContext? transportContext = null;

            Activity? activity = ConnectionSetupDistributedTracing.StartConnectionSetupActivity(IsSecure, OriginAuthority);

            try
            {
                switch (_kind)
                {
                    case HttpConnectionKind.Http:
                    case HttpConnectionKind.Https:
                    case HttpConnectionKind.ProxyConnect:
                        stream = await ConnectToTcpHostAsync(_originAuthority.IdnHost, _originAuthority.Port, request, async, cancellationToken).ConfigureAwait(false);
                        // remoteEndPoint is returned for diagnostic purposes.
                        remoteEndPoint = GetRemoteEndPoint(stream);
                        if (_kind == HttpConnectionKind.ProxyConnect && _sslOptionsProxy != null)
                        {
                            stream = await ConnectHelper.EstablishSslConnectionAsync(_sslOptionsProxy, request, async, stream, cancellationToken).ConfigureAwait(false);
                        }
                        break;

                    case HttpConnectionKind.Proxy:
                        stream = await ConnectToTcpHostAsync(_proxyUri!.IdnHost, _proxyUri.Port, request, async, cancellationToken).ConfigureAwait(false);
                        // remoteEndPoint is returned for diagnostic purposes.
                        remoteEndPoint = GetRemoteEndPoint(stream);
                        if (_sslOptionsProxy != null)
                        {
                            stream = await ConnectHelper.EstablishSslConnectionAsync(_sslOptionsProxy, request, async, stream, cancellationToken).ConfigureAwait(false);
                        }
                        break;

                    case HttpConnectionKind.ProxyTunnel:
                    case HttpConnectionKind.SslProxyTunnel:
                        stream = await EstablishProxyTunnelAsync(async, cancellationToken).ConfigureAwait(false);

                        if (stream is HttpContentStream contentStream && contentStream._connection?._stream is Stream innerStream)
                        {
                            remoteEndPoint = GetRemoteEndPoint(innerStream);
                        }

                        break;

                    case HttpConnectionKind.SocksTunnel:
                    case HttpConnectionKind.SslSocksTunnel:
                        stream = await EstablishSocksTunnel(request, async, cancellationToken).ConfigureAwait(false);
                        // remoteEndPoint is returned for diagnostic purposes.
                        remoteEndPoint = GetRemoteEndPoint(stream);
                        break;
                }

                Debug.Assert(stream != null);

                if (IsSecure)
                {
                    SslStream? sslStream = stream as SslStream;
                    if (sslStream == null)
                    {
                        sslStream = await ConnectHelper.EstablishSslConnectionAsync(GetSslOptionsForRequest(request), request, async, stream, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        if (NetEventSource.Log.IsEnabled())
                        {
                            Trace($"Connected with custom SslStream: alpn='${sslStream.NegotiatedApplicationProtocol}'");
                        }
                    }
                    transportContext = sslStream.TransportContext;
                    stream = sslStream;
                }
            }
            catch (Exception ex) when (activity is not null)
            {
                exception = ex;
                throw;
            }
            finally
            {
                if (activity is not null)
                {
                    ConnectionSetupDistributedTracing.StopConnectionSetupActivity(activity, exception, remoteEndPoint);
                }
            }

            return (stream, transportContext, activity, remoteEndPoint);

            static IPEndPoint? GetRemoteEndPoint(Stream stream) => (stream as NetworkStream)?.Socket?.RemoteEndPoint as IPEndPoint;
        }

        private async ValueTask<Stream> ConnectToTcpHostAsync(string host, int port, HttpRequestMessage initialRequest, bool async, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var endPoint = new DnsEndPoint(host, port);
            Stream? stream = null;
            try
            {
                // If a ConnectCallback was supplied, use that to establish the connection.
                if (Settings._connectCallback != null)
                {
                    ValueTask<Stream> streamTask = Settings._connectCallback(new SocketsHttpConnectionContext(endPoint, initialRequest), cancellationToken);

                    if (!async && !streamTask.IsCompleted)
                    {
                        // User-provided ConnectCallback is completing asynchronously but the user is making a synchronous request; if the user cares, they should
                        // set it up so that synchronous requests are made on a handler with a synchronously-completing ConnectCallback supplied. If in the future,
                        // we could add a Boolean to SocketsHttpConnectionContext (https://github.com/dotnet/runtime/issues/44876) to let the callback know whether
                        // this request is sync or async.
                        Trace($"{nameof(SocketsHttpHandler.ConnectCallback)} completing asynchronously for a synchronous request.");
                    }

                    stream = await streamTask.ConfigureAwait(false) ?? throw new HttpRequestException(SR.net_http_null_from_connect_callback);
                }
                else
                {
                    // Otherwise, create and connect a socket using default settings.
                    Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        if (async)
                        {
                            await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            using (cancellationToken.UnsafeRegister(static s => ((Socket)s!).Dispose(), socket))
                            {
                                socket.Connect(endPoint);
                            }
                        }

                        stream = new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }

                return stream;
            }
            catch (Exception ex)
            {
                throw ex is OperationCanceledException oce && oce.CancellationToken == cancellationToken ?
                    CancellationHelper.CreateOperationCanceledException(innerException: null, cancellationToken) :
                    ConnectHelper.CreateWrappedException(ex, host, port, cancellationToken);
            }
        }

        private SslClientAuthenticationOptions GetSslOptionsForRequest(HttpRequestMessage request)
        {
            if (_http2Enabled)
            {
                if (request.Version.Major >= 2 && request.VersionPolicy != HttpVersionPolicy.RequestVersionOrLower)
                {
                    return _sslOptionsHttp2Only!;
                }

                if (request.Version.Major >= 2 || request.VersionPolicy == HttpVersionPolicy.RequestVersionOrHigher)
                {
                    return _sslOptionsHttp2!;
                }
            }
            return _sslOptionsHttp11!;
        }

        private async ValueTask<Stream> ApplyPlaintextFilterAsync(bool async, Stream stream, Version httpVersion, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Settings._plaintextStreamFilter is null)
            {
                return stream;
            }

            Stream newStream;
            try
            {
                ValueTask<Stream> streamTask = Settings._plaintextStreamFilter(new SocketsHttpPlaintextStreamFilterContext(stream, httpVersion, request), cancellationToken);

                if (!async && !streamTask.IsCompleted)
                {
                    // User-provided PlaintextStreamFilter is completing asynchronously but the user is making a synchronous request; if the user cares, they should
                    // set it up so that synchronous requests are made on a handler with a synchronously-completing PlaintextStreamFilter supplied. If in the future,
                    // we could add a Boolean to SocketsHttpPlaintextStreamFilterContext (https://github.com/dotnet/runtime/issues/44876) to let the callback know whether
                    // this request is sync or async.
                    Trace($"{nameof(SocketsHttpHandler.PlaintextStreamFilter)} completing asynchronously for a synchronous request.");
                }

                newStream = await streamTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
            {
                stream.Dispose();
                throw;
            }
            catch (Exception e)
            {
                stream.Dispose();
                throw new HttpRequestException(SR.net_http_exception_during_plaintext_filter, e);
            }

            if (newStream == null)
            {
                stream.Dispose();
                throw new HttpRequestException(SR.net_http_null_from_plaintext_filter);
            }

            return newStream;
        }

        private async ValueTask<Stream> EstablishProxyTunnelAsync(bool async, CancellationToken cancellationToken)
        {
            // Send a CONNECT request to the proxy server to establish a tunnel.
            HttpRequestMessage tunnelRequest = new HttpRequestMessage(HttpMethod.Connect, _proxyUri);
            tunnelRequest.Headers.Host = $"{_originAuthority.IdnHost}:{_originAuthority.Port}";    // This specifies destination host/port to connect to

            if (_connectTunnelUserAgent is not null)
            {
                tunnelRequest.Headers.TryAddWithoutValidation(KnownHeaders.UserAgent.Descriptor, _connectTunnelUserAgent);
            }

            HttpResponseMessage tunnelResponse = await _poolManager.SendProxyConnectAsync(tunnelRequest, _proxyUri!, async, cancellationToken).ConfigureAwait(false);

            if (tunnelResponse.StatusCode != HttpStatusCode.OK)
            {
                tunnelResponse.Dispose();
                throw new HttpRequestException(HttpRequestError.ProxyTunnelError, SR.Format(SR.net_http_proxy_tunnel_returned_failure_status_code, _proxyUri, (int)tunnelResponse.StatusCode), statusCode: tunnelResponse.StatusCode);
            }

            try
            {
                return tunnelResponse.Content.ReadAsStream(cancellationToken);
            }
            catch
            {
                tunnelResponse.Dispose();
                throw;
            }
        }

        private async ValueTask<Stream> EstablishSocksTunnel(HttpRequestMessage request, bool async, CancellationToken cancellationToken)
        {
            Debug.Assert(_proxyUri != null);

            Stream stream = await ConnectToTcpHostAsync(_proxyUri.IdnHost, _proxyUri.Port, request, async, cancellationToken).ConfigureAwait(false);

            try
            {
                await SocksHelper.EstablishSocksTunnelAsync(stream, _originAuthority.IdnHost, _originAuthority.Port, _proxyUri, ProxyCredentials, async, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Debug.Assert(e is not HttpRequestException);
                throw new HttpRequestException(HttpRequestError.ProxyTunnelError, SR.net_http_proxy_tunnel_error, e);
            }

            return stream;
        }

        private CancellationTokenSource GetConnectTimeoutCancellationTokenSource<T>(HttpConnectionWaiter<T> waiter)
            where T : HttpConnectionBase?
        {
            var cts = new CancellationTokenSource(Settings._connectTimeout);

            lock (waiter)
            {
                // After a request completes (or is canceled), it will call into SetTimeoutToPendingConnectionAttempt,
                // which will no-op if ConnectionCancellationTokenSource is not set, assuming that the connection attempt is done.
                // As the initiating request for this connection attempt may complete concurrently at any time,
                // there is a race condition where the first call to SetTimeoutToPendingConnectionAttempt may happen
                // before we were able to set the CTS, so no timeout will be applied even though the request is already done.
                waiter.ConnectionCancellationTokenSource = cts;

                // To fix that, we check whether the waiter already completed now that we're holding a lock.
                // If it had, call SetTimeoutToPendingConnectionAttempt again now that the CTS is set.
                if (waiter.Task.IsCompleted)
                {
                    waiter.SetTimeoutToPendingConnectionAttempt(this, requestCancelled: waiter.Task.IsCanceled);
                    waiter.ConnectionCancellationTokenSource = null;
                }
            }

            return cts;
        }

        private static Exception CreateConnectTimeoutException(OperationCanceledException oce)
        {
            // The pattern for request timeouts (on HttpClient) is to throw an OCE with an inner exception of TimeoutException.
            // Do the same for ConnectTimeout-based timeouts.
            TimeoutException te = new TimeoutException(SR.net_http_connect_timedout, oce.InnerException);
            Exception newException = CancellationHelper.CreateOperationCanceledException(te, oce.CancellationToken);
            ExceptionDispatchInfo.SetCurrentStackTrace(newException);
            return newException;
        }

        [DoesNotReturn]
        private static void ThrowGetVersionException(HttpRequestMessage request, int desiredVersion, Exception? inner = null)
        {
            Debug.Assert(desiredVersion == 2 || desiredVersion == 3);

            HttpRequestException ex = new(HttpRequestError.VersionNegotiationError, SR.Format(SR.net_http_requested_version_cannot_establish, request.Version, request.VersionPolicy, desiredVersion), inner);
            if (request.IsExtendedConnectRequest && desiredVersion == 2)
            {
                ex.Data["HTTP2_ENABLED"] = false;
            }

            throw ex;
        }

        private bool CheckExpirationOnGet(HttpConnectionBase connection)
        {
            Debug.Assert(!HasSyncObjLock);

            TimeSpan pooledConnectionLifetime = _poolManager.Settings._pooledConnectionLifetime;
            if (pooledConnectionLifetime != Timeout.InfiniteTimeSpan)
            {
                return connection.GetLifetimeTicks(Environment.TickCount64) > pooledConnectionLifetime.TotalMilliseconds;
            }

            return false;
        }

        private bool CheckExpirationOnReturn(HttpConnectionBase connection)
        {
            TimeSpan lifetime = _poolManager.Settings._pooledConnectionLifetime;
            if (lifetime != Timeout.InfiniteTimeSpan)
            {
                return lifetime == TimeSpan.Zero || connection.GetLifetimeTicks(Environment.TickCount64) > lifetime.TotalMilliseconds;
            }

            return false;
        }

        /// <summary>
        /// Disposes the connection pool.  This is only needed when the pool currently contains
        /// or has associated connections.
        /// </summary>
        public void Dispose()
        {
            List<HttpConnectionBase>? toDispose = null;

            lock (SyncObj)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _http11RequestQueueIsEmptyAndNotDisposed = false;

                if (NetEventSource.Log.IsEnabled()) Trace("Disposing the pool.");

                if (_availableHttp2Connections is not null)
                {
                    toDispose = [.. _availableHttp2Connections];
                    _associatedHttp2ConnectionCount -= _availableHttp2Connections.Count;
                    _availableHttp2Connections.Clear();
                }

                if (IsHttp3Supported() && _availableHttp3Connections is not null)
                {
                    toDispose ??= new();
                    toDispose.AddRange(_availableHttp3Connections);
                    _associatedHttp3ConnectionCount -= _availableHttp3Connections.Count;
                    _availableHttp3Connections.Clear();
                }

                if (_authorityExpireTimer != null)
                {
                    _authorityExpireTimer.Dispose();
                    _authorityExpireTimer = null;
                }

                if (_altSvcBlocklistTimerCancellation != null)
                {
                    _altSvcBlocklistTimerCancellation.Cancel();
                    _altSvcBlocklistTimerCancellation.Dispose();
                    _altSvcBlocklistTimerCancellation = null;
                }

                Debug.Assert((_availableHttp2Connections?.Count ?? 0) == 0, $"Expected {nameof(_availableHttp2Connections)}.{nameof(_availableHttp2Connections.Count)} == 0");
            }

            // Dispose connections outside the lock to avoid lock re-entrancy issues.

            // This will trigger the disposal of Http11 connections.
            // Note: Http11 connections will decrement the _associatedHttp11ConnectionCount when disposed.
            // Http2 connections will not, hence the difference in handing _associatedHttp2ConnectionCount.
            ProcessHttp11RequestQueue(null);

            toDispose?.ForEach(c => c.Dispose());
        }

        /// <summary>
        /// Removes any unusable connections from the pool, and if the pool is then empty and stale, disposes of it.
        /// 从池中删除任何不可用的连接，如果池为空且过时，则将其丢弃。
        /// </summary>
        /// <returns>
        /// true if the pool disposes of itself; otherwise, false.
        /// </returns>
        public bool CleanCacheAndDisposeIfUnused()
        {
            TimeSpan pooledConnectionLifetime = _poolManager.Settings._pooledConnectionLifetime;
            TimeSpan pooledConnectionIdleTimeout = _poolManager.Settings._pooledConnectionIdleTimeout;
            long nowTicks = Environment.TickCount64;

            List<HttpConnectionBase>? toDispose = null;

            lock (SyncObj)
            {
                // If there are now no connections associated with this pool, we can dispose of it. We
                // avoid aggressively cleaning up pools that have recently been used but currently aren't;
                // if a pool was used since the last time we cleaned up, give it another chance. New pools
                // start out saying they've recently been used, to give them a bit of breathing room and time
                // for the initial collection to be added to it.
                if (!_usedSinceLastCleanup && _associatedHttp11ConnectionCount == 0 && _associatedHttp2ConnectionCount == 0)
                {
                    _disposed = true;
                    return true; // Pool is disposed of.  It should be removed.
                }

                // Reset the cleanup flag.  Any pools that are empty and not used since the last cleanup
                // will be purged next time around.
                _usedSinceLastCleanup = false;

                ScavengeHttp11ConnectionStack(this, _http11Connections, ref toDispose, nowTicks, pooledConnectionLifetime, pooledConnectionIdleTimeout);

                if (_availableHttp2Connections is not null)
                {
                    int removed = ScavengeHttp2ConnectionList(_availableHttp2Connections, ref toDispose, nowTicks, pooledConnectionLifetime, pooledConnectionIdleTimeout);
                    _associatedHttp2ConnectionCount -= removed;

                    // Note: Http11 connections will decrement the _associatedHttp11ConnectionCount when disposed.
                    // Http2 connections will not, hence the difference in handing _associatedHttp2ConnectionCount.
                }
                if (IsHttp3Supported() && _availableHttp3Connections is not null)
                {
                    int removed = ScavengeHttp3ConnectionList(_availableHttp3Connections, ref toDispose, nowTicks, pooledConnectionLifetime, pooledConnectionIdleTimeout);
                    _associatedHttp3ConnectionCount -= removed;

                    // Note: Http11 connections will decrement the _associatedHttp11ConnectionCount when disposed.
                    // Http3 connections will not, hence the difference in handing _associatedHttp3ConnectionCount.
                }
            }

            // Dispose the stale connections outside the pool lock, to avoid holding the lock too long.
            // Dispose them asynchronously to not to block the caller on closing the SslStream or NetworkStream.
            if (toDispose is not null)
            {
                Task.Factory.StartNew(static s => ((List<HttpConnectionBase>)s!).ForEach(c => c.Dispose()), toDispose,
                    CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
            }

            // Pool is active.  Should not be removed.
            return false;
        }

        // For diagnostic purposes
        public override string ToString() =>
            $"{nameof(HttpConnectionPool)} " +
            (_proxyUri == null ?
                (_sslOptionsHttp11 == null ?
                    $"http://{_originAuthority}" :
                    $"https://{_originAuthority}" + (_sslOptionsHttp11.TargetHost != _originAuthority.IdnHost ? $", SSL TargetHost={_sslOptionsHttp11.TargetHost}" : null)) :
                (_sslOptionsHttp11 == null ?
                    $"Proxy {_proxyUri}" :
                    $"https://{_originAuthority}/ tunnelled via Proxy {_proxyUri}" + (_sslOptionsHttp11.TargetHost != _originAuthority.IdnHost ? $", SSL TargetHost={_sslOptionsHttp11.TargetHost}" : null)));

        public void Trace(string? message, [CallerMemberName] string? memberName = null) =>
            NetEventSource.Log.HandlerMessage(
                GetHashCode(),               // pool ID
                0,                           // connection ID
                0,                           // request ID
                memberName,                  // method name
                message);                    // message
    }
}
