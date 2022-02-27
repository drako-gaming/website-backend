using System;
using System.Collections.Generic;
using System.Threading;
using Quartz;

namespace Drako.Api.Controllers.Jobs;

public class JobExecutionContext : IJobExecutionContext
{
    private Dictionary<object, object> bag = new Dictionary<object, object>();
        
    public JobExecutionContext()
    {
        FireTimeUtc = DateTimeOffset.UtcNow;
    }
        
    public void Put(object key, object objectValue)
    {
        bag[key] = objectValue;
    }

    public object? Get(object key)
    {
        bag.TryGetValue(key, out var result);
        return result;
    }

    public IScheduler Scheduler { get; }
    public ITrigger Trigger { get; }
    public ICalendar? Calendar { get; }
    public bool Recovering { get; }
    public TriggerKey RecoveringTriggerKey { get; }
    public int RefireCount { get; }
    public JobDataMap MergedJobDataMap { get; }
    public IJobDetail JobDetail { get; }
    public IJob JobInstance { get; }
    public DateTimeOffset FireTimeUtc { get; }
    public DateTimeOffset? ScheduledFireTimeUtc { get; }
    public DateTimeOffset? PreviousFireTimeUtc { get; }
    public DateTimeOffset? NextFireTimeUtc { get; }
    public string FireInstanceId { get; }
    public object? Result { get; set; }
    public TimeSpan JobRunTime { get; }
    public CancellationToken CancellationToken { get; }
}