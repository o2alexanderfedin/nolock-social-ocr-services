namespace Nolock.social.OCRservices.Core.Pipelines;

public interface IPipelineNode<TIn, TOut>
{
    ValueTask<TOut> ProcessAsync(TIn input);
}