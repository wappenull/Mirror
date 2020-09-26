namespace Telepathy
{
    public enum EventType
    {
        Connected,
        Data,
        Disconnected,

        /// <summary>
        /// Added by wappen for game layer to determine address origin.
        /// </summary>
        PreConnect = 10,
    }
}
