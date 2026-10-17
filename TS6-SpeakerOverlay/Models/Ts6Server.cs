using System.Collections.Generic;

namespace TS6_SpeakerOverlay.Models
{
    public sealed class Ts6Server
    {
        public int ConnectionId { get; init; }
        public string UniqueIdentifier { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int MyClientId { get; init; }
        public string MyChannelId { get; set; } = string.Empty;
        public List<User> Users { get; init; } = new();
    }
}
