using Chatbot.Application.Common.Interfaces;

namespace Chatbot.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
