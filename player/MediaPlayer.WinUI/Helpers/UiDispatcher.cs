namespace MediaPlayer.Helpers;

public static class UiDispatcher
{
    public static bool IsOnUiThread =>
        App.DispatcherQueue?.HasThreadAccess == true;

    public static void Invoke(Action action)
    {
        if (IsOnUiThread)
        {
            action();
            return;
        }

        if (App.DispatcherQueue?.TryEnqueue(() => action()) != true)
            action();
    }

    public static Task InvokeAsync(Action action)
    {
        if (IsOnUiThread)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        if (App.DispatcherQueue?.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }) != true)
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }

        return tcs.Task;
    }

    public static Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (IsOnUiThread)
            return Task.FromResult(func());

        var tcs = new TaskCompletionSource<T>();
        if (App.DispatcherQueue?.TryEnqueue(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }) != true)
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }

        return tcs.Task;
    }

    public static Task<T> InvokeAsync<T>(Func<Task<T>> func)
    {
        if (IsOnUiThread)
            return func();

        var tcs = new TaskCompletionSource<T>();
        if (App.DispatcherQueue?.TryEnqueue(async () =>
        {
            try
            {
                tcs.SetResult(await func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }) != true)
        {
            return func();
        }

        return tcs.Task;
    }
}
