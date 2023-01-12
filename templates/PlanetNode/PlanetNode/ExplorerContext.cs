using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Explorer.Interfaces;
using Libplanet.Net;
using Libplanet.Store;
using PlanetNode.Action;

namespace PlanetNode;

public class ExplorerContext : IBlockChainContext<PolymorphicAction<BaseAction>>
{
    private readonly Swarm<PolymorphicAction<BaseAction>> _swarm;

    public ExplorerContext(
        BlockChain<PolymorphicAction<BaseAction>> blockChain,
        IStore store,
        Swarm<PolymorphicAction<BaseAction>> swarm
    )
    {
        BlockChain = blockChain;
        Store = store;
        _swarm = swarm;
    }

    public bool Preloaded => _swarm.Running;

    public BlockChain<PolymorphicAction<BaseAction>> BlockChain { get; private set; }

    public IStore Store { get; private set; }
}
