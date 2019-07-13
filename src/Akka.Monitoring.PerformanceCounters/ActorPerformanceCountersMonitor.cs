﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Akka.Monitoring.Impl;

namespace Akka.Monitoring.PerformanceCounters
{
    public class ActorPerformanceCountersMonitor : AbstractActorMonitoringClient
    {
        private const string TotalCounterInstanceName = "_Total";
        private static readonly Guid MonitorName = new Guid("F651B9F8-AA38-45BD-BFB9-C5595519C23C");
        private static readonly HashSet<string> BuiltInCounterNames = new HashSet<string>(new[]
        {
            CounterNames.ActorRestarts,
            CounterNames.ActorsCreated,
            CounterNames.ActorsStopped,
            CounterNames.ReceivedMessages,
            CounterNames.DeadLetters,
            CounterNames.UnhandledMessages,
            CounterNames.DebugMessages,
            CounterNames.InfoMessages,
            CounterNames.WarningMessages,
            CounterNames.ErrorMessages,
        });

        private readonly Dictionary<string, AkkaCounter> _counters;
        private readonly Dictionary<string, AkkaGauge> _gauges;
        private readonly Dictionary<string, AkkaTimer> _timers;

        private readonly string categoryName;

        public ActorPerformanceCountersMonitor(CustomMetrics customMetrics = null, string categoryName = "Akka")
        {
            this.categoryName = categoryName;

            var counterNames = BuiltInCounterNames;
            var gaugeNames = new HashSet<string>();
            var timerNames = new HashSet<string>();
            if (customMetrics != null)
            {
                counterNames = new HashSet<string>(counterNames.Concat(customMetrics.Counters));
                gaugeNames = customMetrics.Gauges;
                timerNames = customMetrics.Timers;
            }
            _counters = counterNames.ToDictionary(cn => cn, cn => new AkkaCounter(cn, categoryName));
            _gauges = gaugeNames.ToDictionary(cn => cn, cn => new AkkaGauge(cn, categoryName));
            _timers = timerNames.ToDictionary(cn => cn, cn => new AkkaTimer(cn, categoryName));
            Init(_counters.Values.Cast<AkkaMetric>().Concat(_gauges.Values).Concat(_timers.Values));
        }

        public override void UpdateCounter(string metricName, int delta, double sampleRate)
        {
            var resolution = ResolveMetricInstance(metricName, _counters);
            resolution?.Item1.Update(resolution.Item2, delta);
        }

        public virtual void ResetCounterTotal(string metricName)
        {
            var resolution = ResolveMetricInstance(metricName, _counters);
            resolution?.Item1.ResetTotal(resolution.Item2);
        }

        public virtual void ResetAllCounterTotals()
        {
            foreach (var akkaCounterKvp in _counters)
            {
                akkaCounterKvp.Value.ResetTotal(TotalCounterInstanceName);
            }
        }

        public override void UpdateGauge(string metricName, int value, double sampleRate)
        {
            var resolution = ResolveMetricInstance(metricName, _gauges);
            resolution?.Item1.Update(resolution.Item2, value);
        }

        public override void UpdateTiming(string metricName, long time, double sampleRate)
        {
            var resolution = ResolveMetricInstance(metricName, _timers);
            resolution?.Item1.Update(resolution.Item2, time);
        }

        public override int MonitoringClientId => MonitorName.GetHashCode();

        public override void DisposeInternal()
        {            
        }

        private void Init(IEnumerable<AkkaMetric> akkaMetrics)
        {
            var ccdc = new CounterCreationDataCollection();

            foreach (var akkaMetric in akkaMetrics)
            {
                akkaMetric.RegisterIn(ccdc);
            }            

            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                //Only create if it doesn't exist.
                PerformanceCounterCategory.Create(categoryName, "", PerformanceCounterCategoryType.MultiInstance, ccdc);
            }

            
        }

        //extracts metric object and instance name ("_total" or ActorSpecificCategory)
        private Tuple<TAkkaMetric,string> ResolveMetricInstance<TAkkaMetric>(string metricName, Dictionary<string,TAkkaMetric> metrics)
        {                       
            if (metricName == null)
            {
                return null;
            }
            if (metrics.ContainsKey(metricName))
            {
                return Tuple.Create(metrics[metricName],TotalCounterInstanceName);
            }
            foreach (var counterName in metrics.Keys)
            {
                if (metricName.EndsWith(counterName))
                {
                    var counterNamePartIndex = metricName.LastIndexOf(counterName, StringComparison.InvariantCulture);

                    var counterNamePart = metricName.Substring(counterNamePartIndex);
                    return Tuple.Create(metrics[counterNamePart],metricName.Substring(0, counterNamePartIndex - 1));
                }
            }
            return null;
        }
    }
}
