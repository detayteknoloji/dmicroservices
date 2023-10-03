using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DMicroservices.Utils.Extensions
{
    /// <summary>
    /// https://stackoverflow.com/a/49597502
    /// </summary>
    public static partial class DatabaseExtensions
    {
        public static IQueryable<T> Include<T>(this IQueryable<T> source, IEnumerable<string> navigationPropertyPaths)
            where T : class
        {
            return navigationPropertyPaths.Aggregate(source, (query, path) => query.Include(path));
        }

        public static IEnumerable<string> GetIncludePaths(this DbContext context, Type clrEntityType)
        {
            var entityType = context.Model.FindEntityType(clrEntityType);
            var includedNavigation = new HashSet<INavigation>();
            var stack = new Stack<IEnumerator<INavigation>>();
            while (true)
            {
                var entityNavigation = new List<INavigation>();
                foreach (var navigation in entityType.GetNavigations())
                {
                    if (includedNavigation.Add(navigation))
                        entityNavigation.Add(navigation);
                }
                if (entityNavigation.Count == 0)
                {
                    if (stack.Count > 0)
                        yield return string.Join(".", stack.Reverse().Select(e => e.Current.Name));
                }
                else
                {
                    foreach (var navigation in entityNavigation)
                    {

                        var inverseNavigation = navigation.Inverse;
                        if (inverseNavigation != null)
                            includedNavigation.Add(inverseNavigation);
                    }
                    stack.Push(entityNavigation.GetEnumerator());
                }
                while (stack.Count > 0 && !stack.Peek().MoveNext())
                    stack.Pop();
                if (stack.Count == 0) break;

                entityType = stack.Peek().Current.TargetEntityType;
            }
        }

    }
}
