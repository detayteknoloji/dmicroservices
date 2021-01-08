using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DMicroservices.DataAccess.Tests.Models
{
    [Table("Student")]
    public class Student : Person
    {
        public long StudentNum { get; set; }

    }
}
