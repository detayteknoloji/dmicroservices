using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DMicroservices.DataAccess.History
{
    public class HistoryCollectionModel
    {
        public string DatabaseName { get; set; }
        public string ObjectName { get; set; }
        public string ChangeType { get; set; }
        public string ChangedUser { get; set; }
        public object RowId { get; set; }
        public List<HistoryTableColumnsModel> Columns { get; set; }
        public string Hash { get; set; }
        public DateTime DateTime { get; set; }
    }

    public class HistoryTableColumnsModel
    {
        public string Name { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
    }
}
