using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSA
{
  internal class ProgressEstimator
  {
    private readonly List<ProcessingPhase> workPhases = new List<ProcessingPhase>();

    public double WeightedCurrent { get => GetWithLock(() => workPhases.Sum(p => p.Current * p.Weighting)); }

    public double WeightedTotal { get => GetWithLock(() => workPhases.Sum(p => p.Total * p.Weighting)); }

    public double Percentage { get => GetWithLock(() => WeightedCurrent / WeightedTotal * 100); }

    private IProgress<double> percentageProgress;
    private readonly object changeLock = new object();

    public ProgressEstimator(IProgress<double> percentageProgress, params object[] pairs)
    {
      this.percentageProgress = percentageProgress;
      var length = (pairs.Length % 2 == 0) ? pairs.Length : pairs.Length - 1;
      for (int i = 0; i < length; i+=2)
      {
        if (pairs[i] != null && pairs[i] is WorkPhase && pairs[i + 1] != null)
        {
          if (pairs[i + 1] is double)
          {
            workPhases.Add(new ProcessingPhase(phase: (WorkPhase)pairs[i], weighting: (double)pairs[i + 1]));
          }
          else if (pairs[i + 1] is int)
          {
            workPhases.Add(new ProcessingPhase(phase: (WorkPhase)pairs[i], weighting: Convert.ToDouble(pairs[i + 1])));
          }
        }
      }
    }

    public void Clear()
    {
      lock (changeLock)
      {
        workPhases.ForEach(p => p.Clear());
      }
    }

    public void UpdateTotal(WorkPhase phase, double total) => ActionWithCheck(phase, total, (i, v) => workPhases[i].Total = v);

    public void UpdateCurrent(WorkPhase phase, double curr) => ActionWithCheck(phase, curr, (i, v) => workPhases[i].Current = v);

    public void AppendCurrent(WorkPhase phase, double curr) => ActionWithCheck(phase, curr, (i, v) => workPhases[i].Current += v);

    public void AppendTotal(WorkPhase phase, double total) => ActionWithCheck(phase, total, (i, v) => workPhases[i].Total += v);

    public void SetCurrentToTotal(WorkPhase phase)
    {
      lock (changeLock)
      {
        if (ContainsPhase(phase, out int? foundIndex) && foundIndex.HasValue)
        {
          workPhases[foundIndex.Value].Current = workPhases[foundIndex.Value].Total;
          percentageProgress.Report(Percentage);
        }
      }
    }

    private double GetWithLock(Func<double> fn)
    {
      double val;
      lock(changeLock)
      {
        val = fn();
      }
      return val;
    }

    private void ActionWithCheck(WorkPhase phase, double val, Action<int, double> fn)
    {
      lock (changeLock)
      {
        if (ContainsPhase(phase, out int? foundIndex) && foundIndex.HasValue)
        {
          fn(foundIndex.Value, val);
          percentageProgress.Report(Percentage);
        }
      }
    }

    private bool ContainsPhase(WorkPhase phase, out int? foundIndex)
    {
      var phaseItem = workPhases.FirstOrDefault(p => p.Phase == phase);
      foundIndex = (phaseItem == null) ? null : (int?)workPhases.IndexOf(phaseItem);
      return (phaseItem != null && foundIndex.HasValue);
    }
  }
}