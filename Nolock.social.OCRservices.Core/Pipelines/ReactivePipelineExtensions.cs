using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Concurrency;

namespace Nolock.social.OCRservices.Core.Pipelines;

/// <summary>
/// Reactive extensions for pipeline nodes to enable Rx.NET-based processing
/// </summary>
public static class ReactivePipelineExtensions
{
    /// <summary>
    /// Converts a pipeline node to an observable operator
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="node">The pipeline node to convert</param>
    /// <returns>Observable operator function</returns>
    public static Func<IObservable<TIn>, IObservable<TOut>> ToObservableOperator<TIn, TOut>(
        this IPipelineNode<TIn, TOut> node)
    {
        return source => source.SelectMany(async item => await node.ProcessAsync(item));
    }

    /// <summary>
    /// Converts a pipeline node to an observable operator with error handling
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="node">The pipeline node to convert</param>
    /// <param name="errorHandler">Function to handle errors</param>
    /// <returns>Observable operator function</returns>
    public static Func<IObservable<TIn>, IObservable<TOut>> ToObservableOperator<TIn, TOut>(
        this IPipelineNode<TIn, TOut> node,
        Func<Exception, TIn, IObservable<TOut>> errorHandler)
    {
        return source => source.SelectMany(async item =>
        {
            try
            {
                return await node.ProcessAsync(item);
            }
            catch (InvalidOperationException ex)
            {
                return await errorHandler(ex, item).FirstAsync();
            }
            catch (NotSupportedException ex)
            {
                return await errorHandler(ex, item).FirstAsync();
            }
            catch (ArgumentException ex)
            {
                return await errorHandler(ex, item).FirstAsync();
            }
        });
    }

    /// <summary>
    /// Creates a reactive pipeline from a sequence of pipeline nodes
    /// </summary>
    /// <typeparam name="T">The data type flowing through the pipeline</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="nodes">Pipeline nodes to chain</param>
    /// <returns>Observable with chained pipeline processing</returns>
    public static IObservable<T> ThroughPipeline<T>(
        this IObservable<T> source,
        params IPipelineNode<T, T>[] nodes)
    {
        return nodes.Aggregate(source, (current, node) => current.SelectMany(async item => await node.ProcessAsync(item)));
    }

    /// <summary>
    /// Creates a reactive pipeline with different input/output types
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="node">Pipeline node</param>
    /// <returns>Observable with transformed output</returns>
    public static IObservable<TOut> Through<TIn, TOut>(
        this IObservable<TIn> source,
        IPipelineNode<TIn, TOut> node)
    {
        return source.SelectMany(async item => await node.ProcessAsync(item));
    }

    /// <summary>
    /// Creates a reactive pipeline with backpressure handling
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="node">Pipeline node</param>
    /// <param name="maxConcurrency">Maximum concurrent operations</param>
    /// <returns>Observable with backpressure control</returns>
    public static IObservable<TOut> ThroughWithBackpressure<TIn, TOut>(
        this IObservable<TIn> source,
        IPipelineNode<TIn, TOut> node,
        int maxConcurrency = 4)
    {
        return source
            .Select(item => Observable.FromAsync(() => node.ProcessAsync(item).AsTask()))
            .Merge(maxConcurrency);
    }

    /// <summary>
    /// Creates a reactive pipeline with retry capability
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="node">Pipeline node</param>
    /// <param name="retryCount">Number of retries</param>
    /// <returns>Observable with retry logic</returns>
    public static IObservable<TOut> ThroughWithRetry<TIn, TOut>(
        this IObservable<TIn> source,
        IPipelineNode<TIn, TOut> node,
        int retryCount = 3)
    {
        return source.SelectMany(item =>
            Observable.FromAsync(() => node.ProcessAsync(item).AsTask())
                .Retry(retryCount));
    }

    /// <summary>
    /// Creates a reactive pipeline with timeout
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="node">Pipeline node</param>
    /// <param name="timeout">Timeout duration</param>
    /// <param name="scheduler">Scheduler for timeout</param>
    /// <returns>Observable with timeout handling</returns>
    public static IObservable<TOut> ThroughWithTimeout<TIn, TOut>(
        this IObservable<TIn> source,
        IPipelineNode<TIn, TOut> node,
        TimeSpan timeout,
        IScheduler? scheduler = null)
    {
        scheduler ??= DefaultScheduler.Instance;
        return source.SelectMany(item =>
            Observable.FromAsync(() => node.ProcessAsync(item).AsTask())
                .Timeout(timeout, scheduler));
    }

