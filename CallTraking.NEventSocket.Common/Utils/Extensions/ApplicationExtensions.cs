﻿using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Play;
using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.PlayGetDigits;
using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Read;
using CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Say;
using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;
using CallTraking.NEventSocket.Common.Sockets;
using CallTraking.NEventSocket.Common.Sockets.Interfaces;
using System.Threading.Tasks;

namespace CallTraking.NEventSocket.Common.Utils.Extensions
{
    /// <summary>
    /// Defines Application operations that can operate on either an <seealso cref="InboundSocket"/> or an <seealso cref="OutboundSocket"/>.
    /// </summary>
    /// <remarks>
    /// See https://freeswitch.org/confluence/display/FREESWITCH/mod_dptools
    /// </remarks>
    public static class ApplicationExtensions
    {
        /// <summary>
        /// Plays the given file to the specified channel.
        /// </summary>
        /// <param name="eventSocket">The EventSocket instance.</param>
        /// <param name="uuid">The Channel UUID.</param>
        /// <param name="file">The Path to the file to be played. Note: use forward slashes for path separators.</param>
        /// <param name="options">Options to customize playback.</param>
        /// <returns>A PlayResult.</returns>
        /// <exception cref="FileNotFoundException">Throws FileNotFoundException if FreeSwitch is unable to play the file.</exception>//todo: should it?
        public static async Task<PlayResult> Play(this IEventSocket eventSocket, string uuid, string file, PlayOptions options = null)
        {
            // TODO(TC): implement options for playback eg a-leg, b-leg, both, using uuid_displace
            if (options == null)
            {
                options = new PlayOptions();
            }

            try
            {
                // TODO(TC): what if applicationresult is null (hang up occurs before the application completes)
                var result =
                    new PlayResult(
                        await
                        eventSocket.ExecuteApplication(uuid, "playback", file, loops: options.Loops)
                                   .ConfigureAwait(false));

                return result;
            }
            catch (TaskCanceledException)
            {
                return new PlayResult(null);
            }
        }

        public static async Task<PlayGetDigitsResult> PlayGetDigits(this IEventSocket eventSocket, string uuid, PlayGetDigitsOptions options)
        {
            try
            {
                // todo: what if applicationresult is null (hang up occurs before the application completes)
                var result =
                    new PlayGetDigitsResult(
                        await eventSocket.ExecuteApplication(uuid, "play_and_get_digits", options.ToString()).ConfigureAwait(false),
                        options.ChannelVariableName);

                return result;
            }
            catch (TaskCanceledException)
            {
                return new PlayGetDigitsResult(null, null);
            }
        }

        public static async Task<ReadResult> Read(this IEventSocket eventSocket, string uuid, ReadOptions options)
        {
            try
            {
                // todo: what if applicationresult is null (hang up occurs before the application completes)
                var result = new ReadResult(
                    await eventSocket.ExecuteApplication(uuid, "read", options.ToString()).ConfigureAwait(false),
                    options.ChannelVariableName);

                return result;
            }
            catch (TaskCanceledException)
            {
                return new ReadResult(null, null);
            }
        }

        public static Task<ChannelEvent> Say(this IEventSocket eventSocket, string uuid, SayOptions options)
        {
            return eventSocket.ExecuteApplication(uuid, "say", options.ToString());
        }

        public static Task<ChannelEvent> StartDtmf(this IEventSocket eventSocket, string uuid)
        {
            return eventSocket.ExecuteApplication(uuid, "spandsp_start_dtmf");
        }

        public static Task<ChannelEvent> StopDtmf(this IEventSocket eventSocket, string uuid)
        {
            return eventSocket.ExecuteApplication(uuid, "spandsp_stop_dtmf");
        }
    }
}
