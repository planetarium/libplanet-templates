namespace PlanetNode.GraphTypes;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Action.Sys;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Net;
using Libplanet.Tx;
using PlanetNode.Action;

public class Mutation : ObjectGraphType
{
    [SuppressMessage(
        "StyleCop.CSharp.ReadabilityRules",
        "SA1118:ParameterMustNotSpanMultipleLines",
        Justification = "GraphQL docs require long lines of text.")]
    public Mutation(
        BlockChain<PolymorphicAction<BaseAction>> blockChain,
        Swarm<PolymorphicAction<BaseAction>>? swarm = null
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
                swarm?.BroadcastTxs(new[] { tx });
                return tx;
            }
        );

        // TODO: This mutation should be upstreamed to Libplanet.Explorer so that any native tokens
        // can work together with this mutation:
        Field<TransactionType<PolymorphicAction<BaseAction>>>(
            "transferAsset",
            description: "Transfers the given amount of PNG from the account of the specified " +
                "privateKeyHex to the specified recipient.  The transaction is signed using " +
                "the privateKeyHex and added to the stage (and eventually included in one of " +
                "the next blocks).",
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
                swarm?.BroadcastTxs(new[] { tx });
                return tx;
            }
        );

        // TODO: This mutation should be upstreamed to Libplanet.Explorer so that any native tokens
        // can work together with this mutation:
        Field<TransactionType<PolymorphicAction<BaseAction>>>(
            "mintAsset",
            description: "Mints the given amount of PNG to the balance of the specified " +
                "recipient. The transaction is signed using the privateKeyHex and added to " +
                "the stage (and eventually included in one of the next blocks).",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>>
                {
                    Name = "recipient",
                    Description = "The recipient's 40-hex address.",
                },
                new QueryArgument<NonNullGraphType<StringGraphType>>
                {
                    Name = "amount",
                    Description = "The amount to mint in PNG.",
                },
                new QueryArgument<NonNullGraphType<StringGraphType>>
                {
                    Name = "privateKeyHex",
                    Description = "A hex-encoded private key of the minter.  A made " +
                        "transaction will be signed using this key.",
                }
            ),
            resolve: context =>
            {
                Address recipient = new Address(context.GetArgument<string>("recipient"));
                string amount = context.GetArgument<string>("amount");
                string privateKeyHex = context.GetArgument<string>("privateKeyHex");

                PrivateKey privateKey = PrivateKey.FromString(privateKeyHex);
                var action = new Mint(
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
                swarm?.BroadcastTxs(new[] { tx });
                return tx;
            }
        );
    }
}
