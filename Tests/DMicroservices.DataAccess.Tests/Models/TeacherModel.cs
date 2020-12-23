using System.ComponentModel.DataAnnotations.Schema;

namespace DMicroservices.DataAccess.Tests.Models
{
    [Table("Teacher", Schema = "person")]
    public class TeacherModel
    {
        public long Id { get; set; }

        public string Name { get; set; }
        public string Surname { get; set; }

    }
}
