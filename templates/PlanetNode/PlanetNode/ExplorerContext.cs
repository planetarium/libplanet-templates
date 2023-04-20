namespace PlanetNode;

using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Explorer.Indexing;
using Libplanet.Explorer.Interfaces;
using Libplanet.Net;
using Libplanet.Store;
using PlanetNode.Action;

public class ExplorerContext : IBlockChainContext<PolymorphicAction<BaseAction>>
{
    public ExplorerContext(
        BlockChain<PolymorphicAction<BaseAction>> blockChain,
        IStore store,
        Swarm<PolymorphicAction<BaseAction>>? swarm = null,
        IBlockChainIndex? index = null
    )
    {
        BlockChain = blockChain;
        Store = store;
        Swarm = swarm;
        Index = index;
    }

    public bool Preloaded => Swarm?.Running ?? true;

    public BlockChain<PolymorphicAction<BaseAction>> BlockChain { get; private set; }

    public IStore Store { get; private set; }

    public Swarm<PolymorphicAction<BaseAction>>? Swarm { get; private set; }

    public IBlockChainIndex? Index { get; private set; }
}
