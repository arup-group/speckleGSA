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

		protected Dictionary<Type, List<Type>> FilteredWriteTypePrerequisites = new Dictionary<Type, List<Type>>();
    protected Dictionary<Type, List<Type>> FilteredReadTypePrerequisites = new Dictionary<Type, List<Type>>();
    
    protected List<string> GetFilteredKeywords()
    {
      var keywords = new List<string>();
      keywords.AddRange(GetFilteredKeywords(FilteredWriteTypePrerequisites));
      keywords.AddRange(GetFilteredKeywords(FilteredReadTypePrerequisites));

      return keywords;
    }

    private List<string> GetFilteredKeywords(Dictionary<Type, List<Type>> prereqs)
    {
      var keywords = new List<string>();
      foreach (var kvp in prereqs)
      {
        try
        {
          var keyword = (string)kvp.Key.GetAttribute("GSAKeyword");
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
        catch { }
      }
      return keywords;
    }



		protected bool ObjectTypeMatchesLayer(Type t)
		{
			var attributeType = typeof(GSAObject);
			var analysisLayerAttribute = t.GetAttribute("AnalysisLayer");
			var designLayerAttribute = t.GetAttribute("DesignLayer");

			//If an object type has a layer attribute exists and its boolean value doesn't match the settings target layer, then it doesn't match.  This could be reviewed and simplified.
			if ((analysisLayerAttribute != null && GSA.Settings.TargetLayer == GSATargetLayer.Analysis && !(bool)analysisLayerAttribute)
				|| (designLayerAttribute != null && GSA.Settings.TargetLayer == GSATargetLayer.Design && !(bool)designLayerAttribute))
			{
				return false;
			}
			return true;
		}

    protected List<Dictionary<Type, List<object>>> GetAssembliesStaticTypes()
    {
      var assemblies = SpeckleInitializer.GetAssemblies().Where(a => a.GetTypes().Any(t => t.GetInterfaces().Contains(typeof(ISpeckleInitializer))));
      var staticObjects = new List<Dictionary<Type, List<object>>>();

      //Now obtain the serialised (inheriting from SpeckleObject) objects
      foreach (var ass in assemblies)
      {
        var types = ass.GetTypes();

        try
        {
          var gsaStatic = types.FirstOrDefault(t => t.GetInterfaces().Contains(typeof(ISpeckleInitializer)) && t.GetProperties().Any(p => p.PropertyType == typeof(IGSACacheForKit)));
          if (gsaStatic != null)
          {
            var dict = (Dictionary<Type, List<object>>)gsaStatic.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(Dictionary<Type, List<object>>)).GetValue(null);
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
