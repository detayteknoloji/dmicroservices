using System;
using System.Collections;
using System.Collections.Generic;
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
        /// Verilen kaynak nesneyi hedef nesne listesini yaratarak nesnenin özelliklerine set eder.
        /// </summary>
        /// <typeparam name="TSource">kaynak tipi</typeparam>
        /// <typeparam name="TDestination">hedef tipi</typeparam>
        /// <param name="sourceList">kaynak</param>
        /// <returns>yeni bir klonlanmış hedef listesi</returns>
        /// <exception cref="Exception"></exception>
        public static List<TDestination> MapList<TSource, TDestination>(List<TSource> sourceList) where TDestination : new()
        {
            if (sourceList == null)
                return null;

            List<TDestination> destinationList = new List<TDestination>();
            foreach (var source in sourceList)
            {
                TDestination destinationItem = new TDestination();
                foreach (var sourceProperty in source.GetType().GetProperties())
                {
                    PropertyInfo targetProperty = destinationItem.GetType().GetProperty(sourceProperty.Name);
                    if (targetProperty == null)
                        continue;

                    if (sourceProperty.PropertyType.IsPrimitive || sourceProperty.PropertyType == typeof(string) || sourceProperty.PropertyType.IsValueType)
                    {
                        MapProperty(sourceProperty, source, destinationItem, targetProperty);
                    }
                    else if (sourceProperty.PropertyType.IsGenericType && sourceProperty.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var tempSourceList = (IList)sourceProperty.GetValue(source);
                        if (tempSourceList == null)
                            continue;

                        var listGenericType = sourceProperty.PropertyType.GetGenericArguments()[0];

                        if (sourceProperty.PropertyType.GetGenericArguments().Length != 1)
                            throw new Exception($"Model içerisindeki {sourceProperty?.Name} isimli liste tek tipli olmalı!");

                        var listType = typeof(List<>).MakeGenericType(listGenericType);
                        var clonedList = (IList)Activator.CreateInstance(listType);

                        foreach (var item in tempSourceList)
                        {
                            clonedList.Add(item);
                        }

                        MapProperty(sourceProperty, clonedList, destinationItem, targetProperty, clonedList);
                    }
                }

                destinationList.Add(destinationItem);
            }

            return destinationList;
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
        /// <param name="sourceValue">liste tipleri için önceden hazırlanmış değerleri.</param>
        private static void MapProperty(PropertyInfo sourceProperty, object source, object target, PropertyInfo targetProperty, object sourceValue = null)
        {
            if (source == null)
            {
                return;
            }

            if (sourceProperty.CanRead && targetProperty.CanWrite)
            {
                sourceValue = sourceValue ?? sourceProperty.GetValue(source);
                if (targetProperty.PropertyType == sourceProperty.PropertyType && sourceValue != null)
                    targetProperty.SetValue(target, sourceValue);
            }
        }
    }
}
