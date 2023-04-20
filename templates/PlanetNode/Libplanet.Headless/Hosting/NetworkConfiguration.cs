using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using Libplanet.Crypto;
using Libplanet.Net;

namespace Libplanet.Headless.Hosting;

using Libplanet.Net.Protocols;

// TODO: Implement TypeConverter for Libplanet types
// https://github.com/planetarium/libplanet/issues/2709
public class NetworkConfiguration
{
    public string? Host { get; init; }

    public ushort Port { get; init; }

    public string? ConsensusHost { get; init; }

    public ushort ConsensusPort { get; init; }

    public string? AppProtocolVersion { get; init; }

    public string[]? TrustedAppProtocolVersionSigners { get; init; } = Array.Empty<string>();

    public Uri[] IceServerUris { get; init; } = Array.Empty<Uri>();

    public string[] PeerStrings { get; init; } = Array.Empty<string>();

    public int MinimumBroadcastTarget { get; init; } = 10;

    // TODO: Give a better name e.g. KademliaBucketSize
    public int BucketSize { get; init; } = Kademlia.BucketSize;

    public string[] StaticPeerStrings { get; init; } = Array.Empty<string>();

    public HostOptions GetHostOptions(Random? random = null)
    {
        random ??= new Random();
        return new HostOptions(
            host: Host,
            iceServers: IceServerUris
                .Select(uri => new IceServer(uri))
                .OrderBy(_ => random.Next()),
            port: Port);
    }

    public HostOptions GetConsensusHostOptions(Random? random = null)
    {
        random ??= new Random();
        return new HostOptions(
            host: ConsensusHost,
            iceServers: Enumerable.Empty<IceServer>(),
            port: ConsensusPort
        );
    }

    [Pure]
    public SwarmOptions GetSwarmOptions() =>
        new()
        {
            StaticPeers = StaticPeerStrings
                .Select(BoundPeer.ParsePeer)
                .ToImmutableHashSet(),
            BucketSize = BucketSize,
            MinimumBroadcastTarget = MinimumBroadcastTarget,
            BootstrapOptions = new BootstrapOptions
            {
                SeedPeers = PeerStrings
                    .Select(BoundPeer.ParsePeer)
                    .ToImmutableList(),
            },
            TimeoutOptions = new TimeoutOptions
            {
                MaxTimeout = TimeSpan.FromSeconds(50),
                GetBlockHashesTimeout = TimeSpan.FromSeconds(50),
                GetBlocksBaseTimeout = TimeSpan.FromSeconds(5),
            }
        };

    [Pure]
    public AppProtocolVersionOptions GetAppProtocolVersionOptions() =>
        new()
        {
            AppProtocolVersion = AppProtocolVersion is {} apv
                ? Libplanet.Net.AppProtocolVersion.FromToken(apv)
                : throw new MissingConfigurationFieldException(
                    nameof(AppProtocolVersion)
                ),
            TrustedAppProtocolVersionSigners =
                TrustedAppProtocolVersionSigners
                    ?.Select(hex => new PublicKey(ByteUtil.ParseHex(hex)))
                    ?.ToImmutableHashSet()
                ?? ImmutableHashSet<PublicKey>.Empty,
        };
}
