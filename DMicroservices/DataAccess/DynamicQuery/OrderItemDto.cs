
namespace DMicroservices.DataAccess.DynamicQuery
{
    /// <summary>
    /// Sıralama yapmak için gerekli nesne
    /// </summary>
    public class OrderItemDto
    {
        /// <summary>
        /// Filtreleme yapılması için özellik adı.
        /// </summary>
        public string Column { get; set; }

        /// <summary>
        /// Descending olarak sırala?
        /// </summary>
        public bool Descending { get; set; }
    }
}
