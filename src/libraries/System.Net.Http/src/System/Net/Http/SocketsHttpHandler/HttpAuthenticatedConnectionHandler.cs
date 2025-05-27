// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http
{
    /// <summary>
    /// 认证连接处理器
    /// 处理 HTTP 身份认证（如 NTLM、Kerberos、OAuth），管理认证令牌和连接复用
    /// 通常位于管道中后段，靠近实际网络 I/O 的 HttpConnectionPool
    /// </summary>
    internal sealed class HttpAuthenticatedConnectionHandler : HttpMessageHandlerStage
    {
        private readonly HttpConnectionPoolManager _poolManager;

        public HttpAuthenticatedConnectionHandler(HttpConnectionPoolManager poolManager)
        {
            _poolManager = poolManager;
        }

        internal override ValueTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool async, CancellationToken cancellationToken)
        {
            return _poolManager.SendAsync(request, async, doRequestAuth: true, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _poolManager.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
