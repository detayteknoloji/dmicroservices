using System.ComponentModel.DataAnnotations.Schema;

namespace DMicroservices.DataAccess.Tests.Models
{
    [Table("Document", Schema = "school")]
    public class DocumentModel
    {
        public long Id { get; set; }

        public string Name { get; set; }

    }
}
