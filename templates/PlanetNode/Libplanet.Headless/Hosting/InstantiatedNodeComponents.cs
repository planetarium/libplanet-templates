namespace Libplanet.Headless.Hosting;

using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Store;

public readonly record struct InstantiatedNodeComponents<T>
    where T : IAction, new()
{
    public IStore Store { get; init; }
    public IStateStore StateStore { get; init; }
    public BlockChain<T> BlockChain { get; init; }
    public Swarm<T> Swarm { get; init; }
    public PrivateKey? MinerPrivateKey { get; init; }
}
