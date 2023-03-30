namespace PlanetNode;

using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Explorer.Interfaces;
using Libplanet.Net;
using Libplanet.Store;
using PlanetNode.Action;

public class ExplorerContext : IBlockChainContext<PolymorphicAction<BaseAction>>
{
    public ExplorerContext(
        BlockChain<PolymorphicAction<BaseAction>> blockChain,
        IStore store,
        Swarm<PolymorphicAction<BaseAction>> swarm
    )
    {
        BlockChain = blockChain;
        Store = store;
        Swarm = swarm;
    }

    public bool Preloaded => Swarm.Running;

    public BlockChain<PolymorphicAction<BaseAction>> BlockChain { get; private set; }

    public IStore Store { get; private set; }

    public Swarm<PolymorphicAction<BaseAction>> Swarm { get; private set; }
}
