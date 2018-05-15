using Common.Log;
using JetBrains.Annotations;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Enumerators;
using Lykke.AlgoStore.CSharp.AlgoTemplate.Models.Repositories;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Requests;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Listening.Responses;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Services.Listening;
using Lykke.AlgoStore.MatchingEngineAdapter.Core.Services.Listening;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lykke.AlgoStore.MatchingEngineAdapter.Services.Listening
{
    /// <summary>
    /// Listens for incoming requests on a <see cref="IStreamWrapper"/> and
    /// handles events like network failures and invalid requests
    /// </summary>
    public class ConnectionWorker : IDisposable
    {
        private readonly IStreamWrapper _connection;
        private readonly IMessageHandler _messageHandler;
        private readonly IAlgoClientInstanceRepository _algoClientInstanceRepository;

        private readonly ILog _log;

        private readonly TaskCompletionSource<IMessageInfo> _abortReadSource = new TaskCompletionSource<IMessageInfo>();

        private readonly Func<string, Task<bool>> _authenticationCallback;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of <see cref="ConnectionWorker"/>
        /// </summary>
        /// <param name="algoClientInstanceRepository">The <see cref="IAlgoClientInstanceRepository"/> to use for validating connections</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="requestQueue"/>, <paramref name="algoClientInstanceRepository"/>
        /// or <paramref name="log"/> are null
        /// </exception>
        public ConnectionWorker(
            IStreamWrapper connection,
            IMessageHandler messageHandler,
            IAlgoClientInstanceRepository algoClientInstanceRepository, 
            [NotNull] ILog log,
            Func<string, Task<bool>> authenticationCallback)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            _algoClientInstanceRepository = algoClientInstanceRepository ?? throw new ArgumentNullException(nameof(algoClientInstanceRepository));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _authenticationCallback = authenticationCallback ?? throw new ArgumentNullException(nameof(authenticationCallback));
        }

        /// <summary>
        /// Disposes this <see cref="ConnectionWorker"/> and closes all of the connections it handles
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed) return;

            if(isDisposing)
            {
                _connection.Dispose();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Worker thread which handles incoming messages
        /// </summary>
        public async Task AcceptMessagesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => _abortReadSource.SetResult(null));

            try
            {
                while (true)
                {
                    var completedTask = await Task.WhenAny(_abortReadSource.Task, _connection.ReadMessageAsync());

                    var message = completedTask.Result;

                    // Only happens when the read is cancelled through the task cancellation source
                    if (message == null)
                        break;

                    var isAuthenticated = _connection.IsAuthenticated;

                    if (!await TryAuthenticate(_connection, message))
                    {
                        await message.ReplyAsync((byte)MeaResponseType.Pong, new PingRequest { Message = "Fail" });
                        break;
                    }

                    await _messageHandler.HandleMessage(message);
                }
            }
            catch(AggregateException e)
            {
                e.Flatten().Handle((ex) =>
                {
                    return HandleConnectionFailure(ex);
                });
            }
            catch (Exception e)
            {
                if (!HandleConnectionFailure(e))
                    throw;
            }

            Dispose();
        }

        private bool HandleConnectionFailure(Exception ex)
        {
            switch(ex)
            {
                case System.IO.IOException ioe:
                    _log.WriteInfo(nameof(ConnectionWorker), nameof(AcceptMessagesAsync), $"Connection {_connection} was lost");
                    return true;

                case ObjectDisposedException ode:
                    _log.WriteInfo(nameof(ConnectionWorker), nameof(AcceptMessagesAsync), $"Connection {_connection} was dropped");
                    return true;

                case System.IO.InvalidDataException ide:
                    _log.WriteWarning(nameof(ConnectionWorker), nameof(AcceptMessagesAsync),
                        $"Client {_connection} sent invalid data, dropping connection!");
                    return true;

                default:
                    return false;
            }
        }

        private async Task<bool> TryAuthenticate(IStreamWrapper connection, IMessageInfo messageInfo)
        {
            if (!connection.AuthenticationEnabled || connection.IsAuthenticated) return true;

            var pingRequest = messageInfo.Message as PingRequest;

            if (pingRequest == null)
            {
                await _log.WriteWarningAsync(nameof(ConnectionWorker), nameof(TryAuthenticate),
                    $"Connection {connection} didn't send {nameof(PingRequest)} as the first message, dropping connection!");
                return false;
            }

            if(string.IsNullOrEmpty(pingRequest.Message))
            {
                await _log.WriteWarningAsync(nameof(ConnectionWorker), nameof(TryAuthenticate),
                    $"Connection {connection} sent empty {nameof(PingRequest)}, dropping connection!");
                return false;
            }

            var splitString = pingRequest.Message.Split('_', StringSplitOptions.RemoveEmptyEntries);

            if (splitString.Length != 2)
            {
                await _log.WriteWarningAsync(nameof(ConnectionWorker), nameof(TryAuthenticate),
                    $"Connection {connection} sent invalid format {nameof(PingRequest)}, dropping connection!");
                return false;
            }

            if (!_algoClientInstanceRepository.ExistsAlgoInstanceDataWithClientIdAsync(splitString[0], splitString[1]).Result)
            {
                await _log.WriteWarningAsync(nameof(ConnectionWorker), nameof(TryAuthenticate),
                    $"Connection {connection} sent {nameof(PingRequest)} containing unknown client ID/instance ID combination, dropping connection!");
                return false;
            }

            if((await _algoClientInstanceRepository.GetAlgoInstanceDataByClientIdAsync(splitString[0], splitString[1]))
                .AlgoInstanceStatus != AlgoInstanceStatus.Started)
            {
                await _log.WriteWarningAsync(nameof(ConnectionWorker), nameof(TryAuthenticate),
                    $"Connection {connection} sent {nameof(PingRequest)} containing algo instance with status different from started, dropping connection!");
                return false;
            }

            if (!await _authenticationCallback(pingRequest.Message))
                return false;

            connection.ID = pingRequest.Message;
            connection.MarkAuthenticated();

            await _log.WriteInfoAsync(nameof(ConnectionWorker), nameof(TryAuthenticate),
                $"Connection {connection} has successfully authenticated");

            await messageInfo.ReplyAsync((byte)MeaResponseType.Pong, new PingRequest { Message = "Success" });
            return true;
        }
    }
}
