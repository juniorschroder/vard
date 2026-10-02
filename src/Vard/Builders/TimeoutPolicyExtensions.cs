using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vard.Abstractions;
using Vard.Policies;

namespace Vard.Builders
{
    public static class TimeoutPolicyExtensions
    {
        public static TimeoutPolicy Timeout(
            this IPolicyBuilder builder,
            TimeSpan timeout,
            Action<IDictionary<string, object>?, TimeSpan>? onTimeout = null,
            Func<IDictionary<string, object>?, TimeSpan, Task>? onTimeoutAsync = null)
        {
            return new TimeoutPolicy(timeout, onTimeout, onTimeoutAsync);
        }
    }
}
