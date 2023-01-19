using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Action.Sys;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Explorer.Mutations;
using Libplanet.Net;
using Libplanet.Tx;
using PlanetNode.Action;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace PlanetNode.GraphTypes;

public class Mutation : ObjectGraphType
{
    [SuppressMessage(
        "StyleCop.CSharp.ReadabilityRules",
        "SA1118:ParameterMustNotSpanMultipleLines",
        Justification = "GraphQL docs require long lines of text.")]
    public Mutation(
        Swarm<PolymorphicAction<BaseAction>> swarm,
        BlockChain<PolymorphicAction<BaseAction>> blockChain
    )
    {
        Field<TransactionType<PolymorphicAction<BaseAction>>>(
            "stage",
            description: "Stage transaction to current chain",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<ByteStringType>>
                {
                    Name = "payloadHex",
                    Description = "The hexadecimal string of the serialized transaction to stage.",
                }
            ),
            resolve: context =>
            {
                string payloadHex = context.GetArgument<string>("payloadHex");
                byte[] payload = ByteUtil.ParseHex(payloadHex);
                var tx = Transaction<PolymorphicAction<BaseAction>>.Deserialize(payload);
                blockChain.StageTransaction(tx);
                swarm.BroadcastTxs(new[] { tx });
                return tx;
            }
        );

        // TODO: This mutation should be upstreamed to Libplanet.Explorer so that any native tokens
        // can work together with this mutation:
        Field<TransactionType<PolymorphicAction<BaseAction>>>(
            "transferAsset",
            description: "Transfers the given amount of PNG from the account of the specified " +
                "privateKeyHex to the specified recipient.  A made transaction is signed using " +
                "the privateKeyHex and added to the pending list (and eventually included in " +
                "one of the next blocks).",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>>
                {
                    Name = "recipient",
                    Description = "The recipient's 40-hex address.",
                },
                new QueryArgument<NonNullGraphType<StringGraphType>>
                {
                    Name = "amount",
                    Description = "The amount to transfer in PNG.",
                },
                new QueryArgument<NonNullGraphType<StringGraphType>>
                {
                    Name = "privateKeyHex",
                    Description = "A hex-encoded private key of the sender.  A made " +
                        "transaction will be signed using this key.",
                }
            ),
            resolve: context =>
            {
                Address recipient = new Address(context.GetArgument<string>("recipient"));
                string amount = context.GetArgument<string>("amount");
                string privateKeyHex = context.GetArgument<string>("privateKeyHex");

                PrivateKey privateKey = PrivateKey.FromString(privateKeyHex);
                var action = new Transfer(
                    recipient,
                    FungibleAssetValue.Parse(
                        Currencies.KeyCurrency,
                        amount
                    )
                );

                var tx = blockChain.MakeTransaction(
                    privateKey,
                    action,
                    ImmutableHashSet<Address>.Empty
                        .Add(privateKey.ToAddress())
                        .Add(recipient));
                swarm.BroadcastTxs(new[] { tx });
                return tx;
            }
        );
    }
}
