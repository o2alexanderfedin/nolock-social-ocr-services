namespace Nolock.social.OCRservices.Pipelines;

public interface IPipelineNode<TIn, TOut>
{
    ValueTask<TOut> ProcessAsync(TIn input);
}