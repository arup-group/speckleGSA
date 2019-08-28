using SpeckleGSAInterfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
  /// <summary>
  /// Static class to store settings.
  /// </summary>
  public class Settings : IGSASettings
	{
    public bool SendOnlyMeaningfulNodes = true;
    public bool SeparateStreams = false;
    public int PollingRate = 2000;

		//Default values for properties specified in the interface
		public string Units { get; set; }
		public GSATargetLayer TargetLayer { get; set; } = GSATargetLayer.Design;
		public double CoincidentNodeAllowance { get; set; } = 0.1;
		public bool SendOnlyResults { get; set; } = false;

		public bool SendResults = false;

		public Dictionary<string, Tuple<int, int, List<string>>> NodalResults { get; set; } = new Dictionary<string, Tuple<int, int, List<string>>>();

		public Dictionary<string, Tuple<int, int, List<string>>> Element1DResults { get; set; } = new Dictionary<string, Tuple<int, int, List<string>>>();

		public Dictionary<string, Tuple<int, int, List<string>>> Element2DResults { get; set; } = new Dictionary<string, Tuple<int, int, List<string>>>();

		public Dictionary<string, Tuple<string, int, int, List<string>>> MiscResults { get; set; } = new Dictionary<string, Tuple<string, int, int, List<string>>>();

		public List<string> ResultCases { get; set; } = new List<string>();

		public bool ResultInLocalAxis { get; set; } = false;

		public int Result1DNumPosition { get; set; } = 3;

		public bool EmbedResults { get; set; } = true;

		/*
		public Dictionary<string, Tuple<int, int, List<string>>> ChosenNodalResult = new Dictionary<string, Tuple<int, int, List<string>>>();
    public Dictionary<string, Tuple<int, int, List<string>>> ChosenElement1DResult = new Dictionary<string, Tuple<int, int, List<string>>>();
		public Dictionary<string, Tuple<int, int, List<string>>> ChosenElement2DResult = new Dictionary<string, Tuple<int, int, List<string>>>();
    public Dictionary<string, Tuple<string, int, int, List<string>>> ChosenMiscResult = new Dictionary<string, Tuple<string, int, int, List<string>>>();
		
    public bool EmbedResults = true;
    public List<string> ResultCases = new List<string>();
    public bool ResultInLocalAxis = false;
    public int Result1DNumPosition = 3;
		*/

		public void SetFieldOrPropValue(string fieldOrPropName, object value)
		{
			FieldOrProp fieldOrProp = FieldOrProp.Unknown;
			object info = null;
			Type fieldOrPropType = null;

			var fieldInfo = typeof(Settings).GetField(fieldOrPropName);
			if (fieldInfo != null)
			{
				fieldOrPropType = fieldInfo.FieldType;
				fieldOrProp = FieldOrProp.Field;
				info = fieldInfo;
			}
			var propInfo = typeof(Settings).GetProperty(fieldOrPropName);
			if (propInfo != null)
			{
				fieldOrPropType = propInfo.PropertyType;
				fieldOrProp = FieldOrProp.Property;
				info = propInfo;
			}
			if (fieldOrPropType == null) return;

			if (typeof(IEnumerable).IsAssignableFrom(fieldOrPropType))
			{
				//Assume all enumerable values are of string type for now
				var pieces = ((string)value).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
				Type subType = fieldOrPropType.GetGenericArguments()[0];

				var newList = Activator.CreateInstance(fieldOrPropType);
				foreach (string p in pieces)
				{
					newList.GetType().GetMethod("Add").Invoke(newList, new object[] { Convert.ChangeType(p, newList.GetType().GetGenericArguments().Single()) });
				}

				SetFieldOrPropValue(fieldOrProp, info, newList);
			}
			else
			{
				SetFieldOrPropValue(fieldOrProp, info, Convert.ChangeType(value, fieldOrPropType));
			}
		}

		private void SetFieldOrPropValue(FieldOrProp fieldOrProp, object info, object value)
		{
			if (fieldOrProp == FieldOrProp.Field)
			{
				((FieldInfo)info).SetValue(this, value);
			}
			else if (fieldOrProp == FieldOrProp.Property)
			{
				((PropertyInfo)info).SetValue(this, value);
			}
			return;
		}

		private enum FieldOrProp
		{
			Unknown = 0,
			Field = 1,
			Property = 2
		}
	}
}
