using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Westwind.MessageQueueing.Properties;
using System.Data;
using Westwind.Utilities;

namespace Westwind.MessageQueueing
{
    /// <summary>
    /// Provides easy to use serialization helpers to read values 
    /// from the XML and Binary fields of the queue
    /// </summary>
    public class QueueMessageManagerSerializationHelper
    {
        QueueMessageManager Manager = null;

        public QueueMessageManagerSerializationHelper(QueueMessageManager manager)
        {
            Manager = manager;
        }

        /// <summary>
        /// Serializes an object into the BinResult field
        /// </summary>
        /// <param name="objectInstance"></param>
        /// <returns></returns>
        public bool SerializeToBinResult(object objectInstance, QueueMessageItem item = null)
        {
            if (item == null)
                item = Manager.Entity;

            if (item == null)
            {
                Manager.SetError(Resources.NoEntityAvailable);
                return false;
            }

            byte[] result = null;
            try
            {
                result = SerializationUtils.SerializeObjectToByteArray(objectInstance, true);
            }
            catch (Exception ex)
            {
                Manager.SetError(ex.Message);
                return false;
            }

            item.BinResult = result;

            return true;
        }




        /// <summary>
        /// Deserializes an object out of the 
        /// </summary>        
        /// <returns></returns>
        public T DeSerializeFromBinResult<T>(QueueMessageItem item = null)
        {
            if (item == null)
                item = Manager.Entity;

            if (item == null)
            {
                Manager.SetError(Resources.NoEntityIsLoaded);
                return default(T);
            }
            if (item.BinResult == null)
                return default(T);

            object result = null;
            try
            {
                result = SerializationUtils.DeSerializeObject(item.BinResult, typeof(T));
            }
            catch (Exception ex)
            {
                Manager.SetError(ex.Message);
                return default(T);
            }

            return (T)result;
        }

        /// <summary>
        /// Serializes an object to the current or passed queue item's XML property
        /// </summary>
        /// <param name="value">The value to serialize to XML</param>
        /// <param name="item">optional queue item. If not passed the current Entity instance is used</param>
        /// <returns></returns>
        public bool SerializeToXml(object value, QueueMessageItem item = null)
        {
            if (item == null)
                item = Manager.Entity;

            if (item == null)
                return false;

            string xml = null;
            if (!SerializationUtils.SerializeObject(value, out xml))
                return false;

            item.Xml = xml;

            return true;
        }

        /// <summary>
        /// Deserializes the XML field of the current or passed entity back into 
        /// a value
        /// </summary>
        /// <typeparam name="T">The type of the expected item</typeparam>
        /// <param name="xml"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public T DeSerializeFromXml<T>(string xml, QueueMessageItem item = null)
        {
            object val = DeSerializeFromXml(xml, typeof(T), item);
            if (val == null)
                return default(T);

            return (T)val;
        }

        /// <summary>
        /// Deserializes the XML field of the current or passed entity back into 
        /// a value
        /// </summary>        
        /// <param name="xml">The XML to parse into an object</param>
        /// <param name="item">the QueueMessageItem to parse into or the current entity</param>
        /// <returns>object or null on failure</returns>
        public object DeSerializeFromXml(string xml, Type type, QueueMessageItem item = null)
        {
            if (item == null)
                item = Manager.Entity;

            if (item == null)
                return null;

            return SerializationUtils.DeSerializeObject(xml, type);
        }


      

    }
}
