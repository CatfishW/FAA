using System;

namespace FAA.XPlaneIntegration.Core
{
    /// <summary>
    /// Bounds snapshot retries during an API/tunnel outage. The clock is supplied
    /// by the caller so recovery is independent of simulation time and testable.
    /// </summary>
    public sealed class XPlaneApiRetryPolicy
    {
        public int ConsecutiveFailures { get; private set; }
        public double RetryAt { get; private set; }

        public bool CanRequest(double now) => now >= RetryAt;
        public double SecondsRemaining(double now) => Math.Max(0d, RetryAt - now);

        public void RecordFailure(double now)
        {
            ConsecutiveFailures = Math.Min(ConsecutiveFailures + 1, 30);
            double delay = Math.Min(10d, Math.Pow(2d, Math.Min(ConsecutiveFailures - 1, 4)));
            RetryAt = now + delay;
        }

        public void Reset()
        {
            ConsecutiveFailures = 0;
            RetryAt = 0d;
        }

        // A disconnected host, timeout, 429 or 5xx is NOT an older API version.
        public static bool AllowsCompatibilityFallback(long responseCode) =>
            responseCode == 404 || responseCode == 405;
    }
}
