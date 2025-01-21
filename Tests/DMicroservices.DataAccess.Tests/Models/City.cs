using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DMicroservices.DataAccess.Tests.Models
{
    [Table("City")]
    public class City
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public int CompanyNo { get; set; }

        [MaxLength(5)]
        public string Name { get; set; }

        public ICollection<Person> Persons { get; set; }   

    }
}
