using System;
using System.Linq;
using System.Reflection;

namespace DMicroservices.Utils.ObjectMapper
{
    public class ObjectMapper
    {
        /// <summary>
        /// Verilen kaynak nesneyi hedef nesnenin özelliklerine set eder.
        /// </summary>
        /// <param name="target">Hedef</param>
        /// <param name="source">Kaynak</param>
        public static void Map(object target, object source)
        {
            if (source != null && target != null)
            {
                PropertyInfo[] targetProperties = target.GetType().GetProperties();
                foreach (var sourceProperty in source.GetType().GetProperties())
                {
                    PropertyInfo targetProperty = targetProperties.FirstOrDefault(x => x.Name == sourceProperty.Name);
                    if (targetProperty == null)
                        continue;

                    MapProperty(sourceProperty, source, target, targetProperty);
                }
            }
        }

        /// <summary>
        /// Verilen kaynak nesneyi hedef nesne yaratarak nesnenin özelliklerine set eder.
        /// </summary> 
        /// <param name="source">Kaynak</param>
        public static T Map<T>(object source) where T : class
        {
            if (source == null)
                return null;

            object target = Activator.CreateInstance(typeof(T));
            PropertyInfo[] targetProperties = target.GetType().GetProperties();

            foreach (var sourceProperty in source.GetType().GetProperties())
            {
                PropertyInfo targetProperty = targetProperties.FirstOrDefault(x => x.Name == sourceProperty.Name);
                if (targetProperty == null)
                    continue;

                MapProperty(sourceProperty, source, target, targetProperty);
            }
            return (T)target;
        }

        /// <summary>
        /// Verilen kaynak nesneyi hedef nesnenin özelliklerine set eder. Exclude parametresi ile verilen özellikler atanmaz.
        /// </summary>
        /// <param name="target">Hedef</param>
        /// <param name="source">Kaynak</param>
        /// <param name="exclude">Atanmayacak Özellikler</param>
        public static void MapExclude(object target, object source, params string[] exclude)
        {
            if (source != null && target != null)
            {
                PropertyInfo[] targetProperties = target.GetType().GetProperties();
                foreach (var sourceProperty in source.GetType().GetProperties())
                {
                    if (exclude.Contains(sourceProperty.Name))
                        continue;

                    PropertyInfo targetProperty = targetProperties.FirstOrDefault(x => x.Name == sourceProperty.Name);
                    if (targetProperty == null)
                        continue;

                    MapProperty(sourceProperty, source, target, targetProperty);
                }
            }
        }

        /// <summary>
        /// Propertyinin değerini set eder.
        /// </summary>
        /// <param name="sourceProperty"></param>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="targetProperty"></param>
        private static void MapProperty(PropertyInfo sourceProperty, object source, object target, PropertyInfo targetProperty)
        {
            object soruceValue = sourceProperty.GetValue(source);
            if (targetProperty.PropertyType == sourceProperty.PropertyType && soruceValue != null)
                targetProperty.SetValue(target, sourceProperty.GetValue(source));

        }
    }
}
