using System;
using System.Collections.Generic;
using SpeckleGSAInterfaces;

namespace SpeckleGSA
{
	public abstract class BaseReceiverSender
	{
		public bool IsInit = false;
		public bool IsBusy = false;

		protected Dictionary<Type, List<Type>> FilteredTypePrerequisites = new Dictionary<Type, List<Type>>();

		protected bool ObjectTypeMatchesLayer(Type t, Type attributeType)
		{
			var analysisLayerAttribute = t.GetAttribute("AnalysisLayer", attributeType);
			var designLayerAttribute = t.GetAttribute("DesignLayer", attributeType);

			//If an object type has a layer attribute exists and its boolean value doesn't match the settings target layer, then it doesn't match.  This could be reviewed and simplified.
			if ((analysisLayerAttribute != null && GSA.Settings.TargetLayer == GSATargetLayer.Analysis && !(bool)analysisLayerAttribute)
				|| (designLayerAttribute != null && GSA.Settings.TargetLayer == GSATargetLayer.Design && !(bool)designLayerAttribute))
			{
				return false;
			}
			return true;
		}
	}
}
