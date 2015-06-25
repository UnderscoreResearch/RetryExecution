# RetryExecution

This is a small library to help you retry execution of a block of code similar to the [Transient Fault Handling Application Block](https://msdn.microsoft.com/en-us/library/hh680934%28v=pandp.50%29.aspx) from Microsoft. It does support reusing the definitions from this library to detect transient exceptions and retry delay delegates.

Where it differs is that it will only log once per call, regardless of how many retries are needed only one logging call is needed. Also the action executed is passed in to the logging call and
whether the call eventually succeeded, this allows you to write a generic logging call that actually shows you what went wrong and it wont fill your logs.

Finally it also support async method blocks. Here is an example of usage.

```C#
      RetryExecutor executor = new RetryExecutor();
      executor.ExecuteAction(() => { ... Do something ... });
      var val executor.ExecuteAction(() => { ... Do something ...; return val; });
      await executor.ExecuteAsync(async () => { ... Do something async ... });
      var val = await executor.ExecuteAsync(async () => { ... Do something async ...; return val; });
```

By default only ApplicationExceptions are passed through without retries. Also the retry strategy will try 10 times waiting for the number of previously tries seconds until the next try (Which means it will signal a failure after around 55 seconds). The logging will just write to the standard output.

[Henrik Johnson](http://www.henrik.org)

[Underscore Research](http://www.underscoreresearch.com/)
