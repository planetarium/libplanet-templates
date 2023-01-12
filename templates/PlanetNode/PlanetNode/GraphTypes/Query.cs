using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Net;
using PlanetNode.Action;
using System.Diagnostics.CodeAnalysis;

namespace PlanetNode.GraphTypes;

public class Query : ObjectGraphType
{
    [SuppressMessage(
        "StyleCop.CSharp.ReadabilityRules",
        "SA1118:ParameterMustNotSpanMultipleLines",
        Justification = "GraphQL docs require long lines of text.")]
    public Query(
        BlockChain<PolymorphicAction<BaseAction>> blockChain,
        Swarm<PolymorphicAction<BaseAction>> swarm)
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
            {
                var peer = swarm.AsPeer;
                var peerString = GetPeerString(peer);

                return peerString;
            }
        );
    }

    private static string GetPeerString(BoundPeer peer)
    {
        var pubKey = peer.PublicKey.ToString();
        var hostAndPort = peer.ToString().Split('/')[1];
        var host = hostAndPort.Split(':')[0];
        var port = hostAndPort.Split(':')[1];
        return $"{pubKey},{host},{port}";
    }
}
