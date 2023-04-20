namespace PlanetNode.GraphTypes;

using System.Diagnostics.CodeAnalysis;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Net;
using PlanetNode.Action;

public class Query : ObjectGraphType
{
    [SuppressMessage(
        "StyleCop.CSharp.ReadabilityRules",
        "SA1118:ParameterMustNotSpanMultipleLines",
        Justification = "GraphQL docs require long lines of text.")]
    public Query(
        BlockChain<PolymorphicAction<BaseAction>> blockChain,
        Swarm<PolymorphicAction<BaseAction>>? swarm = null)
    {
        Field<StringGraphType>(
            "asset",
            description: "The specified address's balance in PNG.",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>>
                {
                    Name = "address",
                    Description = "The account holder's 40-hex address",
                }
            ),
            resolve: context =>
            {
                var accountAddress = new Address(context.GetArgument<string>("address"));
                FungibleAssetValue asset = blockChain.GetBalance(
                    accountAddress,
                    Currencies.KeyCurrency
                );

                return asset.ToString();
            }
        );

        // TODO: Move to Libplanet.Explorer or Node API.
        Field<StringGraphType>(
            "peerString",
            resolve: context =>
                swarm is null
                ? throw new InvalidOperationException("Network settings is not set.")
                : swarm.AsPeer.PeerString);
    }
}
