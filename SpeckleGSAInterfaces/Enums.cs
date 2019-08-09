using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAInterfaces
{

	public enum GSATargetLayer
	{
		Design,
		Analysis
	}

	public enum GSAEntity
	{
		NODE = 1,
		ELEMENT = 2,
		MEMBER = 3,
		LINE = 6,
		AREA = 7,
		REGION = 8
	}
}
