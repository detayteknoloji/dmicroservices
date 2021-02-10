using DMicroservices.Utils.Extensions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DMicroservices.DataAccess.DynamicQuery
{
    /// <summary>
    /// Veri getirmek için kullanılan dto.
    /// </summary>
    public class SelectDto<T, D> : IDisposable
        where T : class
        where D : DbContext
    {
        private ConstantExpression ZeroConstant = Expression.Constant(0);
        private ConstantExpression OneConstant = Expression.Constant(1);
        private ConstantExpression MinusOneConstant = Expression.Constant(-1);
        private ConstantExpression TrueConstant = Expression.Constant(true);
        private MethodInfo ToLowerMethod = typeof(String).GetMethod("ToLower", new Type[] { });
        private MethodInfo ToUpperMethod = typeof(String).GetMethod("ToUpper", new Type[] { });



        private MethodCallExpression CalledExpression;

        /// <summary>
        /// Filtreleme yapılacak olan alanlar.
        /// </summary>
        public List<FilterItemDto> Filter { get; set; }

        /// <summary>
        /// Filtrelerin birleştirilme şekli
        /// AND
        /// OR
        /// </summary>
        public string FilterCompareType { get; set; }

        /// <summary>
        /// Filtrelerin gruplarının kendi aralarında birleştirilmesi için gerekli tip.
        /// </summary>
        public List<FilterCompareTypesItem> FilterCompareTypes { get; set; }

        /// <summary>
        /// Geri dönüş tipi
        /// </summary>
        public string ReturnDtoType { get; set; }

        /// <summary>
        /// Belirtilen listeyi sunucu tarafında sıralamak için kullanılır.
        /// Gönderilen sıraya göre ThenBy yapılarak getirilir.
        /// </summary>
        public List<OrderItemDto> Sort { get; set; }

        /// <summary>
        /// Atlanılacak kayıt sayısı
        /// </summary>
        public int SkipCount { get; set; }

        /// <summary>
        /// Alınacak kayıt sayısı
        /// </summary>
        public int TakeCount { get; set; }

        /// <summary>
        /// Layout gelsin mi?
        /// </summary>
        public bool GetLayout { get; set; }

        /// <summary>
        /// Layout çekilecekse layot verilerinin dili.
        /// </summary>
        public string LayoutLanguage { get; set; }

        /// <summary>
        /// Verilen nesne ile ilgili expression oluşturur.
        /// </summary>
        /// <returns></returns>
        public Expression<Func<T, bool>> GetExpression()
        {
            return GetExpression<T>();
        }

        /// <summary>
        /// Verilen nesne ile ilgili expression aldığı tipe göre oluşturulur.
        /// </summary>
        /// <typeparam name="TO"></typeparam>
        /// <returns></returns>
        public Expression<Func<TO, bool>> GetExpression<TO>()
        {
            ParameterExpression argParams = Expression.Parameter(typeof(TO), "x");
            BinaryExpression complateBinaryExp = null;
            if (Filter == null)
                return x => true;

            var availableProperties = typeof(TO).GetProperties();

            foreach (var filterItemDtos in Filter.GroupBy(x => x.Group))
            {
                BinaryExpression groupComplateBinaryExp = null;

                List<FilterItemDto> localFilters = Filter.Where(x => x.Group == filterItemDtos.Key).ToList();
                for (var i = localFilters.Count - 1; i >= 0; i--)
                {
                    if (availableProperties.All(x => x.Name != localFilters[i].PropertyName))
                        continue;

                    if (!string.IsNullOrEmpty(localFilters[i].TableObject) && !typeof(TO).Name.Equals(localFilters[i].TableObject))
                        continue;

                    BinaryExpression filterExpression = GetBinaryExpression(argParams, localFilters[i]);

                    if (FilterCompareTypes != null)
                    {
                        FilterCompareTypesItem groupCompareType =
                            FilterCompareTypes.FirstOrDefault(x => x.Group == filterItemDtos.Key);
                        if (groupCompareType == null)
                        {
                            groupComplateBinaryExp =
                                GetCompareBinaryExpression(FilterCompareType, groupComplateBinaryExp, filterExpression);
                        }
                        else
                        {
                            groupComplateBinaryExp =
                                GetCompareBinaryExpression(groupCompareType.Type, groupComplateBinaryExp,
                                    filterExpression);
                        }
                    }
                    else
                    {
                        groupComplateBinaryExp =
                            GetCompareBinaryExpression(FilterCompareType, groupComplateBinaryExp, filterExpression);
                    }
                }
                if (groupComplateBinaryExp != null)
                    complateBinaryExp =
                    GetCompareBinaryExpression(FilterCompareType, complateBinaryExp, groupComplateBinaryExp);
            }

            if (complateBinaryExp != null)
                return Expression.Lambda<Func<TO, bool>>(complateBinaryExp, argParams);

            return x => true;
        }

        /// <summary>
        /// Sorgu nesnesini filtrelenmiş ve sıralanmış olarak oluşturur.
        /// </summary>
        /// <param name="uow">Açılmış olan veritabanı bağlantısı</param>
        /// <param name="additionalExpression">Ek filtre sorgusu yazılması gerekiyorsa yazılmalıdır.</param>
        /// <returns></returns>
        public IQueryable<T> GetQueryObject(UnitOfWork.UnitOfWork<D> uow, Expression<Func<T, bool>> additionalExpression = null)
        {
            IQueryable<T> queryObject = uow.GetRepository<T>().GetAll(GetExpression());
            if (additionalExpression != null)
                queryObject = queryObject.Where(additionalExpression);
            if (Sort != null)
            {
                IOrderedQueryable<T> orderedQueryable = (IOrderedQueryable<T>)queryObject;
                for (int i = 0; i < Sort.Count; i++)
                {
                    orderedQueryable = i == 0 ? GetOrderQueryable(queryObject, Sort[i]) : GetOrderQueryable(orderedQueryable, Sort[i]);
                }
                queryObject = orderedQueryable;
            }
            return queryObject;
        }

        /// <summary>
        /// Sorgu nesnesini ilişkileriyle, filtrelenmiş ve sıralanmış olarak oluşturur.
        /// </summary>
        /// <param name="uow">Açılmış olan veritabanı bağlantısı</param>
        /// <param name="additionalExpression">Ek filtre sorgusu yazılması gerekiyorsa yazılmalıdır.</param>
        /// <returns></returns>
        public IQueryable<T> GetQueryObject(UnitOfWork.UnitOfWork<D> uow, List<string> includePaths, Expression<Func<T, bool>> additionalExpression = null)
        {
            IQueryable<T> queryObject = uow.GetRepository<T>().GetAll(GetExpression()).Include(includePaths);
            if (additionalExpression != null)
                queryObject = queryObject.Where(additionalExpression);
            if (Sort != null)
            {
                IOrderedQueryable<T> orderedQueryable = (IOrderedQueryable<T>)queryObject;
                for (int i = 0; i < Sort.Count; i++)
                {
                    orderedQueryable = i == 0 ? GetOrderQueryable(queryObject, Sort[i]) : GetOrderQueryable(orderedQueryable, Sort[i]);
                }
                queryObject = orderedQueryable;
            }
            return queryObject;
        }

        /// <summary>
        /// Daha önce oluşturulmuş olan query nesnesini limitlendirir ve liste olarak döndürür.
        /// </summary>
        /// <param name="queryableObject">Açılmış olan veritabanı bağlantısı</param> 
        /// <returns></returns>
        public List<T> SetPager(IQueryable<T> queryableObject)
        {
            if (TakeCount > 0)
                queryableObject = queryableObject.Take(SkipCount + TakeCount);

            List<T> queryList = queryableObject.ToList();
            if (SkipCount > 0)
                queryList = queryList.Skip(SkipCount).ToList();

            return queryList;
        }

        /// <summary>
        /// BinaryExpression oluştururup döndürür.
        /// </summary>
        /// <param name="argParams"></param>
        /// <param name="filterItem"></param>
        /// <returns></returns>
        private BinaryExpression GetBinaryExpression(ParameterExpression argParams, FilterItemDto filterItem)
        {
            BinaryExpression filterExpression = null;

            Expression filterProp = Expression.Property(argParams, filterItem.PropertyName); //x.BKTX

            if (filterItem.ConversionMethodName != null && filterItem.ConversionMethodName.Equals("ToLower"))
                filterProp = Expression.Call(filterProp, ToLowerMethod);
            else if (filterItem.ConversionMethodName != null && filterItem.ConversionMethodName.Equals("ToUpper"))
                filterProp = Expression.Call(filterProp, ToUpperMethod);

            ConstantExpression filterValue = Expression.Constant(null);

            if (filterItem.Operation != "IN")
            {
                filterValue = GetConstanstValue(filterProp.Type, filterItem.PropertyValue);
            }

            switch (filterItem.Operation)
            {
                case "CT":
                    {
                        MethodInfo ContainsMethod = filterProp.Type.GetMethod("Contains", new[] { filterProp.Type });
                        CalledExpression = Expression.Call(filterProp, ContainsMethod, filterValue); // x.BKTX.ToUpper().Contains("xx")
                        filterExpression = Expression.MakeBinary(ExpressionType.Equal, CalledExpression, TrueConstant);
                        break;
                    }
                case "IN":
                    {
                        foreach (var propertyValueItem in filterItem.PropertyValue.Split(','))
                        {
                            filterValue = GetConstanstValue(filterProp.Type, propertyValueItem);

                            if (filterExpression != null)
                                filterExpression = Expression.Or(filterExpression, Expression.Equal(filterProp, filterValue));
                            else
                                filterExpression = Expression.Equal(filterProp, filterValue);
                        }
                        break;
                    }
                case "GT":
                    {
                        MethodInfo CompareToMethod = filterProp.Type.GetMethod("CompareTo", new[] { filterProp.Type });
                        CalledExpression = Expression.Call(filterProp, CompareToMethod, filterValue);
                        filterExpression = Expression.MakeBinary(ExpressionType.GreaterThanOrEqual, CalledExpression, ZeroConstant);
                        break;
                    }
                case "LT":
                    {
                        MethodInfo CompareToMethod = filterProp.Type.GetMethod("CompareTo", new[] { filterProp.Type });
                        CalledExpression = Expression.Call(filterProp, CompareToMethod, filterValue);
                        filterExpression = Expression.MakeBinary(ExpressionType.LessThanOrEqual, CalledExpression, ZeroConstant);
                        break;
                    }
                case "NE":
                    {
                        if (filterProp.Type == typeof(bool))
                        {
                            MethodInfo CompareToMethod = filterProp.Type.GetMethod("CompareTo", new[] { filterProp.Type });
                            CalledExpression = Expression.Call(filterProp, CompareToMethod, filterValue);
                            filterExpression = Expression.Or(Expression.MakeBinary(ExpressionType.Equal, CalledExpression, MinusOneConstant), Expression.MakeBinary(ExpressionType.Equal, CalledExpression, OneConstant));
                        }
                        else
                        {
                            filterExpression = Expression.NotEqual(filterProp, filterValue);
                        }
                        break;
                    }
                default:
                    {
                        if (filterProp.Type == typeof(bool))
                        {
                            MethodInfo CompareToMethod = filterProp.Type.GetMethod("CompareTo", new[] { filterProp.Type });
                            CalledExpression = Expression.Call(filterProp, CompareToMethod, filterValue);
                            filterExpression = Expression.And(Expression.MakeBinary(ExpressionType.NotEqual, CalledExpression, MinusOneConstant), Expression.MakeBinary(ExpressionType.NotEqual, CalledExpression, OneConstant));
                        }
                        else
                        {
                            filterExpression = Expression.Equal(filterProp, filterValue);
                        }
                        break;
                    }
            }

            return filterExpression;
        }

        /// <summary>
        /// Expressionları comparetype a göre birleştirir.
        /// </summary>
        /// <returns></returns>
        private BinaryExpression GetCompareBinaryExpression(string compareType, BinaryExpression complateBinaryExp, BinaryExpression filterExpression)
        {
            if (compareType == "OR")
            {
                return complateBinaryExp != null
                    ? Expression.Or(complateBinaryExp, filterExpression)
                    : filterExpression;
            }

            return complateBinaryExp != null
                ? Expression.And(complateBinaryExp, filterExpression)
                : filterExpression;
        }

        /// <summary>
        /// Sıralanmış Queryble nesnesini getirir.
        /// </summary> 
        /// <returns></returns>
        private IOrderedQueryable<T> GetOrderQueryable(IQueryable<T> queryable, OrderItemDto orderItem)
        {
            IOrderedQueryable<T> orderedQueryable;

            if (orderItem.Descending)
                orderedQueryable = queryable.OrderByDescending(GetOrderBinaryExpression(orderItem.Column));
            else
                orderedQueryable = queryable.OrderBy(GetOrderBinaryExpression(orderItem.Column));

            return orderedQueryable;
        }

        /// <summary>
        /// Sıralanmış Queryble nesnesini getirir.
        /// </summary> 
        /// <returns></returns>
        private IOrderedQueryable<T> GetOrderQueryable(IOrderedQueryable<T> queryable, OrderItemDto orderItem)
        {
            if (!string.IsNullOrEmpty(orderItem.Column))
            {
                if (orderItem.Descending)
                    queryable = queryable.ThenByDescending(GetOrderBinaryExpression(orderItem.Column));
                else
                    queryable = queryable.ThenBy(GetOrderBinaryExpression(orderItem.Column));
            }
            return queryable;
        }

        /// <summary>
        /// Sıralamak için BinaryExpression oluştururup döndürür.
        /// </summary>
        /// <returns></returns>
        private Expression<Func<T, dynamic>> GetOrderBinaryExpression(string columnName)
        {
            string paramName = string.Format("{0}_SORT", columnName);
            var param = Expression.Parameter(typeof(T), paramName);

            PropertyInfo sortProperty = typeof(T).GetProperty(columnName);
            if (sortProperty == null)
                sortProperty = typeof(T).GetProperties().First();

            return Expression.Lambda<Func<T, object>>
                (Expression.Convert(Expression.Property(param, sortProperty.Name), sortProperty.PropertyType), param);
        }

        /// <summary>
        /// Expression karşılaştırma sabitini getirir.
        /// </summary>
        /// <returns></returns>
        private ConstantExpression GetConstanstValue(Type filterType, string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue) || string.IsNullOrEmpty(propertyValue))
                return Expression.Constant(null);

            TypeConverter converter = TypeDescriptor.GetConverter(filterType);
            object result = converter.ConvertFrom(propertyValue);
            return Expression.Constant(result, filterType);
        }

        public SelectDto()
        {
            if (string.IsNullOrEmpty(LayoutLanguage))
                LayoutLanguage = "T";
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}
