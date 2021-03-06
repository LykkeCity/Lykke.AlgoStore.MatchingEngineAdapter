﻿using Common.Log;
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
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain;
using Lykke.AlgoStore.MatchingEngineAdapter.Abstractions.Domain.Contracts;
using Lykke.Common.Log;

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

        private readonly Func<IStreamWrapper, string, Task<bool>> _authenticationCallback;

        private CancellationTokenRegistration _ctr;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of <see cref="ConnectionWorker"/>
        /// </summary>
        /// <param name="connection">The connection this worker will be working on</param>
        /// <param name="messageHandler">The message handler which will take care of handling incoming messages</param>
        /// <param name="algoClientInstanceRepository">The <see cref="IAlgoClientInstanceRepository"/> to use for validating connections</param>
        /// <param name="log">The logger to use for logging</param>
        /// <param name="authenticationCallback">Callback to do extra authentication checks based on the sent ID</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="connection"/>, <paramref name="messageHandler"/>,
        /// <paramref name="algoClientInstanceRepository"/>, <paramref name="log"/>
        /// or <paramref name="authenticationCallback"/> are null
        /// </exception>
        public ConnectionWorker(
            IStreamWrapper connection,
            IMessageHandler messageHandler,
            IAlgoClientInstanceRepository algoClientInstanceRepository, 
            [NotNull] ILog log,
            Func<IStreamWrapper, string, Task<bool>> authenticationCallback)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            _algoClientInstanceRepository = algoClientInstanceRepository ?? throw new ArgumentNullException(nameof(algoClientInstanceRepository));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _authenticationCallback = authenticationCallback ?? throw new ArgumentNullException(nameof(authenticationCallback));
        }

        ~ConnectionWorker()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes this <see cref="ConnectionWorker"/> and closes all of the connections it handles
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed) return;

            if(isDisposing)
            {
                _connection.Dispose();
            }

            _ctr.Dispose();

            _isDisposed = true;
        }

        /// <summary>
        /// Worker thread which handles incoming messages
        /// </summary>
        public async Task AcceptMessagesAsync(CancellationToken cancellationToken)
        {
            _ctr = cancellationToken.Register(() => _abortReadSource.SetResult(null));

            try
            {
                while (true)
                {
                    var completedTask = await Task.WhenAny(_abortReadSource.Task, _connection.ReadMessageAsync());

                    var message = completedTask.Result;

                    // Only happens when the read is cancelled through the task cancellation source
                    if (message == null)
                        break;

                    if (!await TryAuthenticate(_connection, message))
                    {
                        await message.ReplyAsync((byte)MeaResponseType.Pong, new PingRequest { Message = "Fail" });
                        break;
                    }

                    try
                    {
                        await _messageHandler.HandleMessage(message);
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, $"Exception while processing {message.Message.GetType().Name}", message);
                        await HandleMEAException(message, ex);
                        throw;
                    }
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

        private async Task HandleMEAException(IMessageInfo request, Exception e)
        {
            var requestType = request.Message.GetType();
            if (requestType == typeof(LimitOrderRequest))
                await request.ReplyAsync(MeaResponseType.LimitOrderResponse, ResponseModel<LimitOrderResponseModel>.CreateFail(ErrorCodeType.Runtime, e.Message));
            if (requestType == typeof(MarketOrderRequest))
                await request.ReplyAsync(MeaResponseType.MarketOrderResponse, ResponseModel<double>.CreateFail(ErrorCodeType.Runtime, e.Message));
            if (requestType == typeof(CancelLimitOrderRequest))
                await request.ReplyAsync(MeaResponseType.CancelLimitOrderResponse, ResponseModel.CreateFail(ErrorCodeType.Runtime, e.Message));
        }

        private bool HandleConnectionFailure(Exception ex)
        {
            switch (ex)
            {
                case System.IO.IOException ioe:
                    if (_connection.IsAuthenticated)
                        _log.WriteInfo(nameof(ConnectionWorker), nameof(AcceptMessagesAsync), $"Connection {_connection} was lost");
                    return true;

                case ObjectDisposedException ode:
                    if (_connection.IsAuthenticated)
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

            if (!await _algoClientInstanceRepository.ExistsAlgoInstanceDataWithAuthTokenAsync(pingRequest.Message))
            {
                await _log.WriteWarningAsync(nameof(ConnectionWorker), nameof(TryAuthenticate),
                    $"Connection {connection} sent {nameof(PingRequest)} containing unknown auth token, dropping connection!");
                return false;
            }

            if((await _algoClientInstanceRepository.GetAlgoInstanceDataByAuthTokenAsync(pingRequest.Message))
                .AlgoInstanceStatus != AlgoInstanceStatus.Started)
            {
                await _log.WriteWarningAsync(nameof(ConnectionWorker), nameof(TryAuthenticate),
                    $"Connection {connection} sent {nameof(PingRequest)} containing algo instance with status different from started, dropping connection!");
                return false;
            }

            if (!await _authenticationCallback(connection, pingRequest.Message))
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
