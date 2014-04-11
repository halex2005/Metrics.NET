﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Metrics.Utils
{
    /// <summary>
    /// Utility class to schedule an Action to be executed repeatedly according to the interval.
    /// </summary>
    /// <remarks>
    /// The scheduling code is heavenly inspired form Daniel Crenna's metrics port
    /// https://github.com/danielcrenna/metrics-net/blob/master/src/metrics/Reporting/ReporterBase.cs
    /// </remarks>
    public sealed class ActionScheduler : IDisposable
    {
        private readonly TimeSpan interval;
        private readonly Action<CancellationToken> action;
        private CancellationTokenSource token = null;

        public ActionScheduler(TimeSpan interval, Action<CancellationToken> action)
        {
            this.interval = interval;
            this.action = action;
        }

#if !NET40
        public void Start()
        {
            this.token = new CancellationTokenSource();
            Task.Factory.StartNew(async () =>
            {
                while (!this.token.IsCancellationRequested)
                {
                    await Task.Delay(interval, this.token.Token);
                    if (!this.token.IsCancellationRequested)
                    {
                        RunAction();
                    }
                }
            }, this.token.Token);
        }
#else
        public void Start()
        {
            this.token = new CancellationTokenSource();
            Task.Factory.StartNew(() =>
            {
                while (!this.token.IsCancellationRequested)
                {
                    Delay(interval.Milliseconds).ContinueWith(t =>
                    {
                        if (!this.token.IsCancellationRequested)
                        {
                            RunAction();
                        }
                    });
                }
            }, this.token.Token);
        }

        private static Task Delay(double milliseconds)
        {
            var tcs = new TaskCompletionSource<bool>();
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Elapsed += (obj, args) =>
            {
                tcs.TrySetResult(true);
            };
            timer.Interval = milliseconds;
            timer.AutoReset = false;
            timer.Start();
            return tcs.Task;
        }
#endif

        public void Stop()
        {
            if (this.token != null)
            {
                token.Cancel();
            }
        }

        private void RunAction()
        {
            try
            {
                this.action(this.token.Token);
            }
            catch (Exception x)
            {
                if (Metric.ErrorHandler != null)
                {
                    Metric.ErrorHandler(x);
                }
                else
                {
                    throw;
                }
            }
        }

        public void Dispose()
        {
            if (this.token != null)
            {
                this.token.Dispose();
                this.token = null;
            }
            GC.SuppressFinalize(this);
        }
    }
}