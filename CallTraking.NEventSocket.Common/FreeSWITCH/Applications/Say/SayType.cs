namespace CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Say
{
    /// <summary>
    /// The module to use with the Say dialplan application
    /// </summary>
    public enum SayType
    {
        Number,
        Items,
        Persons,
        Messages,
        Currency,
        TimeMeasurement,
        CurrentDate,
        CurrentTime,
        CurrentDateTime,
        TelephoneNumber,
        TelephoneExtension,
        Url,
        IpAddress,
        EmailAddress,
        PostalAddress,
        AccountNumber,
        NameSpelled,
        NamePhonetic,
        ShortDateTime
    }
}
