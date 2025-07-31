using System.Net;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Nolock.social.OCRservices.Pipelines;

public sealed class PipelineNodeRelay<TIn, TOut>(Func<TIn, ValueTask<TOut>> process)
    : IPipelineNode<TIn, TOut>
{
    public ValueTask<TOut> ProcessAsync(TIn input)
        => process?.Invoke(input) ?? throw new ArgumentNullException(nameof(process))
        {
            HelpLink = null,
            HResult = (int)HttpStatusCode.ExpectationFailed,
            Source = $"{GetType().FullName}.{nameof(ProcessAsync)}"
        };
    
    public static implicit operator PipelineNodeRelay<TIn, TOut>(Func<TIn, ValueTask<TOut>> process)
        => new(process);
}

public static class PipelineNodeRelay
{
    public static IPipelineNode<TIn, TOut> Create<TIn, TOut>(Func<TIn, ValueTask<TOut>> process)
        => new PipelineNodeRelay<TIn, TOut>(process);
}