using System.Collections.Immutable;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Consensus;
using Libplanet.Crypto;
using Microsoft.Extensions.Hosting;

namespace Libplanet.Headless.Hosting;

public class MinerService<T> : BackgroundService, IDisposable
    where T : IAction, new()
{
    private readonly BlockChain<T> _blockChain;

    private readonly PrivateKey _privateKey;

    public MinerService(BlockChain<T> blockChain, PrivateKey minerPrivateKey)
    {
        _blockChain = blockChain;
        _privateKey = minerPrivateKey;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var block = _blockChain.ProposeBlock(
                _privateKey,
                lastCommit: _blockChain.GetBlockCommit(_blockChain.Tip.Hash));
            _blockChain.Append(
                block,
                new BlockCommit(
                    _blockChain.Count,
                    0,
                    block.Hash,
                    ImmutableArray<Vote>
                        .Empty
                        .Add(
                            new VoteMetadata(
                                _blockChain.Count,
                                0,
                                block.Hash,
                                DateTimeOffset.UtcNow,
                                _privateKey.PublicKey,
                                VoteFlag.PreCommit)
                                .Sign(_privateKey))));
            stoppingToken.ThrowIfCancellationRequested();
        }
    }
}
