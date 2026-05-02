using System;
using System.Threading;
using System.Threading.Tasks;

public static class TaskExtensions
{
    public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        using (cancellationToken.Register(state => ((TaskCompletionSource<object>)state).TrySetResult(null), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        return await task;
    }

    public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        using (cancellationToken.Register(state => ((TaskCompletionSource<object>)state).TrySetResult(null), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        await task;
    }
}
