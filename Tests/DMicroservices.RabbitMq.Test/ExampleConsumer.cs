using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Consumer;
using DMicroservices.RabbitMq.Producer;
using DMicroservices.Utils.Logger;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client.Events;

namespace DMicroservices.RabbitMq.Test  
{
    [ListenQueue("ExampleQueue")]
    class ExampleConsumer : StepBase
    {
        public override bool AutoAck => false;
        public override ushort PrefectCount { get => 5; }

        public override bool Durable => true;
        public override bool AutoDelete => false;

        public Test TestData { get; set; }

        


        public override void Execute(ExampleModel sender, BasicDeliverEventArgs e)
        {
            TestData = new Test
            {
                Data = sender.Message,
                ConsumerTag = e.ConsumerTag
            };

            Thread.Sleep(50);
            if (TestData.Data != sender.Message)
            {
                Console.WriteLine($"err {TestData.Data} != {sender.Message} => {TestData.ConsumerTag} {e.ConsumerTag}");
            }
        }
    }

    public class Test
    {
        public string Data { get; set; }
        public string ConsumerTag { get; set; }
    }

    public abstract class StepBase : BasicConsumer<ExampleModel>
    {
      

        public int CompanyNo { get; set; }

        private string _companyVkn;

      
        #region Member
        public override bool AutoAck => false;

        public override Action<ExampleModel, BasicDeliverEventArgs> DataReceivedAction => DataReceived;


        #endregion

        public virtual void Execute(ExampleModel sender, BasicDeliverEventArgs e)
        {

        }
        public virtual void OnBeforeExecution(object sender, BasicDeliverEventArgs e)
        {
        }

        #region Constructor
        protected StepBase()
        {

        }
        #endregion


        private void DataReceived(ExampleModel stepExecution, BasicDeliverEventArgs e)
        {
            try
            {

                try
                {

                    Execute(stepExecution, e);
                }
                catch (Exception exception)
                {


                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                GetCurrentConsumer().Model.BasicAck(e.DeliveryTag, false);
            }
        }


        public void Dispose()
        {
           
            GC.Collect();
            GC.SuppressFinalize(this);
        }
    }
}