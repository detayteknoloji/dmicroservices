using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DMicroservices.DataAccess.Tests.Models
{
    [Table("Teacher")]
    public class Teacher : Person
    {
        public int Branch { get; set; }

    }
}
