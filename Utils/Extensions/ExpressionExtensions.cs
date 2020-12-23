using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DMicroservices.Utils.Extensions
{ 
    public static class ExpressionExtensions
    {
        /// <summary>
        /// Given the key column name in item type.
        /// </summary>
        /// <param name="item">Requested key name in this item</param>
        /// <returns></returns>
        public static string GetIdentifierColumnName(this Type item)
        {
            foreach (var propertyInfo in item.GetProperties())
            {
                foreach (var propertyInfoCustomAttribute in propertyInfo.CustomAttributes)
                {
                    if (propertyInfoCustomAttribute.AttributeType.Name.Equals("KeyAttribute"))
                        return propertyInfo.Name;
                }
            }

            throw new KeyNotFoundException("The object was not contains key property.");
        }

        /// <summary>
        /// Given the created expression by identifier column.
        /// </summary>
        /// <returns></returns>
        public static Expression<Func<T, bool>> GetIdentifierExpression<T>(this long id)
        {
            ParameterExpression argParams = Expression.Parameter(typeof(T), "x");
            Expression filterProp = Expression.Property(argParams, typeof(T).GetIdentifierColumnName());
            ConstantExpression filterValue = Expression.Constant(id);
            return Expression.Lambda<Func<T, bool>>(Expression.Equal(filterProp, filterValue), argParams);
        }
    }
}
