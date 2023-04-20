namespace Libplanet.Headless.Hosting;

using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Net;



public record InstantiatedNodeComponents<T>(
    IStore Store,
    IStateStore StateStore,
    BlockChain<T> BlockChain,
    Swarm<T>? Swarm,
    SwarmService<T>.BootstrapMode? BootstrapMode,
    PrivateKey? ValidatorPrivateKey,
    ValidatorDriverConfiguration ValidatorDriverConfiguration
) where T : IAction, new();
