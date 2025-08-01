// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Nolock.social.OCRservices.Pipelines;

public sealed class PipelineNodeRelay<TIn, TOut> : IPipelineNode<TIn, TOut>
{
    private readonly Func<TIn, ValueTask<TOut>> _process;

    public PipelineNodeRelay(Func<TIn, ValueTask<TOut>> process)
    {
        ArgumentNullException.ThrowIfNull(process);
        _process = process;
    }

    public ValueTask<TOut> ProcessAsync(TIn input)
        => _process(input);
    
    public static implicit operator PipelineNodeRelay<TIn, TOut>(Func<TIn, ValueTask<TOut>> process)
        => new(process);
    
    public static PipelineNodeRelay<TIn, TOut> FromFunc(Func<TIn, ValueTask<TOut>> process)
        => new(process);
}

public static class PipelineNodeRelay
{
    public static PipelineNodeRelay<TIn, TOut> Create<TIn, TOut>(Func<TIn, ValueTask<TOut>> process) => new(process);
}