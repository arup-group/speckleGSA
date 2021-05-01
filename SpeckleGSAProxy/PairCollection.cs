using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSAProxy
{
  //This is designed to maximise performance when speed of searching values (i.e. as opposed to keys) is important.  It is trying to be faster than a SortedList
  //Note: it assumes both keys and values are unique
  internal class PairCollection<U, V> : IPairCollection<U, V> where U : IComparable<U> where V : IComparable<V>
  {
    private readonly List<U> leftKeys = new List<U>();
    private readonly Dictionary<U, int> lefts = new Dictionary<U, int>();
    private readonly List<V> rightKeys = new List<V>();
    private readonly Dictionary<V, int> rights = new Dictionary<V, int>();
    private int? highestIndex = null;
    private readonly object dictLock = new object();

    private U maxLeft;
    private V maxRight;

    public bool ContainsLeft(U u)
    {
      lock (dictLock)
      {
        return lefts.ContainsKey(u);
      }
    }

    public bool ContainsRight(V v)
    {
      lock (dictLock)
      {
        return rights.ContainsKey(v);
      }
    }

    public U MaxLeft() => maxLeft;
    public V MaxRight() => maxRight;

    public List<U> Lefts
    {
      get
      {
        lock (dictLock)
        {
          return leftKeys.ToList();
        }
      }
    }

    public List<V> Rights
    {
      get
      {
        lock (dictLock)
        {
          return rightKeys.ToList();
        }
      }
    }

    public void Add(U u, V v)
    {
      lock (dictLock)
      {
        leftKeys.Add(u);
        rightKeys.Add(v);
        var index = IncrementHighestIndex();
        lefts.Add(u, index);
        rights.Add(v, index);
        if (index == 0)
        {
          maxLeft = u;
          maxRight = v;
        }
        else
        {
          if (u.CompareTo(maxLeft) > 0)
          {
            maxLeft = u;
          }
          if (v.CompareTo(maxRight) > 0)
          {
            maxRight = v;
          }
        }
      }
    }

    public void Clear()
    {
      lock (dictLock)
      {
        lefts.Clear();
        rights.Clear();
        highestIndex = null;
        maxLeft = default;
        maxRight = default;
      }
    }

    public int Count()
    {
      lock (dictLock)
      {
        return highestIndex.HasValue? highestIndex.Value + 1 : 0;
      } 
    }

    public bool FindLeft(V v, out U u)
    {
      lock (dictLock)
      {
        if (rights.ContainsKey(v))
        {
          var index = rights[v];
          u = leftKeys[index];
          return true;
        }
        u = default;
        return false;
      }
    }

    public bool FindRight(U u, out V v)
    {
      lock (dictLock)
      {
        if (lefts.ContainsKey(u))
        {
          var index = lefts[u];
          v = rightKeys[index];
          return true;
        }
        v = default;
        return false;
      }
    }

    public void RemoveLeft(U u)
    {
      lock (dictLock)
      {
        if (!lefts.ContainsKey(u))
        {
          return;
        }
        var index = lefts[u];
        var v = rightKeys[index];
        Remove(u, v);
      }
    }

    public void RemoveRight(V v)
    {
      lock (dictLock)
      {
        if (!rights.ContainsKey(v))
        {
          return;
        }
        var index = rights[v];
        var u = leftKeys[index];
        Remove(u, v);
      }
    }

    #region inside_lock_private_fns
    private void Remove(U u, V v)
    {
      lefts.Remove(u);
      rights.Remove(v);
      leftKeys.Remove(u);
      rightKeys.Remove(v);
      highestIndex = (highestIndex == 0) ? null : highestIndex - 1;
      if (u.CompareTo(maxLeft) == 0)
      {
        maxLeft = leftKeys.Count() == 0 ? default : leftKeys.Max();
      }
      if (v.CompareTo(maxRight) == 0)
      {
        maxRight = rightKeys.Count() == 0 ? default : rightKeys.Max();
      }
    }

    private int IncrementHighestIndex()
    {
      highestIndex = (highestIndex.HasValue) ? highestIndex.Value + 1 : 0;
      return highestIndex.Value;
    }
    #endregion
  }
}
