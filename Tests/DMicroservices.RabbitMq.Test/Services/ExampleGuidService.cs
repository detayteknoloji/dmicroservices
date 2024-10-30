using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMicroservices.RabbitMq.Test.Services
{
    public class ExampleGuidService
    {
        public string Guid =System.Guid.NewGuid().ToString();

        public ExampleGuidService()
        {


        }

    }
}
