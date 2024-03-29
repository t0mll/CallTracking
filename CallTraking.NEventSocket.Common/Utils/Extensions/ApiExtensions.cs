﻿using CallTraking.NEventSocket.Common.FreeSWITCH.Messages;
using CallTraking.NEventSocket.Common.Sockets;
using CallTraking.NEventSocket.Common.Utils.ObjectPooling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallTraking.NEventSocket.Common.Utils.Extensions
{
    /// <summary>
    /// Defines ESL Api Operations that operate on either an <seealso cref="InboundSocket"/> or an <seealso cref="OutboundSocket"/>.
    /// </summary>
    /// <remarks>
    /// Requires the "full" flag to be set on an OutboundSocket in the dialplan.
    /// </remarks>
    public static class ApiExtensions
    {
        /// <summary>
        /// Send an api command (blocking mode)
        /// </summary>
        /// <remarks>
        /// See https://freeswitch.org/confluence/display/FREESWITCH/mod_event_socket#mod_event_socket-api
        /// </remarks>
        /// <param name="eventSocket">The EventSocket instance to execute on.</param>
        /// <param name="command">The API command to send (see https://wiki.freeswitch.org/wiki/Mod_commands) </param>
        /// <param name="arg">(Optional) any arguments for the api command.</param>
        /// <returns>A Task of <seealso cref="ApiResponse"/>.</returns>
        public static Task<ApiResponse> Api(this EventSocket eventSocket, string command, string arg = null)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            return eventSocket.SendApi(arg != null ? $"{command} {arg}" : command);
        }

        /// <summary>
        /// Sets a variable on a channel. If value is omitted, the variable is unset.
        /// </summary>
        /// <remarks>
        /// See https://wiki.freeswitch.org/wiki/Mod_commands#uuid_setvar
        /// </remarks>
        /// <param name="eventSocket">The EventSocket instance to execute on.</param>
        /// <param name="uuid">The Channel UUID.</param>
        /// <param name="variable">The Channel Variable.</param>
        /// <param name="value">The value to assign to the <paramref name="variable">Channel Variable</paramref>.</param>
        /// <returns>A Task of <seealso cref="ApiResponse"/>.</returns>
        public static Task<ApiResponse> SetChannelVariable(this EventSocket eventSocket, string uuid, string variable, object value)
        {
            return eventSocket.SendApi($"uuid_setvar {uuid} {variable} {value}");
        }

        /// <summary>
        /// Set Multiple Channel Variables in one go
        /// </summary>
        /// <remarks>
        /// See https://wiki.freeswitch.org/wiki/Mod_commands#uuid_setvar_multi
        /// </remarks>
        /// <param name="eventSocket">The EventSocket instance.</param>
        /// <param name="uuid">The Channel UUID.</param>
        /// <param name="assignments">Array of assignments in the form "foo=value", "bar=value".</param>
        /// <returns>A Task of <seealso cref="ApiResponse"/> representing the CHANNEL_EXECUTE_COMPLETE event.</returns>
        public static Task<ApiResponse> SetMultipleChannelVariables(this EventSocket eventSocket, string uuid, params string[] assignments)
        {
            var asg = assignments.Aggregate(
                        StringBuilderPool.Allocate(),
                        (sb, s) =>
                        {
                            sb.Append(s);
                            sb.Append(";");
                            return sb;
                        },
                        StringBuilderPool.ReturnAndFree);

            return eventSocket.SendApi(
                $"uuid_setvar_multi {uuid} {asg}");
        }
    }
}
