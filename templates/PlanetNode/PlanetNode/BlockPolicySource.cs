namespace PlanetNode;

using System.Collections.Immutable;
using Libplanet.Action;
using Libplanet.Blockchain.Policies;
using PlanetNode.Action;

public static class BlockPolicySource
{
    public static BlockPolicy<PolymorphicAction<BaseAction>> GetPolicy()
    {
        return new BlockPolicy<PolymorphicAction<BaseAction>>(
            nativeTokens: ImmutableHashSet.Create(Currencies.KeyCurrency)
        );
    }
}
