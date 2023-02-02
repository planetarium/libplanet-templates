namespace Libplanet.Headless.Hosting;

using System.Collections.Immutable;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Net;

public interface ILibplanetBuilder<T>
    where T : IAction, new()
{
    ILibplanetBuilder<T> UseConfiguration(Configuration configuration);

    ILibplanetBuilder<T> UseBlockPolicy(IBlockPolicy<T> blockPolicy);

    ILibplanetBuilder<T> OnDifferentAppProtocolVersionEncountered(
        DifferentAppProtocolVersionEncountered differentApvEncountered);

    ILibplanetBuilder<T> UseNativeTokens(IImmutableSet<Currency> nativeTokens);

    ILibplanetBuilder<T> UseMiner(PrivateKey privateKey);

    InstantiatedNodeComponents<T> Build();
}
