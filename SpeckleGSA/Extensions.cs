using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SpeckleGSA
{
	public static class Extensions
	{
		public static Task ForEachAsync<TSource>(this IEnumerable<TSource> items, Func<TSource, Task> action,	int maxDegreesOfParallelism)
		{
			var actionBlock = new ActionBlock<TSource>(action, new ExecutionDataflowBlockOptions
			{
				MaxDegreeOfParallelism = maxDegreesOfParallelism
			});

			foreach (var item in items)
			{
				actionBlock.Post(item);
			}

			actionBlock.Complete();

			return actionBlock.Completion;
		}

    /// <summary>
    /// Splits lists, keeping entities encapsulated by "" together.
    /// </summary>
    /// <param name="list">String to split</param>
    /// <param name="delimiter">Delimiter</param>
    /// <returns>Array of strings containing list entries</returns>
    public static string[] ListSplit(this string list, string delimiter)
    {
      return Regex.Split(list, delimiter + "(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
    }
  }
}
