using CallTraking.NEventSocket.Common.FreeSWITCH;
using CallTraking.NEventSocket.Common.Utils.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace CallTraking.NEventSocket.Common.Sockets
{
    /// <summary>
    /// Wraps an EventSocket connecting inbound to FreeSwitch
    /// </summary>
    public class InboundSocket : EventSocket
    {
        private readonly ILogger<InboundSocket> _logger;

        public InboundSocket(ILogger<InboundSocket> logger, string host, int port, string password, TimeSpan? timeout = null) : base(new TcpClient(host, port), timeout, logger)
        {
            _logger = logger;
            InitialiseAsync(host, port, password, timeout);
        }

        /// <summary>
        /// Connects to FreeSwitch and authenticates
        /// </summary>
        /// <param name="host">(Default: localhost) The hostname or ip to connect to.</param>
        /// <param name="port">(Default: 8021) The Tcp port to connect to.</param>
        /// <param name="password">(Default: ClueCon) The password to authenticate with.</param>
        /// <param name="timeout">(Optional) The auth request timeout.</param>
        /// <returns>A task of <see cref="InboundSocket"/>.</returns>
        /// <exception cref="InboundSocketConnectionFailedException"></exception>
        private async void InitialiseAsync(
            string host = "localhost", int port = 8021, string password = "ClueCon", TimeSpan? timeout = null)
        {
            try
            {
                var result = await this.Auth(password).ConfigureAwait(false);

                if (!result.Success)
                {
                    _logger?.LogError($"InboundSocket authentication failed ({result.ErrorMessage}).");
                    throw new InboundSocketConnectionFailedException($"Invalid password when trying to connect to {host}:{port}");
                }

                _logger?.LogTrace("InboundSocket authentication succeeded.");
            }
            catch (SocketException ex)
            {
                throw new InboundSocketConnectionFailedException($"Socket error when trying to connect to {host}:{port}", ex);
            }
            catch (IOException ex)
            {
                throw new InboundSocketConnectionFailedException($"IO error when trying to connect to {host}:{port}", ex);
            }
            catch (TimeoutException ex)
            {
                throw new InboundSocketConnectionFailedException($"Timeout when trying to connect to {host}:{port}.{ex.Message}", ex);
            }
        }
    }
}
