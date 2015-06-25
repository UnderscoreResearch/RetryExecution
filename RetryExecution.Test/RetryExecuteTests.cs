using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Underscore.RetryExecution.Test
{
  [TestFixture]
  public class RetryExecuteTests
  {
    private RetryExecutor CreateRetryExecutor(List<string> msg)
    {
      return new RetryExecutor(ShouldRetry, new RetryExecutor.AllExceptApplicationExceptions(),
        delegate(int retries, Delegate del, bool succeeded)
        {
          if (retries > 0)
            msg.Add(RetryExecutor.GetDefaultMessage(retries, del, succeeded));
        });
    }

    private bool ShouldRetry(int retrycount, Exception lastexception, out TimeSpan delay)
    {
      return RetryExecutor.DefaultShouldRetry(retrycount, lastexception, out delay, TimeSpan.TicksPerSecond/8, 4);
    }

    [Test]
    public void RetryPolicyFailCancelTest()
    {
      List<string> msg = new List<string>();
      RetryExecutor executor = CreateRetryExecutor(msg);

      CancellationTokenSource source = new CancellationTokenSource();
      bool shouldThrow = true;
      Task task1 = Task.Run(async () => await executor.ExecuteAsync(() =>
      {
        throw new Exception();
      }, source.Token));

      Action action = () =>
      {
        throw new Exception();
      };

      Task task2 = Task.Run(() => executor.ExecuteAction(action, source.Token));
      Task task3 = Task.Run(() =>
      {
        int tmp = executor.ExecuteAction(() =>
        {
          if (shouldThrow)
            throw new Exception();
          return 1;
        }, source.Token);
      });
      Task task4 = Task.Run(() =>
      {
        int tmp = executor.ExecuteAction(async () =>
        {
          if (shouldThrow)
            throw new Exception();
          await Task.Delay(1);
          return 1;
        }, source.Token);
      });

      Thread.Sleep(250);
      source.Cancel();
      try
      {
        if (!Task.WhenAll(task1, task2, task3, task4).Wait(250))
          throw new Exception("Tasks didn't end in a proper manner");
        throw new Exception("Tasks didn't fail in a proper manner.");
      }
      catch (AggregateException exc)
      {
        ProcessExceptions(exc);
      }

      foreach (string item in msg)
        Assert.IsTrue(item.StartsWith("Failed after 2 ") && item.Contains("RetryExecuteTests"), item);
    }

    [Test]
    public void RetryPolicyFailExceptionTest()
    {
      List<string> msg = new List<string>();
      RetryExecutor executor = CreateRetryExecutor(msg);
      
      CancellationTokenSource source = new CancellationTokenSource();
      bool shouldThrow = true;
      Task task1 = Task.Run(async () => await executor.ExecuteAsync(() =>
      {
        throw new ApplicationException();
      }, source.Token));

      Action action = () =>
      {
        throw new ApplicationException();
      };

      Task task2 = Task.Run(() => executor.ExecuteAction(action, source.Token));
      Task task3 = Task.Run(() =>
      {
        int tmp = executor.ExecuteAction(() =>
        {
          if (shouldThrow)
            throw new ApplicationException();
          return 1;
        }, source.Token);
      });
      Task task4 = Task.Run(() =>
      {
        int tmp = executor.ExecuteAction(async () =>
        {
          if (shouldThrow)
            throw new ApplicationException();
          await Task.Delay(1);
          return 1;
        }, source.Token);
      });

      try
      {
        if (!Task.WhenAll(task1, task2, task3, task4).Wait(250))
          throw new Exception("Tasks didn't end in a proper manner");
        throw new Exception("Tasks didn't fail in a proper manner.");
      }
      catch (AggregateException exc)
      {
        ProcessExceptions(exc, typeof(ApplicationException));
      }

      Assert.AreEqual(0, msg.Count);
    }


    [Test]
    public void RetryPolicyFailTimeoutTest()
    {
      List<string> msg = new List<string>();
      RetryExecutor executor = CreateRetryExecutor(msg);
      
      CancellationTokenSource source = new CancellationTokenSource();
      bool shouldThrow = true;
      Task task1 = Task.Run(async () => await executor.ExecuteAsync(() =>
      {
        throw new TimeoutException();
      }, source.Token));

      Action action = () =>
      {
        throw new TimeoutException();
      };

      Task task2 = Task.Run(() => executor.ExecuteAction(action, source.Token));
      Task task3 = Task.Run(() =>
      {
        int tmp = executor.ExecuteAction(() =>
        {
          if (shouldThrow)
            throw new TimeoutException();
          return 1;
        }, source.Token);
      });
      Task task4 = Task.Run(() =>
      {
        int tmp = executor.ExecuteAction(async () =>
        {
          if (shouldThrow)
            throw new TimeoutException();
          await Task.Delay(1);
          return 1;
        }, source.Token);
      });

      Assert.IsFalse(Task.WaitAll(new[] { task1, task2, task3, task4 }, 1000));

      try
      {
        if (!Task.WhenAll(task1, task2, task3, task4).Wait(1000))
          throw new Exception("Tasks didn't end in a proper manner");
        throw new Exception("Tasks didn't fail in a proper manner.");
      }
      catch (AggregateException exc)
      {
        ProcessExceptions(exc, typeof(TimeoutException));
      }

      foreach (string item in msg)
        Assert.IsTrue(item.StartsWith("Failed after 4 ") && item.Contains("RetryExecuteTests"), item);
    }

    [Test]
    public void RetryPolicSeconderySuccessTest()
    {
      List<string> msg = new List<string>();
      RetryExecutor executor = CreateRetryExecutor(msg);
      
      CancellationTokenSource source = new CancellationTokenSource();
      int retry1 = 0;
      Task task1 = Task.Run(async () => await executor.ExecuteAsync(async () =>
      {
        if (retry1++ == 0)
          throw new Exception();
        await Task.Delay(1);
      }, source.Token));

      int retry2 = 0;
      Action action = () =>
      {
        if (retry2++ == 0)
          throw new Exception();
      };

      Task task2 = Task.Run(() => executor.ExecuteAction(action, source.Token));

      int retry3 = 0;
      Task task3 = Task.Run(() =>
      {
        int tmp = executor.ExecuteAction(() =>
        {
          if (retry3++ == 0)
            throw new Exception();
          return 1;
        }, source.Token);
      });

      int retry4 = 0;
      Task task4 = Task.Run(() =>
      {
        int tmp = executor.ExecuteAction(async () =>
        {
          if (retry4++ == 0)
            throw new Exception();
          await Task.Delay(1);
          return 1;
        }, source.Token);
      });

      if (!Task.WhenAll(task1, task2, task3, task4).Wait(500))
        throw new Exception("Tasks didn't end in a proper manner");

      foreach (string item in msg)
        Assert.IsTrue(item.StartsWith("Needed 1 ") && item.Contains("RetryExecuteTests"), item);
    }

    [Test]
    public void RetryPolicSuccessTest()
    {
      List<string> msg = new List<string>();
      RetryExecutor executor = CreateRetryExecutor(msg);
      
      CancellationTokenSource source = new CancellationTokenSource();
      Task task1 = Task.Run(async () => await executor.ExecuteAsync(async () =>
      {
        await Task.Delay(1);
      }, source.Token));

      Action action = () =>
      {
      };

      Task task2 = Task.Run(() => executor.ExecuteAction(action, source.Token));

      Task task3 = Task.Run(() =>
      {
        int tmp = executor.ExecuteAction(() =>
        {
          return 1;
        }, source.Token);
      });

      Task task4 = Task.Run(() =>
      {
        int tmp = executor.ExecuteAction(async () =>
        {
          await Task.Delay(1);
          return 1;
        }, source.Token);
      });

      if (!Task.WhenAll(task1, task2, task3, task4).Wait(500))
        throw new Exception("Tasks didn't end in a proper manner");

      Assert.AreEqual(0, msg.Count);
    }

    private void ProcessExceptions(AggregateException exc, Type expectedException)
    {
      foreach (Exception exception in exc.InnerExceptions)
      {
        AggregateException aggr = exception as AggregateException;
        if (aggr != null)
          ProcessExceptions(aggr, expectedException);
        else if (exception.GetType() != expectedException)
          TaskUtil.RethrowException(exc);
      }
    }

    private void ProcessExceptions(AggregateException exc)
    {
      ProcessExceptions(exc, typeof(TaskCanceledException));
    }
     
  }
}