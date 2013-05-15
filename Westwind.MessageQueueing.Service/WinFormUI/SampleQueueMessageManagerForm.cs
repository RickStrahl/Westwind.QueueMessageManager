using System;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using Westwind.MessageQueueing;

namespace Westwind.MessageQueueing.Service
{
    public partial class SampleQueueMessageManagerForm : Form
    {
        private SampleQueueController Controller;

        public string MessageType { get; set; }
        public int ThreadCount { get; set; }

        public SampleQueueMessageManagerForm()
        {
            MessageType = "MPWF";
            ThreadCount = 1;

            InitializeComponent();            

            // *** Instantiate an instance of the controller that manages the queueing
            // *** and processing of messages.
            Controller = new SampleQueueController();

            // idle poll interval
            Controller.WaitInterval = 1000;

            Controller.ExecuteStart += Controller_ExecuteStart;
            Controller.ExecuteComplete += Controller_ExecuteComplete;
            Controller.ExecuteFailed += Controller_ExecuteFailed;
        }



        /// <summary>
        /// Start the Controller to look for requests and queue them
        /// </summary>        
        public void StartProcessing()
        {
            Controller.QueueName = this.txtType.Text;

            int ThreadCount = 1;
            int.TryParse(this.txtThreadCount.Text, out ThreadCount);

            // *** Spin up n Number of threads to process requests
            this.Controller.StartProcessingAsync(ThreadCount);
        }

        /// <summary>
        /// Stops processing requests.
        /// </summary>
        public void StopProcessing()
        {
            Controller.StopProcessing();
        }

        void Controller_ExecuteFailed(QueueMessageManager Message, Exception ex)
        {
            this.WriteEntry("Failed: " + Message.Entity.Completed + "->" + Message.Entity.Id +  " - "  + ex.Message);
        }

        void Controller_ExecuteComplete(QueueMessageManager Message)
        {
            this.WriteEntry("Complete: " + Message.Entity.Completed + "->" + Message.Entity.Id +  " - " + Message.Entity.Message);
        }

        void Controller_ExecuteStart(QueueMessageManager Message)
        {
            this.WriteEntry("Started: " + Message.Entity.Started + "->" + Message.Entity.Id);
        }

        /// <summary>
        /// Start the Controller to look for requests and queue them
        /// </summary>
        private void btnStart_Click(object sender, EventArgs e)
        {
            StartProcessing();
        }



        /// <summary>
        /// Stop the control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStop_Click(object sender, EventArgs e)
        {
            StopProcessing();
        }


        /// <summary>
        /// Writes out a message to the List display. 
        /// </summary>
        /// <param name="Message"></param>
        public void WriteEntry(string Message)
        {
            string text = this.Invoke(new Func<string>(this.GetRequestText)) as string + "\r\n" + Message;

            if (text.Length > 4096)
                text = text.Substring(0,3000);

            // *** Threadsafe update
            this.Invoke(new Action<string>(this.SetRequestText), text);
        }

        public void SetRequestText(string Text)
        {
            this.txtRequests.Text = Text;
        }
        public string GetRequestText()
        {
            return this.txtRequests.Text;
        }

        private void QueueMessageServerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Controller.StopProcessing();
        }

        private void QueueMessageServerForm_Load(object sender, EventArgs e)
        {
            this.txtType.Text = MessageType;
            this.txtThreadCount.Text = ThreadCount.ToString();

            StartProcessing();
        }

        private void btnCreateTable_Click(object sender, EventArgs e)
        {
            // Create database table and store procedure if it doesn't exist
            QueueMessageManager manager = new QueueMessageManager();
            if (!manager.CreateDatabaseTable())
                MessageBox.Show(manager.ErrorMessage, "Error creating QueueTable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show("QueueTable created","Queue Table Creation", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

    }
}