using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Bridge;
using CallTraking.NEventSocket.Common.FreeSWITCH.Events;
using CallTraking.NEventSocket.Common.FreeSWITCH.Headers;
using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;
using CallTraking.NEventSocket.Common.Sockets;
using CallTraking.NEventSocket.Common.Utils;
using CallTraking.NEventSocket.Common.Utils.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CallTraking.NEventSocket.Common.Channels
{
    public class Channel : BaseChannel
    {
        private readonly InterlockedBoolean _initialized = new InterlockedBoolean();
        private readonly InterlockedBoolean _disposed = new InterlockedBoolean();
        private readonly BehaviorSubject<BridgedChannel> _bridgedChannelsSubject = new BehaviorSubject<BridgedChannel>(null);

        private string _bridgedUUID;
        private readonly ILogger<Channel> _logger;

        protected internal Channel(OutboundSocket outboundSocket, ILogger<Channel> logger = null) : this(outboundSocket.ChannelData, outboundSocket, logger)
        {
            _logger = logger;
        }

        protected internal Channel(ChannelEvent eventMessage, EventSocket eventSocket, ILogger<Channel> logger = null) : base(eventMessage, eventSocket, logger)
        {
            _logger = logger;
            LingerTime = 10;
        }

        internal static async Task<Channel> Create(OutboundSocket outboundSocket, ILogger<Channel> logger = null)
        {
            var channel = new Channel(outboundSocket, logger)
            {
                ExitOnHangup = true
            };

            await outboundSocket.Linger().ConfigureAwait(false);


            await outboundSocket.SubscribeEvents(
               EventNames.ChannelProgress,
               EventNames.ChannelBridge,
               EventNames.ChannelUnbridge,
               EventNames.ChannelAnswer,
               EventNames.ChannelHangup,
               EventNames.ChannelHangupComplete,
               EventNames.Dtmf).ConfigureAwait(false); //subscribe to minimum events

            await outboundSocket.Filter(HeaderNames.UniqueId, outboundSocket.ChannelData.UUID).ConfigureAwait(false); //filter for our unique id (in case using full socket mode)
            await outboundSocket.Filter(HeaderNames.OtherLegUniqueId, outboundSocket.ChannelData.UUID).ConfigureAwait(false); //filter for channels bridging to our unique id
            await outboundSocket.Filter(HeaderNames.ChannelCallUniqueId, outboundSocket.ChannelData.UUID).ConfigureAwait(false); //filter for channels bridging to our unique id

            channel.InitializeSubscriptions();
            return channel;
        }

        ~Channel()
        {
            Dispose(false);
        }
        public IObservable<ChannelEvent> Events { get { return Socket.ChannelEvents.Where(x => x.UUID == UUID).AsObservable(); } }

        public IObservable<BridgedChannel> BridgedChannels { get { return _bridgedChannelsSubject.Where(x => x != null).AsObservable(); } }

        public BridgedChannel OtherLeg => _bridgedChannelsSubject.Value;

        public bool ExitOnHangup { get; set; }

        public int LingerTime { get; set; }

        public async Task BridgeTo(string destination, BridgeOptions options, Action<EventMessage> onProgress = null)
        {
            if (!IsAnswered && !IsPreAnswered)
            {
                return;
            }

            _logger?.LogDebug($"Channel {UUID} is attempting a bridge to {destination}");

            if (string.IsNullOrEmpty(options.UUID))
            {
                options.UUID = Guid.NewGuid().ToString();
            }

            var subscriptions = new CompositeDisposable();

            if (onProgress != null)
            {
                subscriptions.Add(
                    EventSocket.ChannelEvents.Where(x => x.UUID == options.UUID && x.EventName == EventNames.ChannelProgress)
                        .Take(1)
                        .Subscribe(onProgress));
            }

            var bridgedChannel = this.BridgedChannels.FirstAsync(x => x.UUID == options.UUID);
            var result = await EventSocket.Bridge(UUID, destination, options).ConfigureAwait(false);

            _logger?.LogDebug($"Channel {UUID} bridge complete {result.Success} {result.ResponseText}");
            subscriptions.Dispose();

            if (result.Success)
            {
                //wait for this.OtherLeg to be set before completing
                await bridgedChannel;
            }
        }

        public Task Execute(string application, string args)
        {
            return EventSocket.ExecuteApplication(UUID, application, args);
        }

        public Task Execute(string uuid, string application, string args)
        {
            return EventSocket.ExecuteApplication(uuid, application, args);
        }

        public Task HoldToggle()
        {
            return RunIfAnswered(() => EventSocket.SendApi("uuid_hold toggle " + UUID));
        }

        public Task HoldOn()
        {
            return RunIfAnswered(() => EventSocket.SendApi("uuid_hold " + UUID));
        }

        public Task HoldOff()
        {
            return RunIfAnswered(() => EventSocket.SendApi("uuid_hold off " + UUID));
        }

        public Task Park()
        {
            return RunIfAnswered(() => EventSocket.ExecuteApplication(UUID, "park"));
        }

        public Task RingReady()
        {
            return EventSocket.ExecuteApplication(UUID, "ring_ready");
        }

        public Task Answer()
        {
            return EventSocket.ExecuteApplication(UUID, "answer");
        }

        public Task EnableHeartBeat(int intervalSeconds = 60)
        {
            return RunIfAnswered(
                async () =>
                {
                    await EventSocket.SubscribeEvents(EventNames.SessionHeartbeat).ConfigureAwait(false);
                    await EventSocket.ExecuteApplication(UUID, "enable_heartbeat", intervalSeconds.ToString()).ConfigureAwait(false);
                }, true);
        }

        public Task PreAnswer()
        {
            return EventSocket.ExecuteApplication(UUID, "pre_answer");
        }

        public Task Sleep(int milliseconds)
        {
            return EventSocket.ExecuteApplication(UUID, "sleep", milliseconds.ToString());
        }

        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual new void Dispose(bool disposing)
        {
            if (_disposed != null && !_disposed.EnsureCalledOnce())
            {
                if (disposing)
                {
                    if (!Disposables.IsDisposed)
                    {
                        Disposables.Dispose();
                    }

                    OtherLeg?.Dispose();
                    _bridgedChannelsSubject.Dispose();
                }

                if (EventSocket != null && EventSocket is OutboundSocket)
                {
                    // todo: should we close the socket associated with the channel here?
                    EventSocket.Dispose();
                }

                EventSocket = null;

                _logger?.LogDebug("Channel Disposed.");
            }

            base.Dispose(disposing);
        }

        private void InitializeSubscriptions()
        {
            if (_initialized.EnsureCalledOnce())
            {
                _logger?.LogWarning("Channel already initialized");
                return;
            }

            Disposables.Add(
                    EventSocket.ChannelEvents.Where(x => x.UUID == UUID
                                            && x.EventName == EventNames.ChannelBridge
                                            && x.OtherLegUUID != _bridgedUUID)
                    .Subscribe(
                        async x =>
                        {
                            _logger?.LogInformation($"Channel [{UUID}] Bridged to [{x.GetHeader(HeaderNames.OtherLegUniqueId)}] CHANNEL_BRIDGE");

                            var apiResponse = await EventSocket.Api("uuid_dump", x.OtherLegUUID);

                            if (apiResponse.Success && apiResponse.BodyText != "+OK")
                            {
                                var eventMessage = new ChannelEvent(apiResponse);
                                _bridgedChannelsSubject.OnNext(new BridgedChannel(eventMessage, EventSocket, _logger));
                            }
                            else
                            {
                                _logger?.LogError($"Unable to get CHANNEL_DATA info from 'api uuid_dump {x.OtherLegUUID}' - received '{apiResponse.BodyText}'.");
                            }
                        }));

            Disposables.Add(
                EventSocket.ChannelEvents.Where(x =>
                                    x.UUID == UUID
                                    && x.EventName == EventNames.ChannelUnbridge
                                    && x.GetVariable("bridge_hangup_cause") != null)
                           .Subscribe(
                               x =>
                               {
                                   /* side effects:
                                    * the att_xfer application is evil
                                    * if after speaking to C, B presses '#' to cancel,
                                    * the A channel fires an unbridge event, even though it is still bridged to B
                                    * in this case, bridge_hangup_cause will be empty so we'll ignore those events
                                    * however, this may break if this channel has had any completed bridges before this. */

                                   _logger?.LogInformation($"Channel [{UUID}] Unbridged from [{x.GetVariable("last_bridge_to")}] {x.GetVariable("bridge_hangup_cause")}");

                                   _bridgedChannelsSubject.OnNext(null); //clears out OtherLeg
                               }));

            Disposables.Add(BridgedChannels.Subscribe(
                async b =>
                {
                    if (_bridgedUUID != null && _bridgedUUID != b.UUID)
                    {
                        await EventSocket.FilterDelete(HeaderNames.UniqueId, _bridgedUUID).ConfigureAwait(false);
                        await EventSocket.FilterDelete(HeaderNames.OtherLegUniqueId, _bridgedUUID).ConfigureAwait(false);
                        await EventSocket.FilterDelete(HeaderNames.ChannelCallUniqueId, _bridgedUUID).ConfigureAwait(false);
                    }

                    _bridgedUUID = b.UUID;

                    await EventSocket.Filter(HeaderNames.UniqueId, _bridgedUUID).ConfigureAwait(false);
                    await EventSocket.Filter(HeaderNames.OtherLegUniqueId, _bridgedUUID).ConfigureAwait(false);
                    await EventSocket.Filter(HeaderNames.ChannelCallUniqueId, _bridgedUUID).ConfigureAwait(false);

                    _logger?.LogTrace($"Channel [{UUID}] setting OtherLeg to [{b.UUID}]");
                }));

            Disposables.Add(
                EventSocket.ChannelEvents.Where(
                    x =>
                    x.EventName == EventNames.ChannelBridge
                    && x.UUID != UUID
                    && x.GetHeader(HeaderNames.OtherLegUniqueId) == UUID
                    && x.UUID != _bridgedUUID)
                    .Subscribe(
                        x =>
                        {
                            //there is another channel out there that has bridged to us but we didn't get the CHANNEL_BRIDGE event on this channel
                            _logger?.LogInformation($"Channel [{UUID}] bridged to [{x.UUID}]] on CHANNEL_BRIDGE received on other channel");
                            _bridgedChannelsSubject.OnNext(new BridgedChannel(x, EventSocket, _logger));
                        }));


            if (EventSocket is OutboundSocket)
            {
                Disposables.Add(
                    EventSocket.ChannelEvents.Where(x => x.UUID == UUID && x.EventName == EventNames.ChannelHangupComplete)
                               .Subscribe(
                                   async e =>
                                   {
                                       if (ExitOnHangup)
                                       {
                                           //give event subscribers time to complete
                                           if (LingerTime > 0)
                                           {
                                               _logger?.LogDebug($"Channel[{UUID}] will exit in {LingerTime} seconds...");
                                               await Task.Delay(LingerTime * 1000);
                                           }

                                           if (EventSocket != null)
                                           {
                                               _logger?.LogInformation($"Channel [{UUID}] exiting");
                                               await EventSocket.Exit().ConfigureAwait(false);
                                           }

                                           Dispose();
                                       }
                                   }));
            }

            _logger?.LogTrace($"Channel [{UUID}] subscriptions initialized");
        }
    }
}
