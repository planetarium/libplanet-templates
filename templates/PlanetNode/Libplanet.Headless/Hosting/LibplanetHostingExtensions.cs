namespace Libplanet.Headless.Hosting;

using System.Collections.Immutable;
using Bencodex;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Store;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

public static class LibplanetServicesExtensions
{
    public static IServiceCollection AddLibplanet<T>(
        this IServiceCollection services,
        Configuration configuration,
        IImmutableSet<Currency> nativeTokens,
        DifferentAppProtocolVersionEncountered? differentApvEncountered = null
    )
        where T : IAction, new()
    {
        var blockPolicy = new BlockPolicy<T>(nativeTokens: nativeTokens);
        return services.AddLibplanet(
            configuration,
            blockPolicy,
            differentApvEncountered
        );
    }

    // TODO: Make AddLibplanet() similar to AddGraphQL taking a Func<builder>
    public static IServiceCollection AddLibplanet<T>(
        this IServiceCollection services,
        Configuration configuration,
        IBlockPolicy<T> blockPolicy,
        DifferentAppProtocolVersionEncountered? differentApvEncountered = null
    )
        where T : IAction, new()
    {
        Block<T> genesisBlock;
        if (configuration.GenesisBlockPath is not { } genesisUri)
        {
            throw new ArgumentException(
                $"The {nameof(configuration.GenesisBlockPath)} not specified.",
                nameof(configuration)
            );
        }

        var codec = new Codec();
        IValue serializedGenesis;
        switch (genesisUri.Scheme)
        {
            case "file":
                Log.Debug("Loading genesis block from {GenesisUri}...", genesisUri);
                using (var fileStream = File.OpenRead(genesisUri.LocalPath))
                {
                    serializedGenesis = codec.Decode(fileStream);
                }
                break;

            case "http":
            case "https":
                Log.Debug("Downloading genesis block from {GenesisUri}...", genesisUri);
                using (var handler = new HttpClientHandler { AllowAutoRedirect = true })
                using (var client = new HttpClient(handler))
                using (var request = new HttpRequestMessage(HttpMethod.Get, genesisUri))
                using (var response = client.Send(request))
                {
                    response.EnsureSuccessStatusCode();
                    using Stream stream = response.Content.ReadAsStream();
                    serializedGenesis = codec.Decode(stream);
                }
                break;

            default:
                throw new NotSupportedException(
                    $"Unsupported scheme ({nameof(configuration.GenesisBlockPath)}): " +
                    genesisUri.Scheme
                );
        }

        genesisBlock = BlockMarshaler.UnmarshalBlock<T>(
            (Bencodex.Types.Dictionary)serializedGenesis);

        services.AddSingleton<IBlockPolicy<T>>(blockPolicy);
        // TODO: Make it configurable:
        services.AddSingleton<IStagePolicy<T>>(_ =>
            new VolatileStagePolicy<T>(
                TimeSpan.FromMinutes(configuration.TxLifetimeMins)
            )
        );

        if (configuration.StoreUri is not {} storeUri)
        {
            throw new ArgumentException(
                $"{nameof(configuration.StoreUri)} is required.",
                nameof(configuration)
            );
        }

        Log.Information("Loading store from {StoreUri}...", storeUri);
        var (store, stateStore) = LoadStore(storeUri);
        services.AddSingleton<IStore>(store);
        services.AddSingleton<IStateStore>(stateStore);
        services.AddSingleton<BlockChain<T>>(provider =>
        {
            return new BlockChain<T>(
                provider.GetRequiredService<IBlockPolicy<T>>(),
                provider.GetRequiredService<IStagePolicy<T>>(),
                provider.GetRequiredService<IStore>(),
                provider.GetRequiredService<IStateStore>(),
                genesisBlock
            );
        });
        services.AddSingleton<Swarm<T>>(provider =>
        {
            var peers = configuration.PeerStrings.Select(BoundPeer.ParsePeer).ToImmutableList();
            var options = new SwarmOptions
            {
                StaticPeers = configuration.StaticPeerStrings
                    .Select(BoundPeer.ParsePeer)
                    .ToImmutableHashSet(),
                BucketSize = configuration.BucketSize,
                MinimumBroadcastTarget = configuration.MinimumBroadcastTarget,
                BootstrapOptions = new BootstrapOptions
                {
                    SeedPeers = peers,
                },
                TimeoutOptions = new TimeoutOptions
                {
                    MaxTimeout = TimeSpan.FromSeconds(50),
                    GetBlockHashesTimeout = TimeSpan.FromSeconds(50),
                    GetBlocksBaseTimeout = TimeSpan.FromSeconds(5),
                }
            };
            // TODO: Swarm private key should be configurable:
            var random = new Random();
            return new Swarm<T>(
                provider.GetRequiredService<BlockChain<T>>(),
                new PrivateKey(),
                configuration.AppProtocolVersion is {} apv
                    ? AppProtocolVersion.FromToken(apv)
                    : default,
                host: configuration.Host,
                listenPort: configuration.Port,
                iceServers: configuration.IceServerUris
                    .Select(uri => new IceServer(uri))
                    .OrderBy(_ => random.Next()),
                trustedAppProtocolVersionSigners:
                    configuration.TrustedAppProtocolVersionSigners
                        ?.Select(hex => new PublicKey(ByteUtil.ParseHex(hex))),
                differentAppProtocolVersionEncountered: differentApvEncountered,
                options: options
            );
        });
        services.AddSingleton(_ => configuration);

        services.AddHostedService<SwarmService<T>>();

        if (configuration.MinerPrivateKeyString is { } minerPrivateKey)
        {
            services.AddHostedService(provider =>
                new MinerService<T>(
                    provider.GetRequiredService<BlockChain<T>>(),
                    PrivateKey.FromString(minerPrivateKey)
                )
            );
        }

        return services;
    }

    private static (IStore, IStateStore) LoadStore(Uri storeUri)
    {
#pragma warning disable CS0219
        // Workaround to reference RocksDBStore.dll:
        RocksDBStore.RocksDBStore? _ = null;
#pragma warning restore CS0219

        (IStore, IStateStore)? pair = StoreLoaderAttribute.LoadStore(storeUri);
        if (pair is {} found)
        {
            return found;
        }

        string supported = string.Join(
            "\n",
            StoreLoaderAttribute.ListStoreLoaders().Select(pair =>
                $"  {pair.UriScheme}: {pair.DeclaringType.FullName}"));
        throw new TypeLoadException(
            $"Store type {storeUri.Scheme} is not supported; supported types " +
            $"are:\n\n{supported}"
        );
    }
}
