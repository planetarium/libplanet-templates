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
using Libplanet.Net.Consensus;
using Libplanet.Net.Transports;
using Libplanet.Store;
using Serilog;

internal sealed class LibplanetBuilder<T> : ILibplanetBuilder<T>
    where T : IAction, new()
{
    private Configuration _configuration = new Configuration();
    private IBlockPolicy<T>? _blockPolicy;
    private DifferentAppProtocolVersionEncountered? _differentApvEncountered;
    private IImmutableSet<Currency>? _nativeTokens;
    private PrivateKey? _validatorPrivateKey;

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

    public ILibplanetBuilder<T> UseValidator(PrivateKey privateKey)
    {
        _validatorPrivateKey = privateKey;
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
        var stagePolicy = new VolatileStagePolicy<T>(_configuration.TxLifetime);
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

        if (_configuration.Network is not { } netConfig)
        {
            return new InstantiatedNodeComponents<T>(
                store,
                stateStore,
                blockChain,
                null,
                null,
                _validatorPrivateKey,
                _configuration.ValidatorDriver);
        }

        var apvOptions = netConfig.GetAppProtocolVersionOptions();
        if (_differentApvEncountered is not null)
        {
            apvOptions.DifferentAppProtocolVersionEncountered = _differentApvEncountered;
        }

        var random = new Random();

        var swarmOptions = netConfig.GetSwarmOptions();

        // TODO: Swarm private key should be configurable:
        var transport = NetMQTransport.Create(
            new PrivateKey(),
            apvOptions,
            netConfig.GetHostOptions(),
            swarmOptions.MessageTimestampBuffer)
            .ConfigureAwait(false).GetAwaiter().GetResult();

        NetMQTransport? consensusTransport = null;
        ConsensusReactorOption? consensusReactorOption = null;
        if (_validatorPrivateKey is { } pk)
        {
            if (netConfig.ConsensusHost is null)
            {
                throw new Libplanet.Headless.MissingConfigurationFieldException(
                    "Consensus host must be set when the node is a validator"
                    + " participating in a consensus.",
                    nameof(netConfig.ConsensusHost)
                );
            }

            if (netConfig.ConsensusPort == netConfig.Port)
            {
                throw new Libplanet.Headless.ConflictingConfigurationException(
                    "Consensus port and port must be different.",
                    nameof(netConfig.ConsensusPort),
                    nameof(netConfig.Port)
                );
            }

            consensusTransport =
                NetMQTransport.Create(
                        pk,
                        apvOptions,
                        netConfig.GetConsensusHostOptions(),
                        swarmOptions.MessageTimestampBuffer)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            consensusReactorOption = new ConsensusReactorOption
            {
                SeedPeers = ImmutableList<BoundPeer>.Empty,
                ConsensusPeers = ImmutableList<BoundPeer>.Empty,
                ConsensusPort = 0,
                ConsensusPrivateKey = pk,
                ConsensusWorkers = 100,
                TargetBlockInterval =
                    TimeSpan.FromSeconds(
                        _configuration.ValidatorDriver.MinimumBlockIntervalSecs),
            };
        }

        var swarm = new Swarm<T>(
            blockChain,
            new PrivateKey(),
            transport,
            swarmOptions,
            consensusTransport,
            consensusReactorOption);
        var bootstrapMode = netConfig.PeerStrings.Any() || netConfig.StaticPeerStrings.Any()
            ? SwarmService<T>.BootstrapMode.Participant
            : SwarmService<T>.BootstrapMode.Seed;

        return new InstantiatedNodeComponents<T>(
            store,
            stateStore,
            blockChain,
            swarm,
            bootstrapMode,
            _validatorPrivateKey,
            _configuration.ValidatorDriver);
    }
}
