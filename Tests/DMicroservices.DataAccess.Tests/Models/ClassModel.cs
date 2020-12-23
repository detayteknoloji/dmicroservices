using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DMicroservices.DataAccess.Tests.Models
{
    [Table("Class", Schema = "school")]
    public class ClassModel
    {
        public long Id { get; set; }
        public string Name { get; set; }

        public ICollection<StudentModel> Students { get; set; }
    }
}
