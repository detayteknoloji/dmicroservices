namespace DMicroservices.DataAccess.DynamicQuery
{
    /// <summary>
    /// Filtreleme yapmak için rest tarafından gönderilen veri ögesi
    /// </summary>
    public class FilterItemDto
    {
        /// <summary>
        /// Filtreleme yapılması için özellik adı.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Filtreleme yapılacak işlem<br />
        /// "EQ" ->equals<br />
        /// "IN" ->in<br />
        /// "CT" ->contains<br />
        /// "LT" ->less than<br />
        /// "GT" ->grater than<br />
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Filtreleme yapılacak value
        /// </summary>
        public string PropertyValue { get; set; }

        /// <summary>
        /// Karşılaştırma yapılırken PropertyName tarafında uygulanacak olan metod.<br />
        /// "ToLower" -> PropertyName.ToLower() <br />
        /// "ToUpper" -> PropertyName.ToUpper() <br />
        /// </summary>
        public string ConversionMethodName { get; set; }

        /// <summary>
        /// Mantıksal grup ismi.
        /// Gruplamak / AND or işlemini gruba uygulamak için kullanılır.
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Bir kolon birden fazla tabloda kullanılıyorsa ve bu kolonun iki tabloda birden aynı filtre ile sorgu çekilmesi gerekmiyorsa bu alan
        /// BNT_&lt;TABLO ADI&gt; şeklinde belirtilir.
        /// </summary>
        public string TableObject { get; set; }
    }
}
