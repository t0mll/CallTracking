using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Bridge;
using CallTraking.NEventSocket.Common.FreeSWITCH.Events;
using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;
using System;
using System.Threading.Tasks;

namespace CallTraking.NEventSocket.Common.Sockets.Interfaces
{
    public interface IEventSocket
    {
        IObservable<ChannelEvent> ChannelEvents { get; }
        IObservable<ConferenceEvent> ConferenceEvents { get; }
        IObservable<EventMessage> Events { get; }
        long EventSocketId { get; }
        IObservable<BasicMessage> Messages { get; }

        Task<BackgroundJobResult> BackgroundJob(string command, string arg = null, Guid? jobUUID = null);
        Task<BridgeResult> Bridge(string uuid, string endpoint, BridgeOptions options = null);
        Task<ChannelEvent> ExecuteApplication(string uuid, string application, string applicationArguments = null, bool eventLock = false, bool async = false, int loops = 1);
        Task Exit();
        void OnHangup(string uuid, Action<EventMessage> action);
        Task<ApiResponse> SendApi(string command);
        Task<CommandReply> SendCommand(string command);
        Task SubscribeCustomEvents(params string[] events);
        Task SubscribeEvents(params EventNames[] events);
    }
}