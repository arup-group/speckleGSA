namespace SpeckleGSA
{
  //Used for estimation of work for progress bar
  internal class ProcessingPhase
  {
    public WorkPhase Phase { get; set; }
    public double Weighting { get; set; }
    public double Total { get; set; }
    public double Current { get; set; }

    public void Clear()
    {
      this.Total = 0;
      this.Current = 0;
    }

    public ProcessingPhase(WorkPhase phase, double weighting)
    {
      this.Phase = phase;
      this.Weighting = weighting;
    }
  }

  internal enum WorkPhase
  {
    CacheRead,
    CacheUpdate,
    Conversion,
    ApiCalls
  }
}
