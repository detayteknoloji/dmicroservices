using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMicroservices.RabbitMq.Test.Services
{
    public class ExampleScopedService
    {
        public int UserId { get; set; }
        public ExampleScopedService()
        {
            

        }


        public string GetMessage()
        {
            return $"hello user by id ({UserId})";
        }
    }
}
