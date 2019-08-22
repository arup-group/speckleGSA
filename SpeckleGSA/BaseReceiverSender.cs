using SpeckleGSAInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
	public abstract class BaseReceiverSender
	{
		public bool IsInit = false;
		public bool IsBusy = false;

		protected Dictionary<Type, List<Type>> FilteredTypePrerequisites = new Dictionary<Type, List<Type>>();

		protected bool Initialise(string stream = null)
		{
			if (IsInit) return false;

			if (!GSA.IsInit)
			{
				Status.AddError("GSA link not found.");
				return false;
			}

			var attributeType = typeof(GSAObject);

			//Filter out prerequisites that are excluded by the layer selection
			// Remove wrong layer objects from prerequisites
			foreach (var kvp in GSA.TypePrerequisites)
			{
				FilteredTypePrerequisites[kvp.Key] = kvp.Value.Where(l => ObjectTypeMatchesLayer(l, attributeType) 
					&& ((stream == null) ? true : (string)l.GetAttribute("stream", attributeType) == stream)).ToList();
			}
			return true;
		}

		private bool ObjectTypeMatchesLayer(Type t, Type attributeType)
		{
			var analysisLayerAttribute = t.GetAttribute("AnalysisLayer", attributeType);
			var designLayerAttribute = t.GetAttribute("DesignLayer", attributeType);

			//If an object type has a layer attribute exists and its boolean value doesn't match the settings target layer, then it doesn't match.  This could be reviewed and simplified.
			if ((analysisLayerAttribute != null && GSA.Settings.TargetAnalysisLayer && !(bool)analysisLayerAttribute)
				|| (designLayerAttribute != null && GSA.Settings.TargetDesignLayer && !(bool)designLayerAttribute))
			{
				return false;
			}
			return true;
		}
	}
}
