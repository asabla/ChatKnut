namespace Common.TwitchChat.Models;

internal class IrcEventArgs : EventArgs
{
    public RawIrcMessage IrcMessage { get; set; }

    public IrcEventArgs(RawIrcMessage ircMessage)
    {
        IrcMessage = ircMessage;
    }
}