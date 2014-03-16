using System;
using System.Collections.Generic;
using System.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace Westwind.MessageQueueing.Tests
{
    [TestClass]
    public class MsMqTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            var ids = new List<string>();
            var mq = new MessageQueue(@".\private$\QMM_");

            
            mq.Send("12345");
            mq.Send("23456");

            var msg = mq.Receive(new TimeSpan(0, 0, 0, 0, 1));
            if (msg != null)
                Console.WriteLine(msg.Body);

            mq.Dispose();
            mq = null;

            mq = new MessageQueue(@".\private$\QMM_");
            
            msg = mq.Receive(new TimeSpan(0, 0, 0, 0, 1));
            if (msg != null)
            {
                msg.Formatter = new XmlMessageFormatter(new Type[] {typeof (string)});
                Console.WriteLine(msg.Body);
            }
        }

        [TestMethod]
        public void StringMessageFormatter()
        {
            var ids = new List<string>();
            var mq = new MessageQueue(@".\private$\QMM_");

            mq.Formatter = new StringMessageFormatter();
            mq.Send("12345");
            mq.Send("23456");

            mq.Dispose();
            mq = null;

            mq = new MessageQueue(@".\private$\QMM_");

            for (int i = 0; i < 3; i++)
            {
                Message msg = null;
                try
                {
                    msg = mq.Receive(new TimeSpan(0, 0, 0, 0, 1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                if (msg != null)
                {
                    msg.Formatter = new StringMessageFormatter();
                    Console.WriteLine(msg.Body);
                }
            }
        }
    }
}
