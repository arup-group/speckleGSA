using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSAInterfaces
{
  public interface IGSASenderDictionary
  {
    bool Add<T>(T o);

    bool AddRange<T>(List<T> os);

    int Count<T>();

    List<T> Get<T>();
    Dictionary<Type, List<object>> GetAll();

    void RemoveAll<T>(List<T> os);

    void Clear();
  }
}
