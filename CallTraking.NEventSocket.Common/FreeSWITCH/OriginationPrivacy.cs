using System;

namespace CallTraking.NEventSocket.Common.FreeSWITCH
{
    /// <summary>
    /// Defines the Origination Privacy
    /// See https://wiki.freeswitch.org/wiki/Variable_origination_privacy
    /// </summary>
    [Flags]
    public enum OriginationPrivacy
    {
        HideName,
        HideNumber,
        Screen
    }
}
