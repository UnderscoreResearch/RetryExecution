using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

namespace Underscore.RetryExecution
{
  /// <summary>
  /// Execute an action with retry on failure.
  /// </summary>
  public class RetryExecutor
  {
    /// <summary>
    /// Callback for indicating how the call succeeded. Is called even if the call succeeds on the first call.
    /// </summary>
    public delegate void RetryExecutorLogHandler(int retries, Delegate calledDelegate, bool succeeded);

    private ShouldRetry retryDelegate;
    private ITransientErrorDetectionStrategy transientErrorDetection;
    private RetryExecutorLogHandler defaultMessage;

    public class AllExceptApplicationExceptions : ITransientErrorDetectionStrategy
    {
      #region Implementation of ITransientErrorDetectionStrategy

      /// <summary>
      /// Determines whether the specified exception represents a transient failure that can be compensated by a retry.
      /// </summary>
      /// <param name="ex">The exception object to be verified.</param>
      /// <returns>
      /// true if the specified exception is considered as transient; otherwise, false.
      /// </returns>
      public bool IsTransient(Exception ex)
      {
        return !(ex is ApplicationException);
      }

      #endregion
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:System.Object"/> class.
    /// </summary>
    public RetryExecutor(ShouldRetry retryDelegate, ITransientErrorDetectionStrategy transientErrorDetection, RetryExecutorLogHandler defaultMessage)
    {
      this.retryDelegate = retryDelegate;
      this.transientErrorDetection = transientErrorDetection;
      this.defaultMessage = defaultMessage;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:System.Object"/> class.
    /// </summary>
    public RetryExecutor()
      : this(DefaultShouldRetry, new AllExceptApplicationExceptions(), DefaultMessage)
    {
    }

    /// <summary>
    /// Default should retry delegate. Will try 10 times waiting the number of retries before seconds between retries.
    /// </summary>
    public static bool DefaultShouldRetry(int retrycount, Exception lastexception, out TimeSpan delay)
    {
      return DefaultShouldRetry(retrycount, lastexception, out delay, TimeSpan.TicksPerSecond, 10);
    }

    /// <summary>
    /// Default should retry delegate. Will try 10 times waiting the number of retries before seconds between retries.
    /// </summary>
    public static bool DefaultShouldRetry(int retrycount, Exception lastexception, out TimeSpan delay, long tickBaseMultiplier, int maximumRetries)
    {
      if (retrycount > maximumRetries)
      {
        delay = new TimeSpan(0);
        return false;
      }
      delay = new TimeSpan(tickBaseMultiplier * retrycount);
      return true;
    }

    /// <summary>
    /// Default completed message handler.
    /// </summary>
    /// <param name="retryCount">The number of retries before completion.</param>
    /// <param name="action">The action that was completed.</param>
    /// <param name="success">True if the call eventually succeeded.</param>
    public static void DefaultMessage(int retryCount, Delegate action, bool success)
    {
      if (retryCount > 0)
      {
        string msg = GetDefaultMessage(retryCount, action, success);

        Console.WriteLine("WARNING: {0}", msg);
      }
    }

    /// <summary>
    /// Get default completed message handler.
    /// </summary>
    /// <param name="retryCount">The number of retries before completion.</param>
    /// <param name="action">The action that was completed.</param>
    /// <param name="success">True if the call eventually succeeded.</param>
    public static string GetDefaultMessage(int retryCount, Delegate action, bool success)
    {
      if (action.Method != null && action.Method.DeclaringType != null)
      {
        if (success)
          return String.Format("Needed {0} tries to complete {1}.{2}", retryCount, action.Method.DeclaringType.FullName,
            action.Method.Name);
        else
          return String.Format("Failed after {0} tries {1}.{2}", retryCount, action.Method.DeclaringType.FullName,
            action.Method.Name);
      }
      else
      {
        if (success)
          return String.Format("Needed {0} tries to complete {1}", retryCount, action);
        else
          return String.Format("Failed after {0} tries {1}", retryCount, action);
      }
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancelToken, RetryExecutorLogHandler completedMessage)
    {
      int retryCount = 0;
      TimeSpan span = new TimeSpan(0);
      bool success = false;
      try
      {
        while (true)
        {
          try
          {
            await action();
            success = true;
            return;
          }
          catch (Exception exc)
          {
            if (!transientErrorDetection.IsTransient(exc))
              TaskUtil.RethrowException(exc);
            if (!retryDelegate(retryCount + 1, exc, out span))
              TaskUtil.RethrowException(exc);
            retryCount++;
            cancelToken.ThrowIfCancellationRequested();
          }

          await Task.Delay(span, cancelToken);
          cancelToken.ThrowIfCancellationRequested();
        }
      }
      finally
      {
        completedMessage(retryCount, action, success);
      }
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public async Task ExecuteAsync(Func<Task> action)
    {
      await ExecuteAsync(action, CancellationToken.None, defaultMessage);
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancelToken)
    {
      await ExecuteAsync(action, cancelToken, defaultMessage);
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public async Task<S> ExecuteAsync<S>(Func<Task<S>> func, CancellationToken cancelToken, RetryExecutorLogHandler completedMessage)
    {
      int retryCount = 0;
      bool success = false;
      TimeSpan span = new TimeSpan(0);
      try
      {
        while (true)
        {
          try
          {
            S ret = await func();
            success = true;
            return ret;
          }
          catch (Exception exc)
          {
            if (!transientErrorDetection.IsTransient(exc))
              TaskUtil.RethrowException(exc);
            if (!retryDelegate(retryCount + 1, exc, out span))
              TaskUtil.RethrowException(exc);
            retryCount++;
            cancelToken.ThrowIfCancellationRequested();
          }

          await Task.Delay(span, cancelToken);
          cancelToken.ThrowIfCancellationRequested();
        }
      }
      finally
      {
        completedMessage(retryCount, func, success);
      }
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public async Task<S> ExecuteAsync<S>(Func<Task<S>> func)
    {
      return await ExecuteAsync(func, CancellationToken.None, defaultMessage);
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public S ExecuteAction<S>(Func<S> func, CancellationToken cancelToken, RetryExecutorLogHandler completedMessage)
    {
      int retryCount = 0;
      bool success = false;
      TimeSpan span = new TimeSpan(0);
      try
      {
        while (true)
        {
          try
          {
            S ret = func();
            success = true;
            return ret;
          }
          catch (Exception exc)
          {
            if (!transientErrorDetection.IsTransient(exc))
              TaskUtil.RethrowException(exc);
            if (!retryDelegate(retryCount + 1, exc, out span))
              TaskUtil.RethrowException(exc);
            retryCount++;
            cancelToken.ThrowIfCancellationRequested();
          }

          Task.Delay(span, cancelToken).Wait();
          cancelToken.ThrowIfCancellationRequested();
        }
      }
      finally
      {
        completedMessage(retryCount, func, success);
      }
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public S ExecuteAction<S>(Func<S> func)
    {
      return ExecuteAction<S>(func, CancellationToken.None, defaultMessage);
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public S ExecuteAction<S>(Func<S> func, CancellationToken cancelToken)
    {
      return ExecuteAction<S>(func, cancelToken, defaultMessage);
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public void ExecuteAction(Func<Task> func, CancellationToken cancelToken, RetryExecutorLogHandler completedMessage)
    {
      Task.Run(() => ExecuteAsync(func, cancelToken, completedMessage)).Wait();
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public void ExecuteAction(Func<Task> func)
    {
      ExecuteAction(func, CancellationToken.None, defaultMessage);
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public void ExecuteAction(Func<Task> func, CancellationToken cancelToken)
    {
      ExecuteAction(func, cancelToken, defaultMessage);
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public S ExecuteAction<S>(Func<Task<S>> func, CancellationToken cancelToken, RetryExecutorLogHandler completedMessage)
    {
      return Task.Run(() => ExecuteAsync(func, cancelToken, completedMessage)).Result;
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public S ExecuteAction<S>(Func<Task<S>> func)
    {
      return ExecuteAction(func, CancellationToken.None, defaultMessage);
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public S ExecuteAction<S>(Func<Task<S>> func, CancellationToken cancelToken)
    {
      return ExecuteAction(func, cancelToken, defaultMessage);
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public void ExecuteAction(Action action, CancellationToken cancelToken, RetryExecutorLogHandler completedMessage)
    {
      int retryCount = 0;
      bool success = false;
      TimeSpan span = new TimeSpan(0);
      try
      {
        while (true)
        {
          try
          {
            action();
            success = true;
            return;
          }
          catch (Exception exc)
          {
            if (!transientErrorDetection.IsTransient(exc))
              TaskUtil.RethrowException(exc);
            if (!retryDelegate(retryCount + 1, exc, out span))
              TaskUtil.RethrowException(exc);
            retryCount++;
            cancelToken.ThrowIfCancellationRequested();
          }

          Task.Delay(span, cancelToken).Wait();
          cancelToken.ThrowIfCancellationRequested();
        }
      }
      finally
      {
        completedMessage(retryCount, action, success);
      }
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public void ExecuteAction(Action action)
    {
      ExecuteAction(action, CancellationToken.None, defaultMessage);
    }

    /// <summary>
    /// Execute an action with retry.
    /// </summary>
    public void ExecuteAction(Action action, CancellationToken cancelToken)
    {
      ExecuteAction(action, cancelToken, defaultMessage);
    }
  }
}