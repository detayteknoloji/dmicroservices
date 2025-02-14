using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Consumer;
using DMicroservices.Utils.Logger;
using RabbitMQ.Client.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

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

        public override void OnBeforeExecution(object sender, BasicDeliverEventArgs e)
        {

        }

        public override void Execute(ExampleModel sender, BasicDeliverEventArgs e)
        {
            try
            {
                TestData = new Test
                {
                    Data = sender.Message,
                    ConsumerTag = e.ConsumerTag
                };
                Thread.Sleep(10000);
                if (TestData.Data != sender.Message)
                {
                    Console.WriteLine($"err {TestData.Data} != {sender.Message} => {TestData.ConsumerTag} {e.ConsumerTag}");
                }
            }
            catch (OperationCanceledException ex)
            {
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

        public virtual void OnTriggeredCancelException(object sender, EventArgs e)
        {
        }

        public CancellationTokenSource CancellationTokenSource;

        private void DataReceived(ExampleModel stepExecution, BasicDeliverEventArgs e)
        {
            try
            {
                try
                {
                    CancellationTokenSource = new CancellationTokenSource();
                    OnBeforeExecution(stepExecution, e);
                    
                    var task = Task.Run(() => Execute(stepExecution, e), CancellationTokenSource.Token);
                    task.ContinueWith(p =>
                    {
                        if (p?.Exception?.InnerException != null)
                        {
                            ElasticLogger.Instance.Error(new Exception($"TaxPayer Güncellenirken Hata Aldı!"), $"{p.Exception?.InnerException?.GetType()?.Name}: {p.Exception?.InnerException?.Message}");
                        }
                    }, TaskContinuationOptions.OnlyOnCanceled).ContinueWith(p =>
                    {
                        try
                        {
                        }
                        catch (Exception ioExcepiton)
                        {
                            ElasticLogger.Instance.Error(ioExcepiton, $"{this} IoExcepiton");
                        }
                    }, TaskContinuationOptions.None).Wait();

                }
                catch (OperationCanceledException ex)
                {
                    Console.WriteLine("work ? 1");
                }
                catch (Exception exception)
                {
                    Console.WriteLine("IThreadPoolWorkItem 2");


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