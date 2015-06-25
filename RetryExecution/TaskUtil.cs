using System;
using System.Runtime.ExceptionServices;

namespace Underscore.RetryExecution
{
  /// <summary>
  /// Helper functions for dealing with exceptions.
  /// </summary>
  public static class TaskUtil
  {
    /// <summary>
    /// Rethrow an exception while keeping its stack trace.
    /// </summary>
    public static void RethrowException(Exception exc)
    {
      ExceptionDispatchInfo.Capture(exc).Throw();
    }
  }
}
