using System;
using System.Collections.Generic;
using System.Linq;
using SpeckleCore;
using SpeckleGSAInterfaces;
using SpeckleGSAProxy;

namespace SpeckleGSA
{
	public abstract class BaseReceiverSender
	{
		public bool IsInit = false;
		public bool IsBusy = false;

    #region lock-related

    protected T ExecuteWithLock<T>(ref object lockObject, Func<T> f)
    {
      lock (lockObject)
      {
        return f();
      }
    }

    protected void ExecuteWithLock(ref object lockObject, Action a)
    {
      lock (lockObject)
      {
        a();
      }
    }
    #endregion

    protected List<string> GetFilteredKeywords(IEnumerable<KeyValuePair<Type, List<Type>>> prereqs)
    {
      var keywords = new List<string>();
      foreach (var kvp in prereqs)
      {
        try
        {
          var keyword = (string)kvp.Key.GetAttribute("GSAKeyword");
          if (keyword.Length > 0)
          {
            keywords.AddIfNotContains(keyword);
            var subKeywords = (string[])kvp.Key.GetAttribute("SubGSAKeywords");
            if (subKeywords.Length > 0)
            {
              foreach (var skw in subKeywords)
              {
                keywords.AddIfNotContains(skw);
              }
            }
          }
        }
        catch { }
      }
      return keywords;
    }

    protected List<string> GetFilteredKeywords(Dictionary<Type, List<Type>> prereqs)
    {
      var keywords = new List<string>();
      foreach (var kvp in prereqs)
      {
        try
        {
          var keyword = (string)kvp.Key.GetAttribute("GSAKeyword");
          if (keyword.Length > 0)
          {
            keywords.AddIfNotContains(keyword);
            var subKeywords = (string[])kvp.Key.GetAttribute("SubGSAKeywords");
            if (subKeywords.Length > 0)
            {
              foreach (var skw in subKeywords)
              {
                keywords.AddIfNotContains(skw);
              }
            }
          }
        }
        catch { }
      }
      return keywords;
    }

		protected bool ObjectTypeMatchesLayer(Type t, GSATargetLayer layer)
		{
			var analysisLayerAttribute = t.GetAttribute("AnalysisLayer");
			var designLayerAttribute = t.GetAttribute("DesignLayer");

			//If an object type has a layer attribute exists and its boolean value doesn't match the settings target layer, then it doesn't match.  This could be reviewed and simplified.
			if ((analysisLayerAttribute != null && layer == GSATargetLayer.Analysis && !(bool)analysisLayerAttribute)
				|| (designLayerAttribute != null && layer == GSATargetLayer.Design && !(bool)designLayerAttribute))
			{
				return false;
			}
			return true;
		}

    protected List<IGSASenderDictionary> GetAssembliesStaticTypes()
    {
      var assemblies = SpeckleInitializer.GetAssemblies().Where(a => a.GetTypes().Any(t => t.GetInterfaces().Contains(typeof(ISpeckleInitializer))));
      var staticObjects = new List<IGSASenderDictionary>();

      //Now obtain the serialised (inheriting from SpeckleObject) objects
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();

        try
        {
          var gsaStatic = types.FirstOrDefault(t => t.GetInterfaces().Contains(typeof(ISpeckleInitializer)) && t.GetProperties().Any(p => p.PropertyType == typeof(IGSACacheForKit)));
          if (gsaStatic != null)
          {
            var dict = (IGSASenderDictionary)gsaStatic.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(IGSASenderDictionary)).GetValue(null);
            //This is how SpeckleGSA finds the objects in the GSASenderObjects dictionary - by finding the first property in ISpeckleInitializer which is of the specific dictionary type
            staticObjects.Add(dict);
          }
        }
        catch (Exception e)
        {
        }
      }
      return staticObjects;
    }
  }
}
