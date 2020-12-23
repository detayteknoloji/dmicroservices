using DMicroservices.DataAccess.DynamicQuery.Enum;

namespace DMicroservices.DataAccess.DynamicQuery
{
    public class SortItemDto
    {
        /// <summary>
        /// Sıralama yapılacak olan özellik.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Sıralama Türü
        /// </summary>
        public SortTypeEnum SortType { get; set; }
    }
}
