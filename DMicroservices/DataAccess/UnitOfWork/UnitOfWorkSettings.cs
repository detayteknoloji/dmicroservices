using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DMicroservices.DataAccess.UnitOfWork
{
    public class UnitOfWorkSettings
    {
        public bool ChangeDataCapture { get; set; }

        public string ChangedUserPropertyName { get; set; }

        public string IdPropertyName { get; set; }
    }
}
