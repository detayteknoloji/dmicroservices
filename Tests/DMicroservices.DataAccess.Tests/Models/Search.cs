using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace DMicroservices.DataAccess.Tests.Models
{
    public class Search
    {
        [Key]
        public int Id { get; set; }
        public int IntValue { get; set; }
        public int? IntNullableValue { get; set; }

        public string StringValue { get; set; }

        public decimal DecimalValue { get; set; }

        public short SmallIntValue { get; set; }

        public Int64 BigIntValue { get; set; }

        public byte ByteValue { get; set; }

        public DateTime DateTimeValue { get; set; }

        public bool BoolValue { get; set; }

        public double DoubleValue { get; set; }

        public Number EnumValue { get; set; }

        public Guid GuidValue { get; set; }

        public Guid? GuidNullableValue { get; set; }
    }

    public enum Number : short
    {
        One = 1,
        Two = 2
    }
}
