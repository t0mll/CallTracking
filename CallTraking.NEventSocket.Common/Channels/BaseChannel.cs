using CallTraking.NEventSocket.Common.FreeSWITCH.Applications;
using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.AttendedTransfer;
using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Play;
using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.PlayGetDigits;
using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Read;
using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Say;
using CallTraking.NEventSocket.Common.FreeSWITCH.Channel;
using CallTraking.NEventSocket.Common.FreeSWITCH.Events;
using CallTraking.NEventSocket.Common.FreeSWITCH.Headers;
using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;
using CallTraking.NEventSocket.Common.Sockets;
using CallTraking.NEventSocket.Common.Utils;
using CallTraking.NEventSocket.Common.Utils.Extensions;
using CallTraking.NEventSocket.Common.Utils.ObjectPooling;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CallTraking.NEventSocket.Common.Channels
{
    public abstract class BaseChannel
    {
        private readonly ILogger<BaseChannel> _logger;
        protected readonly CompositeDisposable Disposables = new CompositeDisposable();
        private readonly InterlockedBoolean _disposed = new InterlockedBoolean(false);
        protected EventSocket EventSocket;
        protected ChannelEvent LastEvent;
        private Action<ChannelEvent> _hangupCallback = (e) => { };
        private string _recordingPath;
        private RecordingStatus recordingStatus = RecordingStatus.NotRecording;

        ~BaseChannel()
        {
            Dispose(false);
        }

        protected BaseChannel(ChannelEvent eventMessage, EventSocket EventSocket, ILogger<BaseChannel> logger = null)
        {
            _logger = logger;
            UUID = eventMessage.UUID;
            LastEvent = eventMessage;
            this.EventSocket = EventSocket;

            Variables = new ChannelVariables(this);

            Disposables.Add(
                EventSocket.ChannelEvents
                           .Where(x => x.UUID == UUID)
                           .Subscribe(
                               e =>
                               {
                                   LastEvent = e;

                                   if (e.EventName == EventNames.ChannelAnswer)
                                   {
                                       _logger?.LogInformation($"Channel [{UUID}] Answered");
                                   }

                                   if (e.EventName == EventNames.ChannelHangupComplete)
                                   {
                                       _logger?.LogInformation($"Channel [{UUID}] Hangup Detected [{e.HangupCause}]");

                                       try
                                       {
                                           HangupCallBack(e);
                                       }
                                       catch (Exception ex)
                                       {
                                           _logger?.LogError(ex, $"Channel [{UUID}] error calling hangup callback");
                                       }

                                       Dispose();
                                   }
                               }));
        }

        public string UUID { get; protected set; }

        public ChannelState ChannelState
        {
            get
            {
                // should always be populated, otherwise will throw invalidoperationexception
                // which means we've introduced a b-u-g and are listening to non-channel events
                return LastEvent.ChannelState.Value;
            }
        }

        public AnswerState? Answered
        {
            get
            {
                return LastEvent.AnswerState;
            }
        }

        public HangupCause? HangupCause
        {
            get
            {
                return LastEvent.HangupCause;
            }
        }
        public RecordingStatus RecordingStatus { get { return recordingStatus; } }

        public Action<ChannelEvent> HangupCallBack
        {
            get
            {
                return _hangupCallback;
            }

            set
            {
                _hangupCallback = value;
            }
        }

        public IObservable<string> Dtmf
        {
            get
            {
                return
                    EventSocket.ChannelEvents.Where(x => x.UUID == UUID && x.EventName == EventNames.Dtmf)
                        .Select(x => x.Headers[HeaderNames.DtmfDigit]);
            }
        }

        public EventSocket Socket { get { return EventSocket; } }

        public IDictionary<string, string> Headers { get { return LastEvent.Headers; } }

        public ChannelVariables Variables { get; private set; }

        public bool IsBridged
        {
            get
            {
                return LastEvent != null && LastEvent.Headers.ContainsKey(HeaderNames.OtherLegUniqueId) && LastEvent.Headers[HeaderNames.OtherLegUniqueId] != null; //this.BridgedChannel != null; // 
            }
        }

        public bool IsAnswered
        {
            get
            {
                return Answered.HasValue && Answered.Value == AnswerState.Answered;
            }
        }

        public bool IsPreAnswered
        {
            get
            {
                return Answered.HasValue && Answered.Value == AnswerState.Early;
            }
        }

        public string GetHeader(string headerName)
        {
            return LastEvent.GetHeader(headerName);
        }

        public string GetVariable(string variableName)
        {
            return LastEvent.GetVariable(variableName);
        }

        public IObservable<string> FeatureCodes(string prefix = "#")
        {
            return EventSocket
                       .ChannelEvents.Where(x => x.UUID == UUID && x.EventName == EventNames.Dtmf)
                       .Select(x => x.Headers[HeaderNames.DtmfDigit])
                       .Buffer(TimeSpan.FromSeconds(2), 2)
                       .Where(x => x.Count == 2 && x[0] == prefix)
                       .Select(x => string.Concat(x))
                       .Do(x => _logger?.LogDebug($"Channel {UUID} detected Feature Code {x}"));
        }

        public Task Hangup(HangupCause hangupCause = FreeSWITCH.Channel.HangupCause.NormalClearing)
        {
            return RunIfAnswered(() => EventSocket.SendApi($"uuid_kill {UUID} {hangupCause.ToString().ToUpperWithUnderscores()}"), true);
        }

        public async Task<PlayResult> Play(string file, Leg leg = Leg.ALeg, string terminator = null)
        {
            if (!CanPlayBackAudio)
            {
                return new PlayResult(null);
            }

            if (terminator != null && LastEvent.GetVariable("playback_terminators") != terminator)
            {
                await SetChannelVariable("playback_terminators", terminator).ConfigureAwait(false);
            }

            var bLegUUID = LastEvent.GetHeader(HeaderNames.OtherLegUniqueId);

            if (leg == Leg.ALeg || bLegUUID == null)
            {
                return await EventSocket.Play(UUID, file, new PlayOptions()).ConfigureAwait(false);
            }
            switch (leg)
            {
                case Leg.Both:
                    return (await
                        Task.WhenAll(
                                EventSocket.Play(UUID, file, new PlayOptions()),
                                EventSocket.Play(bLegUUID, file, new PlayOptions()))
                            .ConfigureAwait(false)).First();
                case Leg.BLeg:
                    return await EventSocket.Play(bLegUUID, file, new PlayOptions()).ConfigureAwait(false);
                default:
                    throw new NotSupportedException($"Leg {leg} is not supported");
            }
        }

        public Task Play(IEnumerable<string> files, Leg leg = Leg.ALeg, string terminator = null)
        {
            var sb = StringBuilderPool.Allocate();
            var first = true;

            sb.Append("file_string://");

            foreach (var file in files)
            {
                if (!first)
                {
                    sb.Append("!");
                }
                sb.Append(file);
                first = false;
            }

            return Play(StringBuilderPool.ReturnAndFree(sb), leg, terminator);
        }

        /// <summary>
        /// Plays the provided audio source to the A-Leg.
        /// Dispose the returned token to cancel playback.
        /// </summary>
        /// <param name="file">The audio source.</param>
        /// <returns>An <seealso cref="IDisposable"/> which can be disposed to stop the audio.</returns>
        public async Task<IDisposable> PlayUntilCancelled(string file)
        {
            if (!CanPlayBackAudio)
            {
                _logger?.LogWarning($"Channel [{UUID}] attempted to play hold music when not answered");
                return Task.FromResult(new DisposableAction());
            }

            // essentially, we'll do a playback application call without waiting for the ChannelExecuteComplete event
            // the caller can .Dispose() the returned token to do a uuid_break on the channel to kill audio.
            await EventSocket.SendCommand(string.Format("sendmsg {0}\ncall-command: execute\nexecute-app-name: playback\nexecute-app-arg:{1}\nloops:-1", UUID, file));

            var cancellation = new DisposableAction(
                async () =>
                {
                    if (!CanPlayBackAudio)
                    {
                        return;
                    }

                    try
                    {
                        await EventSocket.Api("uuid_break", UUID);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error calling 'api uuid_break {UUID}'");
                    }
                });

            return cancellation;
        }

        /// Returns true if audio playback is currently possible, false otherwise.
        bool CanPlayBackAudio => (IsAnswered || IsPreAnswered) && Socket?.IsConnected == true;

        public Task<PlayGetDigitsResult> PlayGetDigits(PlayGetDigitsOptions options)
        {
            return RunIfAnswered(() => EventSocket.PlayGetDigits(UUID, options), () => new PlayGetDigitsResult(null, null));
        }

        public Task<ReadResult> Read(ReadOptions options)
        {
            return RunIfAnswered(() => EventSocket.Read(UUID, options), () => new ReadResult(null, null));
        }

        public Task Say(SayOptions options)
        {
            return RunIfAnswered(() => EventSocket.Say(UUID, options));
        }

        /// <summary>
        /// Performs an attended transfer. If succeded, it will replace the Bridged Channel of the other Leg.
        /// </summary>
        /// <remarks>
        /// See https://freeswitch.org/confluence/display/FREESWITCH/Attended+Transfer
        /// </remarks>
        /// <param name="endpoint">The endpoint to transfer to eg. user/1000, sofia/foo@bar.com etc</param>
        public Task<AttendedTransferResult> AttendedTransfer(string endpoint)
        {
            try
            {
                var tcs = new TaskCompletionSource<AttendedTransferResult>();
                var subscriptions = new CompositeDisposable();

                var aLegUUID = LastEvent.Headers[HeaderNames.OtherLegUniqueId];
                var bLegUUID = UUID;

                var events = EventSocket.ChannelEvents;

                _logger?.LogDebug($"Att XFer Starting A-Leg [{aLegUUID}] B-Leg [{bLegUUID}]");

                var aLegHangup = events.Where(x => x.EventName == EventNames.ChannelHangup && x.UUID == aLegUUID)
                                        .Do(x => _logger?.LogDebug($"Att XFer Hangup Detected on A-Leg [{x.UUID}]"));

                var bLegHangup = events.Where(x => x.EventName == EventNames.ChannelHangup && x.UUID == bLegUUID)
                                        .Do(x => _logger?.LogDebug($"Att XFer Hangup Detected on B-Leg [{x.UUID}]"));

                var cLegHangup = events.Where(x => x.EventName == EventNames.ChannelHangup && x.UUID != bLegUUID && x.UUID != aLegUUID)
                                        .Do(x => _logger?.LogDebug($"Att XFer Hangup Detected on C-Leg[{x.UUID}]"));

                var cLegAnswer =
                    events.Where(x => x.EventName == EventNames.ChannelAnswer && x.UUID != bLegUUID && x.UUID != aLegUUID)
                          .Do(x => _logger?.LogDebug($"Att XFer Answer Detected on C-Leg [{x.UUID}]"));

                var aLegBridge =
                    events.Where(x => x.EventName == EventNames.ChannelBridge && x.UUID == aLegUUID)
                          .Do(x => _logger?.LogDebug($"Att XFer Bridge Detected on A-Leg [{x.UUID}]"));

                var cLegBridge =
                    events.Where(x => x.EventName == EventNames.ChannelBridge && x.UUID != bLegUUID && x.UUID != aLegUUID)
                          .Do(x => _logger?.LogDebug($"Att XFer Bridge Detected on C-Leg [{x.UUID}]"));


                var channelExecuteComplete =
                    events.Where(
                        x =>
                            x.EventName == EventNames.ChannelExecuteComplete
                            && x.UUID == bLegUUID
                            && x.GetHeader(HeaderNames.Application) == "att_xfer");


                var cAnsweredThenHungUp =
                    cLegAnswer.And(cLegHangup)
                        .And(channelExecuteComplete.Where(
                                x =>
                                    x.GetVariable("att_xfer_result") == "success"
                                    && x.GetVariable("last_bridge_hangup_cause") == "NORMAL_CLEARING"
                                    && x.GetVariable("originate_disposition") == "SUCCESS"));

                var cAnsweredThenBPressedStarOrHungUp =
                    cLegAnswer.And(bLegHangup)
                        .And(cLegBridge.Where(x => x.OtherLegUUID == aLegUUID));

                subscriptions.Add(channelExecuteComplete.Where(x => x.GetVariable("originate_disposition") != "SUCCESS")
                    .Subscribe(
                        x =>
                        {
                            _logger?.LogDebug("Att Xfer Not Answered");
                            tcs.TrySetResult(AttendedTransferResult.Failed(x.GetVariable("originate_disposition").HeaderToEnum<HangupCause>()));

                        }));

                subscriptions.Add(Observable.When(cAnsweredThenHungUp.Then((answer, hangup, execComplete) => new { answer, hangup, execComplete }))
                                            .Subscribe(
                                                x =>
                                                {
                                                    _logger?.LogDebug("Att Xfer Rejected after C Hungup");
                                                    tcs.TrySetResult(AttendedTransferResult.Failed(FreeSWITCH.Channel.HangupCause.NormalClearing));
                                                }));

                subscriptions.Add(channelExecuteComplete.Where(x => !string.IsNullOrEmpty(x.GetVariable("xfer_uuids")))
                                            .Subscribe(x =>
                                            {
                                                _logger?.LogDebug("Att Xfer Success (threeway)");
                                                tcs.TrySetResult(AttendedTransferResult.Success(AttendedTransferResultStatus.Threeway));
                                            }));

                subscriptions.Add(Observable.When(cAnsweredThenBPressedStarOrHungUp.Then((answer, hangup, bridge) => new { answer, hangup, bridge }))
                                            .Subscribe(
                                                x =>
                                                {
                                                    _logger?.LogDebug("Att Xfer Succeeded after B pressed *");
                                                    tcs.TrySetResult(AttendedTransferResult.Success());
                                                }));

                subscriptions.Add(Observable.When(bLegHangup.And(cLegAnswer).And(aLegBridge.Where(x => x.OtherLegUUID != bLegUUID)).Then((hangup, answer, bridge) => new { answer, hangup, bridge }))
                                            .Subscribe(
                                                x =>
                                                {
                                                    _logger?.LogDebug("Att Xfer Succeeded after B hung up and C answered");
                                                    tcs.TrySetResult(AttendedTransferResult.Success());
                                                }));

                subscriptions.Add(aLegHangup.Subscribe(
                    x =>
                    {
                        _logger?.LogDebug("Att Xfer Failed after A-Leg Hung Up");
                        tcs.TrySetResult(AttendedTransferResult.Hangup(x));
                    }));

                EventSocket.ExecuteApplication(UUID, "att_xfer", endpoint, false, true)
                           .ContinueOnFaultedOrCancelled(tcs, subscriptions.Dispose);

                return tcs.Task.Then(() => subscriptions.Dispose());
            }
            catch (TaskCanceledException)
            {
                return Task.FromResult(AttendedTransferResult.Failed(FreeSWITCH.Channel.HangupCause.None));
            }
        }

        public Task StartDetectingInbandDtmf()
        {
            return RunIfAnswered(
                async () =>
                {
                    await EventSocket.SubscribeEvents(EventNames.Dtmf).ConfigureAwait(false);
                    await EventSocket.StartDtmf(UUID).ConfigureAwait(false);
                });
        }

        public Task StopDetectingInbandDtmf()
        {
            return RunIfAnswered(() => EventSocket.StopDtmf(UUID));
        }

        public Task SetChannelVariable(string name, string value)
        {
            return RunIfAnswered(
                () =>
                {
                    _logger?.LogDebug($"Channel {UUID} setting variable '{name}' to '{value}'");
                    return EventSocket.SendApi($"uuid_setvar {UUID} {name} {value}");
                });
        }

        /// <summary>
        /// Send DTMF digits to the channel
        /// </summary>
        /// <param name="digits">String with digits or characters</param>
        /// <param name="duration">Duration of each symbol (default -- 2000ms)</param>
        /// <returns></returns>
        public Task SendDTMF(string digits, TimeSpan? duration = null)
        {
            var durationMs = duration.HasValue ? duration.Value.TotalMilliseconds : 2000; // default value in freeswitch
            return EventSocket.ExecuteApplication(UUID, "send_dtmf", $"{digits}@{durationMs}");
        }

        public Task StartRecording(string file, int? maxSeconds = null)
        {
            return RunIfAnswered(
                async () =>
                {
                    if (file == _recordingPath)
                    {
                        return;
                    }

                    if (_recordingPath != null)
                    {
                        _logger?.LogWarning($"Channel {UUID} received a request to record to file {file} " +
                            $"while currently recording to file {_recordingPath}. " +
                            $"Channel will stop recording and start recording to the new file.");
                        await StopRecording().ConfigureAwait(false);
                    }

                    _recordingPath = file;
                    await EventSocket.SendApi($"uuid_record {UUID} start {_recordingPath} {maxSeconds}").ConfigureAwait(false);
                    _logger?.LogDebug($"Channel {UUID} is recording to {_recordingPath}");
                    recordingStatus = RecordingStatus.Recording;
                });
        }

        public Task MaskRecording()
        {
            return RunIfAnswered(
                async () =>
                {
                    if (string.IsNullOrEmpty(_recordingPath))
                    {
                        _logger?.LogWarning($"Channel {UUID} is not recording");
                    }
                    else
                    {
                        await EventSocket.SendApi($"uuid_record {UUID} mask {_recordingPath}").ConfigureAwait(false);
                        _logger?.LogDebug($"Channel {UUID} has masked recording to {_recordingPath}");
                        recordingStatus = RecordingStatus.Paused;
                    }
                });
        }

        public Task UnmaskRecording()
        {
            return RunIfAnswered(
                async () =>
                {
                    if (string.IsNullOrEmpty(_recordingPath))
                    {
                        _logger?.LogWarning($"Channel {UUID} is not recording");
                    }
                    else
                    {
                        await EventSocket.SendApi($"uuid_record {UUID} unmask {_recordingPath}").ConfigureAwait(false);
                        _logger?.LogDebug($"Channel {UUID} has unmasked recording to {_recordingPath}");
                        recordingStatus = RecordingStatus.Recording;
                    }
                });
        }

        public Task StopRecording()
        {
            return RunIfAnswered(
                async () =>
                {
                    if (string.IsNullOrEmpty(_recordingPath))
                    {
                        _logger?.LogWarning($"Channel {UUID} is not recording");
                    }
                    else
                    {
                        await EventSocket.SendApi($"uuid_record {UUID} stop {_recordingPath}").ConfigureAwait(false);
                        _recordingPath = null;
                        _logger?.LogDebug($"Channel {UUID} has stopped recording to {_recordingPath}");
                        recordingStatus = RecordingStatus.NotRecording;
                    }
                });
        }

        public Task Exit()
        {
            return EventSocket.Exit();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed != null && _disposed.EnsureCalledOnce())
            {
                if (disposing)
                {
                    if (Disposables != null)
                    {
                        Disposables.Dispose();
                    }
                }

                _logger?.LogDebug("BaseChannel Disposed.");
            }
        }

        /// <summary>
        /// Runs the given async function if the Channel is still connected, otherwise a completed Task.
        /// </summary>
        /// <param name="toRun">An Async function.</param>
        /// <param name="orPreAnswered">Function also run in pre answer state</param>
        protected Task RunIfAnswered(Func<Task> toRun, bool orPreAnswered = false)
        {
            //check not disposed, socket is not null and connected, no hangup event received
            if (_disposed.Value || !EventSocket.IsConnected || !IsAnswered && (!orPreAnswered || !IsPreAnswered))
            {
                return TaskHelper.Completed;
            }

            return toRun();
        }

        /// <summary>
        /// Runs the given async function if the Channel is still connected, otherwise uses the provided function to return a default value.
        /// </summary>
        /// <param name="toRun">An Async function.</param>
        /// <param name="defaultValueProvider">Function returning the default value</param>
        protected Task<T> RunIfAnswered<T>(Func<Task<T>> toRun, Func<T> defaultValueProvider)
        {
            if (_disposed.Value || !EventSocket.IsConnected || !IsAnswered)
            {
                return Task.FromResult(defaultValueProvider());
            }

            return toRun();
        }
    }
}
