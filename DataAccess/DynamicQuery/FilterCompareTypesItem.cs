namespace DMicroservices.DataAccess.DynamicQuery
{
    public class FilterCompareTypesItem
    {
        /// <summary>
        /// Mantıksal grup ismi.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Grubun AND/OR şeklinde birleştirilmesi için AND ve ya OR bağlacı
        /// AND/OR
        /// </summary>
        public string Type { get; set; }
    }
}
