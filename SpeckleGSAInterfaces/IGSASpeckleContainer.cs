using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAInterfaces
{
	public interface IGSASpeckleContainer
	{
		/// <summary>
		/// Record index of GSA record
		/// </summary>
		int GSAId { get; set; }

		/// <summary>
		/// Associated GWA command.
		/// </summary>
		string GWACommand { get; set; }

		/// <summary>
		/// List of GWA records used to read the object.
		/// </summary>
		List<string> SubGWACommand { get; set; }

		/// <summary>
		/// SpeckleObject created
		/// </summary>
		dynamic Value { get; set; }
	}
}
