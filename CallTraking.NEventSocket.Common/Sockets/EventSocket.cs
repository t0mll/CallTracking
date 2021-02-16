using CallTraking.NEventSocket.Common.FreeSWITCH;
using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Bridge;
using CallTraking.NEventSocket.Common.FreeSWITCH.Events;
using CallTraking.NEventSocket.Common.FreeSWITCH.Headers;
using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;
using CallTraking.NEventSocket.Common.Utils;
using CallTraking.NEventSocket.Common.Utils.Extensions;
using CallTraking.NEventSocket.Common.Utils.ObjectPooling;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CallTraking.NEventSocket.Common.Sockets
{
    public abstract class EventSocket : ObservableSocket, IEventSocket
    {
        private readonly ILogger _logger;
        // minimum events required for this class to do its job
        private readonly HashSet<EventNames> _subscribedEvents = new HashSet<EventNames>();
        private readonly HashSet<string> _customEvents = new HashSet<string>();
        private readonly CancellationTokenSource _cancelationTokenSource = new CancellationTokenSource();
        private readonly Utils.AsyncLock _gate = new Utils.AsyncLock();
        private readonly IObservable<BasicMessage> _messages;
        private readonly InterlockedBoolean _disposed = new InterlockedBoolean();


        /// <summary>
        /// Instantiates an <see cref="EventSocket"/> instance wrapping the provided <seealso cref="TcpClient"/>
        /// </summary>
        /// <param name="tcpClient">A TcpClient.</param>
        /// <param name="responseTimeOut">(Optional) The response timeout.</param>
        protected EventSocket(TcpClient tcpClient, TimeSpan? responseTimeOut = null, ILogger<EventSocket> logger = null) : base(tcpClient, logger)
        {
            _logger = logger;

            _messages =
                Receiver.SelectMany(x => Encoding.UTF8.GetString(x))
                    .AggregateUntil(() => new Parser(), (builder, ch) => builder.Append(ch), builder => builder.Completed)
                    .Select(builder => builder.ExtractMessage())
                    .Do(
                        x => _logger?.LogTrace($"Messages Received [{x.ContentType}]."),
                        ex => { },
                        () =>
                        {
                            _logger?.LogDebug("Messages Observable completed.");
                            Dispose();
                        })
                    .ObserveOn(Scheduler.Immediate)
                    .Publish()
                    .RefCount();


            _logger?.LogTrace("EventSocket initialized");
        }

        public long EventSocketId
        {
            get { return Id; }
        }

        /// <summary>
        /// Gets an observable sequence of <seealso cref="BasicMessage"/>.
        /// </summary>
        public IObservable<BasicMessage> Messages
        {
            get
            {
                return _messages.AsObservable();
            }
        }

        /// <summary>
        /// Gets an observable sequence of <seealso cref="EventMessage"/>.
        /// </summary>
        public IObservable<EventMessage> Events
        {
            get
            {
                return Messages
                                .Where(x => x.ContentType == ContentTypes.EventPlain)
                                .Select(x => new EventMessage(x));
            }
        }

        /// <summary>
        /// Gets an observable sequence of <seealso cref="ChannelEvent"/>.
        /// </summary>
        public IObservable<ChannelEvent> ChannelEvents
        {
            get
            {
                return Events.Where(x => x.Headers.ContainsKey(HeaderNames.UniqueId))
                             .Select(x => new ChannelEvent(x));
            }
        }

        /// <summary>
        /// Gets an observable sequence of <seealso cref="ConferenceEvent"/>.
        /// </summary>
        public IObservable<ConferenceEvent> ConferenceEvents
        {
            get
            {
                return
                    Events.Where(x => x.EventName == EventNames.Custom && x.Headers[HeaderNames.EventSubclass] == CustomEvents.Conference.Maintainence)
                          .Select(x => new ConferenceEvent(x));
            }
        }

        /// <summary>
        /// Send an api command (blocking mode)
        /// </summary>
        /// <remarks>
        /// See https://freeswitch.org/confluence/display/FREESWITCH/mod_event_socket#mod_event_socket-api
        /// </remarks>
        /// <param name="command">The API command to send (see https://wiki.freeswitch.org/wiki/Mod_commands) </param>
        /// <returns>A Task of <seealso cref="ApiResponse"/>.</returns>
        public async Task<ApiResponse> SendApi(string command)
        {

            using (var asyncLock = await _gate.LockAsync().ConfigureAwait(false))
            {
                _logger?.LogTrace($"Sending [api {command}]");
                var tcs = new TaskCompletionSource<ApiResponse>();
                var subscriptions = new CompositeDisposable { _cancelationTokenSource.Token.Register(() => tcs.TrySetCanceled()) };

                subscriptions.Add(
                    Messages.Where(x => x.ContentType == ContentTypes.ApiResponse)
                            .Take(1)
                            .Select(x => new ApiResponse(x))
                            .Do(
                                m =>
                                {
                                    var logLevel = m.Success ? LogLevel.Debug : LogLevel.Error;

                                    if (m.Success && command.StartsWith("uuid_dump"))
                                    {
                                        //we don't need to dump the entire response to the logs
                                        _logger?.LogDebug($"ApiResponse received CHANNEL_DATA for [{command}]");
                                    }
                                    else
                                    {
                                        _logger?.LogError($"ApiResponse received [{m.BodyText.Replace("\n", string.Empty)}] for [{command}]");
                                    }
                                },
                                ex => _logger?.LogError(ex, $"Error waiting for Api Response to [{command}]."))
                                .Subscribe(x => tcs.TrySetResult(x), ex => tcs.TrySetException(ex), subscriptions.Dispose));

                await
                    SendAsync(Encoding.ASCII.GetBytes("api " + command + "\n\n"), _cancelationTokenSource.Token)
                        .ContinueOnFaultedOrCancelled(tcs, subscriptions.Dispose)
                        .ConfigureAwait(false);

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Send an event socket command
        /// </summary>
        /// <remarks>
        /// See https://freeswitch.org/confluence/display/FREESWITCH/mod_event_socket#mod_event_socket-api
        /// </remarks>
        /// <param name="command">The command to send.</param>
        /// <returns>A Task of <seealso cref="CommandReply"/>.</returns>
        public async Task<CommandReply> SendCommand(string command)
        {
            using (var asyncLock = await _gate.LockAsync().ConfigureAwait(false))
            {
                _logger?.LogTrace($"Sending [{command}]");
                var tcs = new TaskCompletionSource<CommandReply>();
                var subscriptions = new CompositeDisposable { _cancelationTokenSource.Token.Register(() => tcs.TrySetCanceled()) };

                subscriptions.Add(
                    Messages.Where(x => x.ContentType == ContentTypes.CommandReply)
                            .Take(1)
                            .Select(x => new CommandReply(x))
                            .Do(
                                result =>
                                {
                                    var logLevel = result.Success ? LogLevel.Debug : LogLevel.Error;

                                    if (result.Success)
                                    {
                                        _logger?.LogDebug($"CommandReply received [{ result.ReplyText.Replace("\n", string.Empty)}] for [{command}]");
                                    }
                                    else
                                    {
                                        _logger?.LogError($"CommandReply received [{ result.ReplyText.Replace("\n", string.Empty)}] for [{command}]");
                                    }
                                },
                                ex => _logger?.LogError(ex, $"Error waiting for Command Reply to [{command}]."))
                            .Subscribe(x => tcs.TrySetResult(x), ex => tcs.TrySetException(ex), subscriptions.Dispose));

                await
                    SendAsync(Encoding.ASCII.GetBytes(command + "\n\n"), _cancelationTokenSource.Token)
                        .ContinueOnFaultedOrCancelled(tcs, subscriptions.Dispose)
                        .ConfigureAwait(false);

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously executes a dialplan application on the given channel.
        /// </summary>
        /// <remarks>
        /// See https://freeswitch.org/confluence/display/FREESWITCH/mod_dptools
        /// </remarks>
        /// <param name="uuid">The channel UUID.</param>
        /// <param name="application">The dialplan application to execute.</param>
        /// <param name="applicationArguments">(Optional) arguments to pass to the application.</param>
        /// <param name="eventLock">(Default: false) Whether to block the socket until the application completes before processing further.
        ///  (see https://wiki.freeswitch.org/wiki/Event_Socket_Outbound#Q:_Ordering_and_async_keyword )</param>
        /// <param name="async">(Default: false) Whether to return control from the application immediately. 
        /// (see https://wiki.freeswitch.org/wiki/Event_Socket_Outbound#Q:_Should_I_use_sync_mode_or_async_mode.3F)
        /// </param>
        /// <param name="loops">(Optional) How many times to repeat the application.</param>
        /// <returns>
        /// A Task of <seealso cref="EventMessage"/> that wraps the ChannelExecuteComplete event if the application completes successfully.
        /// The Task result will be null if the application did not execute, for example, the socket disconnected or the channel was hung up.
        /// </returns>
        public Task<ChannelEvent> ExecuteApplication(
            string uuid, string application, string applicationArguments = null, bool eventLock = false, bool async = false, int loops = 1)
        {
            if (uuid == null)
            {
                throw new ArgumentNullException("uuid");
            }

            if (application == null)
            {
                throw new ArgumentNullException("application");
            }

            //lists.freeswitch.org/pipermail/freeswitch-users/2013-May/095329.html
            var applicationUUID = Guid.NewGuid().ToString();

            var sb = StringBuilderPool.Allocate();
            sb.AppendFormat("sendmsg {0}\nEvent-UUID: {1}\ncall-command: execute\nexecute-app-name: {2}\n", uuid, applicationUUID, application);

            if (eventLock)
            {
                sb.Append("event-lock: true\n");
            }

            if (loops > 1)
            {
                sb.Append("loops: " + loops + "\n");
            }

            if (async)
            {
                sb.Append("async: true\n");
            }

            if (applicationArguments != null)
            {
                sb.AppendFormat("content-type: text/plain\ncontent-length: {0}\n\n{1}\n", applicationArguments.Length, applicationArguments);
            }

            var tcs = new TaskCompletionSource<ChannelEvent>();
            var subscriptions = new CompositeDisposable();

            if (_cancelationTokenSource.Token.CanBeCanceled)
            {
                subscriptions.Add(_cancelationTokenSource.Token.Register(() => tcs.TrySetCanceled()));
            }

            subscriptions.Add(
                ChannelEvents.Where(
                    x => x.EventName == EventNames.ChannelExecuteComplete && x.Headers["Application-UUID"] == applicationUUID)
                    .Take(1)
                    .Subscribe(
                        executeCompleteEvent =>
                        {
                            if (executeCompleteEvent != null)
                            {
                                _logger?.LogDebug(
                                    $"{executeCompleteEvent.UUID} " +
                                    $"ChannelExecuteComplete [{executeCompleteEvent.AnswerState} " +
                                    $"{executeCompleteEvent.Headers[HeaderNames.Application]} " +
                                    $"{executeCompleteEvent.Headers[HeaderNames.ApplicationResponse]}]");
                            }
                            else
                            {
                                _logger?.LogTrace($"No ChannelExecuteComplete event received for {application}");
                            }

                            tcs.TrySetResult(executeCompleteEvent);
                        },
                            ex => tcs.TrySetException(ex),
                            subscriptions.Dispose));

            SubscribeEvents(EventNames.ChannelExecuteComplete).ContinueWith(t =>
            {
                if (t.IsCompleted)
                {
                    SendCommand(StringBuilderPool.ReturnAndFree(sb))
                        .Then(reply =>
                        {
                            if (!reply.Success)
                            {
                                tcs.TrySetResult(null);
                            }
                        })
                        .ContinueOnFaultedOrCancelled(tcs, subscriptions.Dispose);
                }
                else
                {
                    tcs.TrySetException(t.Exception);
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// Send an api command (non-blocking mode) this will let you execute a job in the background and the result will be sent as an event with an indicated uuid to match the reply to the command)
        /// </summary>
        /// <remarks>
        /// See https://freeswitch.org/confluence/display/FREESWITCH/mod_event_socket#mod_event_socket-bgapi
        /// </remarks>
        /// <param name="command">The command to execute.</param>
        /// <param name="arg">(Optional) command argument.</param>
        /// <param name="jobUUID">(Optional) job unique identifier.</param>
        /// <returns>A Task of <seealso cref="BackgroundJobResult"/>.</returns>
        public Task<BackgroundJobResult> BackgroundJob(string command, string arg = null, Guid? jobUUID = null)
        {
            if (jobUUID == null)
            {
                jobUUID = Guid.NewGuid();
            }

            var backgroundApiCommand = arg != null
                                           ? $"bgapi {command} {arg}\nJob-UUID: {jobUUID}"
                                           : $"bgapi {command}\nJob-UUID: {jobUUID}";

            var tcs = new TaskCompletionSource<BackgroundJobResult>();
            var subscriptions = new CompositeDisposable();

            if (_cancelationTokenSource.Token.CanBeCanceled)
            {
                subscriptions.Add(
                    _cancelationTokenSource.Token.Register(() => tcs.TrySetCanceled()));
            }

            subscriptions.Add(
                Events.Where(x => x.EventName == EventNames.BackgroundJob && x.Headers[HeaderNames.JobUUID] == jobUUID.ToString())
                        .Take(1)
                        .Select(x => new BackgroundJobResult(x))
                        .Do(result => _logger?.LogDebug($"BackgroundJobResult received [{result.BodyText.Replace("\n", string.Empty)}] for [{command}]"),
                            ex => _logger?.LogError(ex, $"Error waiting for BackgroundJobResult Reply to [{command}]."))
                        .Subscribe(x => tcs.TrySetResult(x), ex => tcs.TrySetException(ex), subscriptions.Dispose));

            SubscribeEvents(EventNames.BackgroundJob).ContinueWith(t =>
            {
                if (t.IsCompleted)
                {
                    SendCommand(backgroundApiCommand).ContinueOnFaultedOrCancelled(tcs, subscriptions.Dispose);
                }
                else
                {
                    tcs.TrySetException(t.Exception);
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// Bridge a new channel to the existing one. Generally used to route an incoming call to one or more endpoints.
        /// </summary>
        /// <remarks>
        /// See https://freeswitch.org/confluence/display/FREESWITCH/bridge
        /// </remarks>
        /// <param name="uuid">The UUID of the channel to bridge (the A-Leg).</param>
        /// <param name="endpoint">The destination to dial.</param>
        /// <param name="options">(Optional) Any <seealso cref="BridgeOptions"/> to configure the bridge.</param>
        /// <returns>A Task of <seealso cref="BridgeResult"/>.</returns>
        public async Task<BridgeResult> Bridge(string uuid, string endpoint, BridgeOptions options = null)
        {
            if (options == null)
            {
                options = new BridgeOptions();
            }

            if (string.IsNullOrEmpty(options.UUID))
            {
                options.UUID = Guid.NewGuid().ToString();
            }

            var bridgeString = string.Format("{0}{1}", options, endpoint);

            // some bridge options need to be set in channel vars
            if (options.ChannelVariables.Any())
            {
                await
                    this.SetMultipleChannelVariables(
                        uuid, options.ChannelVariables.Select(kvp => kvp.Key + "='" + kvp.Value + "'").ToArray()).ConfigureAwait(false);
            }

            /* If the bridge fails to connect we'll get a CHANNEL_EXECUTE_COMPLETE event with a failure message and the Execute task will complete.
             * If the bridge succeeds, that event won't arrive until after the bridged leg hangs up and completes the call.
             * In this case, we want to return a result as soon as the b-leg picks up and connects so we'll merge with the CHANNEL_BRIDGE event
             * observable.Amb(otherObservable) will propogate the first sequence to produce a result. */

            await SubscribeEvents(EventNames.ChannelBridge, EventNames.ChannelHangup).ConfigureAwait(false);

            var bridgedOrHungupEvent =
                ChannelEvents.FirstOrDefaultAsync(x => x.UUID == uuid && (x.EventName == EventNames.ChannelBridge || x.EventName == EventNames.ChannelHangup))
                    .Do(
                        e =>
                        {
                            if (e != null)
                            {
                                switch (e.EventName)
                                {
                                    case EventNames.ChannelBridge:
                                        _logger?.LogDebug($"Bridge [{uuid} - {options.UUID}] complete - {e.Headers[HeaderNames.OtherLegUniqueId]}");
                                        break;
                                    case EventNames.ChannelHangup:
                                        _logger?.LogDebug($"Bridge [{uuid} - {options.UUID}]  aborted, channel hangup [{e.Headers[HeaderNames.HangupCause]}]");
                                        break;
                                }
                            }
                        });

            return
                await
                ExecuteApplication(uuid, "bridge", bridgeString)
                    .ToObservable()
                    .Amb(bridgedOrHungupEvent)
                    .Select(x => new BridgeResult(x))
                    .ToTask()
                    .ConfigureAwait(false);
        }

        /// <summary>
        /// Requests FreeSwitch shuts down the socket
        /// </summary>
        public Task Exit()
        {
            // we're not using the CancellationToken here because we want to wait until the reply comes back
            var command = "exit";

            _logger?.LogTrace($"Sending [{command}]");

            lock (_gate)
            {
                var tcs = new TaskCompletionSource<BasicMessage>();
                var subscriptions = new CompositeDisposable();

                subscriptions.Add(
                    Messages.Where(x => x.ContentType == ContentTypes.CommandReply)
                            .Take(1)
                            .Select(x => new CommandReply(x))
                            .Subscribe(
                                result =>
                                _logger?.LogDebug($"CommandReply received [{result.ReplyText.Replace("\n", string.Empty)}] for [{command}]"),
                                ex =>
                                {
                                    _logger?.LogError(ex, $"Error waiting for Command Reply to [{command}].");
                                    tcs.TrySetException(ex);
                                }));

                subscriptions.Add(
                    Messages.Where(x => x.ContentType == ContentTypes.DisconnectNotice)
                            .Take(1)
                            .Timeout(
                                TimeSpan.FromSeconds(2),
                                Observable.Throw<BasicMessage>(new TimeoutException("No Disconnect Notice received.")))
                            .Subscribe(
                                x =>
                                {
                                    _logger?.LogDebug($"Disconnect Notice received [{x.BodyText}]");
                                    tcs.TrySetResult(x);
                                },
                                ex =>
                                {
                                    _logger?.LogError(ex, "Error waiting for Disconnect Notice");
                                    if (ex is TimeoutException)
                                    {
                                        tcs.TrySetResult(null);
                                    }
                                    else
                                    {
                                        tcs.TrySetException(ex);
                                    }
                                },
                                () =>
                                {
                                    subscriptions.Dispose();
                                    tcs.TrySetResult(null);
                                }));

                SendAsync(Encoding.ASCII.GetBytes(command + "\n\n"), CancellationToken.None)
                    .ContinueOnFaultedOrCancelled(tcs, subscriptions.Dispose);

                return tcs.Task;
            }
        }

        /// <summary>
        /// Subscribes this EventSocket to one or more events.
        /// </summary>
        /// <param name="events">The <seealso cref="EventNames"/>s to subscribe to.</param>
        /// <returns>A Task.</returns>
        /// <remarks>This is additive - additional calls to .SubscribeEvents will add more event subscriptions.</remarks>
        public Task SubscribeEvents(params EventNames[] events)
        {
            if (!events.All(@event => _subscribedEvents.Contains(@event)))
            {
                _subscribedEvents.UnionWith(events);
                return EnsureEventsSubscribed();
            }

            return TaskHelper.Completed;
        }

        /// <summary>
        /// Subscribes this EventSocket to one or more custom events.
        /// </summary>
        /// <param name="events">The custom event names to subscribe to.</param>
        /// <returns>A Task.</returns>
        public Task SubscribeCustomEvents(params string[] events)
        {
            if (!events.All(@event => _customEvents.Contains(@event)))
            {
                _customEvents.UnionWith(events);
                return EnsureEventsSubscribed();
            }

            return TaskHelper.Completed;
        }

        /// <summary>
        /// Register a callback to be invoked when the given Channel UUID hangs up.
        /// </summary>
        /// <param name="uuid">The Channel UUID.</param>
        /// <param name="action">A Callback to be invoked on hangup.</param>
        public void OnHangup(string uuid, Action<EventMessage> action)
        {
            ChannelEvents.Where(x => x.UUID == uuid && x.EventName == EventNames.ChannelHangup).Take(1).Subscribe(action);
        }

        protected Task EnsureEventsSubscribed()
        {
            var sb = StringBuilderPool.Allocate();
            sb.Append("event plain");

            foreach (var @event in _subscribedEvents)
            {
                sb.Append(" ");
                sb.Append(@event.ToString().ToUpperWithUnderscores());
            }

            if (_customEvents.Any())
            {
                sb.Append(" CUSTOM ");

                foreach (var @event in _customEvents)
                {
                    sb.Append(" ");
                    sb.Append(@event);
                }
            }

            return SendCommand(StringBuilderPool.ReturnAndFree(sb));
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed != null && !_disposed.EnsureCalledOnce())
            {
                if (disposing)
                {
                    // cancel any outgoing network sends
                    if (_cancelationTokenSource != null)
                    {
                        _cancelationTokenSource.Cancel();
                    }
                }
            }

            base.Dispose(disposing);
        }
    }
}
