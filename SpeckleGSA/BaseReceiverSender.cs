using System;
using System.Collections.Generic;
using System.Linq;
using SpeckleCore;
using SpeckleGSAInterfaces;

namespace SpeckleGSA
{
	public abstract class BaseReceiverSender
	{
		public bool IsInit = false;
		public bool IsBusy = false;

		protected Dictionary<Type, List<Type>> FilteredTypePrerequisites = new Dictionary<Type, List<Type>>();

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

		protected List<Tuple<string, IGSAInterfacer, IGSASettings, Dictionary<Type, List<object>>>> GetAssembliesStaticTypes()
		{
			var assemblies = SpeckleInitializer.GetAssemblies().Where(a => a.GetTypes().Any(t => t.GetInterfaces().Contains(typeof(ISpeckleInitializer))));
			var staticObjects = new List<Tuple<string, IGSAInterfacer, IGSASettings, Dictionary<Type, List<object>>>>();

			//Now obtain the serialised (inheriting from SpeckleObject) objects
			foreach (var ass in assemblies)
			{
				var types = ass.GetTypes();

				try
				{
					var gsaStatic = types.FirstOrDefault(t => t.GetInterfaces().Contains(typeof(ISpeckleInitializer)) && t.GetProperties().Any(p => p.PropertyType == typeof(IGSAInterfacer)));
					if (gsaStatic != null)
					{
						staticObjects.Add(new Tuple<string, IGSAInterfacer, IGSASettings, Dictionary<Type, List<object>>>(
							ass.GetName().ToString(),
							(IGSAInterfacer)gsaStatic.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(IGSAInterfacer)).GetValue(null),
							(IGSASettings)gsaStatic.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(IGSASettings)).GetValue(null),
							(Dictionary<Type, List<object>>)gsaStatic.GetProperties().FirstOrDefault(p => p.PropertyType == typeof(Dictionary<Type, List<object>>)).GetValue(null))
							);
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
