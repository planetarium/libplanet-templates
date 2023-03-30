using Libplanet.Net.Transports;

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
using Serilog;

internal sealed class LibplanetBuilder<T> : ILibplanetBuilder<T>
    where T : IAction, new()
{
    private Configuration _configuration = new Configuration();
    private IBlockPolicy<T>? _blockPolicy;
    private DifferentAppProtocolVersionEncountered? _differentApvEncountered;
    private IImmutableSet<Currency>? _nativeTokens;
    private PrivateKey? _minerPrivateKey;

    public ILibplanetBuilder<T> UseConfiguration(Configuration configuration)
    {
        _configuration = configuration;
        return this;
    }

    public ILibplanetBuilder<T> UseBlockPolicy(IBlockPolicy<T> blockPolicy)
    {
        if (_nativeTokens is not null)
        {
            throw new InvalidOperationException(
                $"{nameof(UseNativeTokens)}() and {nameof(UseBlockPolicy)}() cannot be " +
                "configured at the same time."
            );
        }

        _blockPolicy = blockPolicy;
        return this;
    }

    public ILibplanetBuilder<T> OnDifferentAppProtocolVersionEncountered(
        DifferentAppProtocolVersionEncountered differentApvEncountered)
    {
        _differentApvEncountered = differentApvEncountered;
        return this;
    }

    public ILibplanetBuilder<T> UseNativeTokens(IImmutableSet<Currency> nativeTokens)
    {
        if (_blockPolicy is not null)
        {
            throw new InvalidOperationException(
                $"{nameof(UseNativeTokens)}() and {nameof(UseBlockPolicy)}() cannot be " +
                "configured at the same time."
            );
        }

        _nativeTokens = nativeTokens;
        return this;
    }

    public ILibplanetBuilder<T> UseMiner(PrivateKey privateKey)
    {
        _minerPrivateKey = privateKey;
        return this;
    }

    private Block<T> GetGenesisBlock()
    {
        if (_configuration.GenesisBlockPath is not { } genesisUri)
        {
            throw new MissingConfigurationFieldException(
                nameof(_configuration.GenesisBlockPath)
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
                    $"Unsupported scheme ({nameof(_configuration.GenesisBlockPath)}): " +
                    genesisUri.Scheme
                );
        }

        return BlockMarshaler.UnmarshalBlock<T>(
            (Bencodex.Types.Dictionary)serializedGenesis);
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

    private SwarmOptions GetSwarmOptions()
    {
        var peers = _configuration.PeerStrings
            .Select(BoundPeer.ParsePeer)
            .ToImmutableList();
        return new SwarmOptions
        {
            StaticPeers = _configuration.StaticPeerStrings
                .Select(BoundPeer.ParsePeer)
                .ToImmutableHashSet(),
            BucketSize = _configuration.BucketSize,
            MinimumBroadcastTarget = _configuration.MinimumBroadcastTarget,
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
    }

    public InstantiatedNodeComponents<T> Build()
    {
        if (_configuration.StoreUri is not {} storeUri)
        {
            throw new MissingConfigurationFieldException(
                nameof(_configuration.StoreUri)
            );
        }

        (IStore store, IStateStore stateStore) = LoadStore(storeUri);
        var blockPolicy = _blockPolicy ?? new BlockPolicy<T>(
            nativeTokens: _nativeTokens);
        var stagePolicy = new VolatileStagePolicy<T>(
            TimeSpan.FromMinutes(_configuration.TxLifetimeMins)
        );
        var genesis = GetGenesisBlock();
        var blockChain = store.GetCanonicalChainId() is not null
            ? new BlockChain<T>(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesis)
            : BlockChain<T>.Create(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesis);
        var apvOptions = new AppProtocolVersionOptions
        {
            AppProtocolVersion = _configuration.AppProtocolVersion is {} apv
                ? AppProtocolVersion.FromToken(apv)
                : throw new MissingConfigurationFieldException(
                    nameof(_configuration.AppProtocolVersion)
                ),
            TrustedAppProtocolVersionSigners =
                _configuration.TrustedAppProtocolVersionSigners
                    ?.Select(hex => new PublicKey(ByteUtil.ParseHex(hex)))
                    ?.ToImmutableHashSet(),
        };
        if (_differentApvEncountered is not null)
        {
            apvOptions.DifferentAppProtocolVersionEncountered = _differentApvEncountered;
        }

        var random = new Random();
        var hostOptions = new HostOptions(
            host: _configuration.Host,
            iceServers: _configuration.IceServerUris
                .Select(uri => new IceServer(uri))
                .OrderBy(_ => random.Next()),
            port: _configuration.Port
        );

        var swarmOptions = GetSwarmOptions();

        // TODO: Swarm private key should be configurable:
        var transport = NetMQTransport.Create(
            new PrivateKey(),
            apvOptions,
            hostOptions,
            swarmOptions.MessageTimestampBuffer)
            .ConfigureAwait(false).GetAwaiter().GetResult();

        var swarm = new Swarm<T>(
            blockChain,
            new PrivateKey(),
            transport,
            GetSwarmOptions());

        return new InstantiatedNodeComponents<T>()
        {
            Store = store,
            StateStore = stateStore,
            BlockChain = blockChain,
            Swarm = swarm,
            MinerPrivateKey = _minerPrivateKey,
            SwarmMode = _configuration.PeerStrings.Any() || _configuration.StaticPeerStrings.Any()
                ? SwarmService<T>.Mode.Node
                : SwarmService<T>.Mode.StandaloneNode,
        };
    }
}
