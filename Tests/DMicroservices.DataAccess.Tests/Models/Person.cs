using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using DMicroservices.Base.Attributes;

namespace DMicroservices.DataAccess.Tests.Models
{
    [Table("Person")]
    public class Person
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        
      //  [DisableChangeTrack]
        public string Name { get; set; }

        public string SurName { get; set; }

        public City City { get; set; }

        public long ForeignCityId { get; set; }

    }
}
