using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMicroservices.RabbitMq.Test.Services
{
    public class ExampleSingleService
    {
        private int _userId;

        public int UserId
        {
            get => _userId;
            set => _userId = value;
        }

        public ExampleSingleService()
        {


        }


        public string GetMessage()
        {
            return $"hello user by id ({UserId})";
        }
    }
}
