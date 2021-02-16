namespace CallTraking.NEventSocket.Common.FreeSWITCH.Applications.Play
{
    /// <summary>
    /// Options for customizing the behaviour of the Play dialplan application
    /// </summary>
    public class PlayOptions
    {
        private int loops = 1;

        /// <summary>
        /// Gets or sets the number of repetitions to play (default 1).
        /// </summary>
        public int Loops
        {
            get
            {
                return loops;
            }

            set
            {
                loops = value;
            }
        }
    }
}
