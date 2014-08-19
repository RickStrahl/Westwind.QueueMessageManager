using System;
using Westwind.Utilities;

namespace Westwind.MessageQueueing
{
    public partial class QueueMessageItem
    {        
        public string Id { get; set; }        
        public string QueueName { get; set; } 
       
        public string Status { get; set; }
        public string Action { get; set; }
        
        public DateTime Submitted { get; set; }
        public DateTime? Started { get; set; }
        public DateTime? Completed { get; set; }

        public bool IsComplete { get; set; }
        public bool IsCancelled { get; set; }

        public int Expire { get; set; }
        public string Message { get; set; }

        public string TextInput { get; set; }
        
        public string TextResult { get; set; }
        public decimal NumberResult { get; set; }

        public string Data { get; set; }
        public string Xml { get; set; }
        public string Json { get; set;  }
        public byte[] BinData { get; set; }

        public int PercentComplete { get; set; }

        public string XmlProperties { get; set; }

        internal bool __IsNew = true;

        

        public QueueMessageItem()
        {             
            // Generate a sequential date based on ticks since the beginning of 
            // the year plus a 8 char unique id - this makes the primary key
            // mostly sequentially sortable from oldest to newest without 
            // having to specify a sort order
            Id = GenerateId();

            QueueName = string.Empty;
            Status = "Submitted";
            Submitted = DateTime.UtcNow;
        }

        private static readonly DateTime baseDate = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static string GenerateId()
        {
            return (DateTime.UtcNow - baseDate).Ticks + "_" +
                 DataUtils.GenerateUniqueId(8);
        }
        
    }
}
