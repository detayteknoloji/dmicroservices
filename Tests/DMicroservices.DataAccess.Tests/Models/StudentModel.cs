using System.ComponentModel.DataAnnotations.Schema;

namespace DMicroservices.DataAccess.Tests.Models
{
    [Table("Student", Schema = "person")]
    public class StudentModel
    {
        public long Id { get; set; }

        public string Name { get; set; }
        public string Surname { get; set; }

    }
}
