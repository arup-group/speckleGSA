using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA.UI.Utilities
{
  /// <summary>
  /// A helper class to report status using the IProgress interface and reporting back a class (TReported).
  /// The object being reported can only be used ONCE. If createNewInstanceAfterReport is FALSE (the default), calling Report again will raise an exception.
  /// If createNewInstanceAfterReport is TRUE, a new instance of the reported object will automatically be created after calling Report().
  /// </summary>
  /// <typeparam name="TReported">The object beeing reported when calling Report()</typeparam>
  public class ProgressReporter<TReported> where TReported : class, new()
  {
    IProgress<TReported> _progress;

    bool _createNewInstanceAfterReport;

    /// <summary>
    /// Provides access to the instance that is beeing reported as the "Progress". 
    /// The setter is private ON PURPOSE to avoid that a caller passes in an already used instance which could result in exactly that bug we are trying to avoid with this class.
    /// </summary>
    public TReported Content { get; private set; }


    /// <summary>
    /// Create an instance of this class, requires the IProgress<TReported> implementation that is used to report progress.
    /// </summary>
    /// <param name="progress">IProgress implementation used to report progress</param>
    /// <param name="createNewInstanceAfterReport">TRUE if a new instance of Content should automatically be created after Report()</param>
    public ProgressReporter(IProgress<TReported> progress, bool createNewInstanceAfterReport = false)
    {
      _progress = progress;
      Content = new TReported();

      _createNewInstanceAfterReport = createNewInstanceAfterReport;
    }

    /// <summary>
    /// Reports Content back as progress. Can be used only ONCE if createNewInstanceAfterReport is FALSE.
    /// </summary>
    public void Report()
    {
      //MTH: We need to make sure that an instance of this class is used only once because ReportedObject might be used in a entire different thread. 
      //Quote from [Reporting Progress from Async Tasks](http://blog.stephencleary.com/2012/02/reporting-progress-from-async-tasks.html) by Stephen Cleary
      //"...it means you can’t modify the progress object after it’s passed to Report. It is an error to keep a single “current progress” object, update it, and repeatedly pass it to Report."
      if (Content != null)
      {
        if (_progress != null)
        {
          _progress.Report(Content);
        }

        //Set Content to null no matter if we have _progress or not to make sure that an incorrect use of this class is detected.
        Content = null;

        //If requested by the caller, create a new instance
        if (_createNewInstanceAfterReport)
        {
          Content = new TReported();
        }
      }
      else
      {
        //Seems you didn't read the part about "USE IT ONLY ONCE".
        //Here's an exception for you.
        //You're welcome. 
        throw new InvalidOperationException("An instance of the reported object can only be used once");
      }
    }
  }
}
