namespace Libplanet.Headless;

using Libplanet.Net.Protocols;

// TODO: Implement TypeConverter for Libplanet types
// https://github.com/planetarium/libplanet/issues/2709
public class Configuration
{
    public Uri? GenesisBlockPath { get; init; }

    public string? Host { get; init; }

    public ushort Port { get; init; }

    public Uri? StoreUri { get; init; }

    public Uri[] IceServerUris { get; init; } = Array.Empty<Uri>();

    public string[] PeerStrings { get; init; } = Array.Empty<string>();

    public string? AppProtocolVersion { get; init; }

    public string[]? TrustedAppProtocolVersionSigners { get; init; } = Array.Empty<string>();

    public Uri? GraphQLUri { get; init; }

    public int TxLifetimeMins { get; init; } = 180;

    public int MinimumBroadcastTarget { get; init; } = 10;

    // TODO: Give a better name e.g. KademliaBucketSize
    public int BucketSize { get; init; } = Kademlia.BucketSize;

    public string[] StaticPeerStrings { get; init; } = Array.Empty<string>();
}