    /// <summary>
    /// Creates a buffered pipeline that processes items in batches
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="node">Pipeline node</param>
    /// <param name="bufferSize">Buffer size</param>
    /// <param name="bufferTimeSpan">Buffer time span</param>
    /// <returns>Observable with buffered processing</returns>
    public static IObservable<TOut> ThroughBuffered<TIn, TOut>(
        this IObservable<TIn> source,
        IPipelineNode<TIn, TOut> node,
        int bufferSize,
        TimeSpan? bufferTimeSpan = null)
    {
        var buffered = bufferTimeSpan.HasValue
            ? source.Buffer(bufferTimeSpan.Value, bufferSize)
            : source.Buffer(bufferSize);

        return buffered.SelectMany(batch =>
            batch.ToObservable().SelectMany(async item => await node.ProcessAsync(item)));
    }

    /// <summary>
    /// Creates a pipeline with error recovery
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="node">Pipeline node</param>
    /// <param name="fallbackValue">Fallback value on error</param>
    /// <returns>Observable with error recovery</returns>
    public static IObservable<TOut> ThroughWithFallback<TIn, TOut>(
        this IObservable<TIn> source,
        IPipelineNode<TIn, TOut> node,
        TOut fallbackValue)
    {
        return source.SelectMany(item =>
            Observable.FromAsync(() => node.ProcessAsync(item).AsTask())
                .Catch<TOut, Exception>(_ => Observable.Return(fallbackValue)));
    }

    /// <summary>
    /// Creates a pipeline with progress tracking
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="node">Pipeline node</param>
    /// <param name="progressSubject">Subject to report progress</param>
    /// <returns>Observable with progress tracking</returns>
    public static IObservable<TOut> ThroughWithProgress<TIn, TOut>(
        this IObservable<TIn> source,
        IPipelineNode<TIn, TOut> node,
        ISubject<PipelineProgress<TIn>> progressSubject)
    {
        return source.SelectMany(async item =>
        {
            progressSubject.OnNext(new PipelineProgress<TIn>(item, PipelineProgressStatus.Started));
            try
            {
                var result = await node.ProcessAsync(item);
                progressSubject.OnNext(new PipelineProgress<TIn>(item, PipelineProgressStatus.Completed));
                return result;
            }
            catch (Exception ex)
            {
                progressSubject.OnNext(new PipelineProgress<TIn>(item, PipelineProgressStatus.Failed, ex));
                throw;
            }
        });
    }
}

/// <summary>
/// Represents progress in a pipeline operation
/// </summary>
/// <typeparam name="T">The type of item being processed</typeparam>
public class PipelineProgress<T>
{
    public PipelineProgress(T item, PipelineProgressStatus status, Exception? error = null)
    {
        Item = item;
        Status = status;
        Error = error;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public T Item { get; }
    public PipelineProgressStatus Status { get; }
    public Exception? Error { get; }
    public DateTimeOffset Timestamp { get; }
}

/// <summary>
/// Status of a pipeline operation
/// </summary>
public enum PipelineProgressStatus
{
    Started,
    InProgress,
    Completed,
    Failed
}

/// <summary>
/// Advanced reactive pipeline builder
/// </summary>
public class ReactivePipelineBuilder<T>
{
    private IObservable<T> _source;

    public ReactivePipelineBuilder(IObservable<T> source)
    {
        _source = source;
    }

    public ReactivePipelineBuilder<TOut> Through<TOut>(IPipelineNode<T, TOut> node)
    {
        var newSource = _source.Through(node);
        return new ReactivePipelineBuilder<TOut>(newSource);
    }

    public ReactivePipelineBuilder<TOut> ThroughWithBackpressure<TOut>(
        IPipelineNode<T, TOut> node,
        int maxConcurrency = 4)
    {
        var newSource = _source.ThroughWithBackpressure(node, maxConcurrency);
        return new ReactivePipelineBuilder<TOut>(newSource);
    }

    public ReactivePipelineBuilder<TOut> ThroughWithRetry<TOut>(
        IPipelineNode<T, TOut> node,
        int retryCount = 3)
    {
        var newSource = _source.ThroughWithRetry(node, retryCount);
        return new ReactivePipelineBuilder<TOut>(newSource);
    }

    public ReactivePipelineBuilder<TOut> ThroughWithTimeout<TOut>(
        IPipelineNode<T, TOut> node,
        TimeSpan timeout,
        IScheduler? scheduler = null)
    {
        var newSource = _source.ThroughWithTimeout(node, timeout, scheduler);
        return new ReactivePipelineBuilder<TOut>(newSource);
    }

    public IObservable<T> Build() => _source;

    public static ReactivePipelineBuilder<T> FromObservable(IObservable<T> source)
        => new(source);
}