using System;
using Newtonsoft.Json;
using Westwind.Utilities;

namespace Westwind.MessageQueueing
{

    /// <summary>
    /// Class that describes a Message Queue Item that is stored in the database.
    /// 
    /// </summary>
    public partial class QueueMessageItem
    {
        /// <summary>
        /// Unique Id assigned to this message.
        /// </summary>
        public string Id { get; set; }


        /// <summary>
        /// Name of the Queue that this item is stored in.
        /// </summary>
        public string QueueName { get; set; }


        /// <summary>
        /// User defined status
        /// Common values are: 
        /// * Submitted
        /// * Started
        /// * Completed         
        /// * Cancelled
        /// * Failed
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// A user defined action that can be used to
        /// group operations to perform when messages are
        /// pulled out of the queue. 
        ///
        /// Essentially this is an operation identifier.
        /// </summary>
        public string Action { get; set; }


        /// <summary>
        /// Time when a message is first submitted
        /// </summary>
        public DateTime Submitted { get; set; }

        /// <summary>
        /// Time when the message starts processing (optional)
        /// </summary>
        public DateTime? Started { get; set; }

        /// <summary>
        /// Time when the message is completed (optional)
        /// </summary>
        public DateTime? Completed { get; set; }

        /// <summary>
        /// The number of times this message has been retried on failure.
        /// </summary>
        public int Retries { get; set; }

        /// <summary>
        /// Elapsed time in milliseconds between Started and Completed or 0
        /// </summary>
        public int ElapsedMs
        {
            get
            {
                if (Completed != null && Started != null && Completed > Started) 
                    return (int) (Completed.Value - Started).Value.TotalMilliseconds;
                
                return 0;
            }
        }

        /// <summary>
        /// Determines whether a message is complete
        /// </summary>
        public bool IsComplete
        {
            get
            {
                if (Completed != null && Completed > DateTime.MinValue)
                {
                    field = true;
                    return field;
                }
                field = false;
                return field;
            }
            set
            {
                field = value;
                if (field)
                {
                    Completed ??= DateTime.UtcNow;
                }
                else
                {
                    Completed = null;
                }
            }
        }

        /// <summary>
        /// Flag that indicates whether a message is cancelled
        /// </summary>
        public bool IsCancelled { get; set; }

        /// <summary>
        /// Flag that is set when the message has failed
        /// </summary>
        public bool IsFailed { get; set; }

        /// <summary>
        /// Determines whether a message is currently running:
        /// * Started
        /// * Not completed or cancelled
        /// </summary>
        /// <remarks>Calculated field - not stored in the data store</remarks>
        /// <returns>true or false</returns>        
        public bool IsRunning
        {
            get
            {
                if (IsComplete || IsCancelled) return false;
                return Started is not null;
            }
        }


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

        [JsonIgnore]
        public bool __IsNew = true;

        

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

        private static readonly DateTime baseDate = new DateTime(DateTime.UtcNow.Year -1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static string GenerateId()
        {
            // generate a semi sequential id based on ticks at beginning of string
            return (DateTime.UtcNow - baseDate).Ticks + "_" +
                 DataUtils.GenerateUniqueId(8);
        }
        
        /// <summary>
        /// Sets the Json property from an object
        /// </summary>
        /// <param name="obj">object to serialize into Json properation</param>
        /// <param name="formatted">if true pretty formats the JSON</param>
        /// <param name="toCamelCase">if true uses camelCase formatting</param>
        /// <returns>true on success false on failure to serialize</returns>
        public bool SetJson(object obj, bool formatted = false, bool toCamelCase =false)
        {
            if (obj == null)
            {
                Json = null;
                return true;
            }

            Json = JsonSerializationUtils.Serialize(obj, false, formatted, toCamelCase);
            if (Json == null)
                return false; // serialization failed
            
            return true;
        }

        /// <summary>
        /// Sets the Started property and sets Status to Started
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Start()
        {
            Started = DateTime.UtcNow;            
            Status = "Started";
        }

        public void Complete()
        {
            Completed = DateTime.UtcNow;
            IsComplete = true;
            Status = "Completed";
        }

        public void Cancel()
        {
            IsCancelled = true;
            Status = "Cancelled";
            Completed = DateTime.UtcNow;
        }

        public void Fail(string message = null)
        {
            IsFailed = true;
            Status = "Failed";
            if (!string.IsNullOrEmpty(message))
                Message = message;
            Completed = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return $"{Id} - {Submitted:HH:mm:ss} - {Status} - {Completed:HH:mm:ss}";
        }
    }
}
