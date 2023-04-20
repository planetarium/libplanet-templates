using System.Collections.Immutable;
using System.Globalization;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Consensus;
using Libplanet.Crypto;
using Microsoft.Extensions.Hosting;

namespace Libplanet.Headless.Hosting;

public class SoloValidationService<T> : BackgroundService, IDisposable
    where T : IAction, new()
{
    private readonly BlockChain<T> _blockChain;

    private readonly PrivateKey _privateKey;

    private readonly PublicKey _publicKey;

    private readonly ValidatorDriverConfiguration _options;

    public SoloValidationService(
        BlockChain<T> blockChain,
        ValidatorDriverConfiguration options,
        ValidatorPrivateKey validatorPrivateKey)
    {
        _blockChain = blockChain;
        _privateKey = validatorPrivateKey.PrivateKey;
        _publicKey = _privateKey.PublicKey;
        _options = options;
        CheckValidator();
    }

    // TODO: Instead of proposing block right away, wait for the interval for the first time.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextProposal = DateTimeOffset.FromUnixTimeMilliseconds(0);
        while (!stoppingToken.IsCancellationRequested)
        {
            var timeToWait = nextProposal - DateTimeOffset.UtcNow;
            if (timeToWait > TimeSpan.Zero)
            {
                await Task.Delay(timeToWait, stoppingToken)
                    .ConfigureAwait(false);
            }
            CheckValidator();
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
            nextProposal = DateTimeOffset.UtcNow + _options.MinimumBlockInterval;
        }
    }

    private void CheckValidator()
    {
        var validators = _blockChain.GetValidatorSet();
        var requiredPower = validators.TotalPower * 2 / 3 + 1;
        Validator? validator = validators.GetValidator(_publicKey);
        string currentPowerString = validator is { } val
            ? val.Power.ToString(CultureInfo.InvariantCulture)
            : $"(does not exist in {nameof(ValidatorSet)})";
        if (validator is not { } validatorVal || requiredPower > validatorVal.Power)
        {
            throw new InvalidOperationException(
                $"For offline validation to work, the {nameof(PublicKey)}  of the validating"
                + $" {nameof(PrivateKey)} must exist in the {nameof(ValidatorSet)} with a power"
                + $" of over 2/3 of the total power. Current total power: {validators.TotalPower},"
                + $" validator public key: ({_privateKey.PublicKey}), "
                + $" Current power: {currentPowerString}, Required power: {requiredPower}");
        }
    }
}
