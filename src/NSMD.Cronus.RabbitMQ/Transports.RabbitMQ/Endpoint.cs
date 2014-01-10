using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace NSMD.Cronus.RabbitMQ
{
    public class Endpoint : IDisposable
    {
        private SafeChannel safeChannel;

        private QueueingBasicConsumer consumer;

        private RetryPolicy retryPolicy = RetryableOperation.RetryPolicyFactory.CreateInfiniteLinearRetryPolicy(new TimeSpan(500000));

        private Dictionary<EndpointMessage, BasicDeliverEventArgs> dequeuedMessages;

        private RabbitMQSession session;

        public Endpoint(string endpointName, RabbitMQSession session)
        {
            RoutingHeaders = new Dictionary<string, object>();
            AutoDelete = false;
            Exclusive = false;
            Durable = true;
            this.session = session;
            RoutingKey = String.Empty;
            Name = endpointName;
        }

        public IDictionary<string, object> RoutingHeaders { get; private set; }

        public bool AutoDelete { get; set; }

        public bool Durable { get; private set; }

        public bool Exclusive { get; private set; }

        public string Name { get; private set; }

        public string RoutingKey { get; private set; }

        public void Acknowledge(EndpointMessage message)
        {
            try
            {
                safeChannel.Channel.BasicAck(dequeuedMessages[message].DeliveryTag, false);
                dequeuedMessages.Remove(message);
            }
            catch (EndOfStreamException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (AlreadyClosedException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (OperationInterruptedException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (Exception ex)
            {
                Close();
                throw ex;
            }
            finally
            {

            }
        }

        public void AcknowledgeAll()
        {
            try
            {
                foreach (KeyValuePair<EndpointMessage, BasicDeliverEventArgs> dequeuedMessage in dequeuedMessages)
                {
                    safeChannel.Channel.BasicAck(dequeuedMessage.Value.DeliveryTag, true);
                }

            }
            catch (EndOfStreamException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (AlreadyClosedException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (OperationInterruptedException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (Exception ex)
            {
                Close();
                throw ex;
            }
        }

        public EndpointMessage BlockDequeue()
        {
            BasicDeliverEventArgs result;
            if (consumer == null)
            {
                throw new EndpointClosedException(String.Format("The Endpoint '{0}' is closed", Name));
            }
            try
            {
                result = consumer.Queue.Dequeue();
                EndpointMessage msg = new EndpointMessage(result.Body, result.BasicProperties.Headers);
                dequeuedMessages.Add(msg, result);
                return msg;
            }
            catch (EndOfStreamException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (AlreadyClosedException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (OperationInterruptedException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (Exception ex)
            {
                Close();
                throw ex;
            }

        }

        public void Close()
        {
            if (safeChannel != null)
                safeChannel.Close();
            safeChannel = null;
            dequeuedMessages.Clear();
        }

        public EndpointMessage DequeueNoWait()
        {
            if (consumer == null)
            {
                throw new EndpointClosedException(String.Format("The Endpoint '{0}' is closed", Name));
            }
            try
            {
                var result = consumer.Queue.DequeueNoWait(null);
                if (result == null)
                    return null;
                var msg = new EndpointMessage(result.Body, result.BasicProperties.Headers);
                dequeuedMessages.Add(msg, result);
                return msg;
            }
            catch (EndOfStreamException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (AlreadyClosedException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (OperationInterruptedException ex) { Close(); throw new EndpointClosedException(String.Format("The Endpoint '{0}' was closed", Name), ex); }
            catch (Exception ex)
            {
                Close();
                throw ex;
            }
        }

        public void Dispose()
        {
            if (safeChannel != null)
                safeChannel.Close();
        }

        public void Declare()
        {
            if (safeChannel == null)
            {
                safeChannel = session.OpenSafeChannel();
            }
            safeChannel.Channel.QueueDeclare(Name, Durable, Exclusive, AutoDelete, RoutingHeaders);
            safeChannel.Close();
            safeChannel = null;
        }

        public void Open()
        {

            safeChannel = session.OpenSafeChannel();
            consumer = new QueueingBasicConsumer(safeChannel.Channel);
            safeChannel.Channel.BasicConsume(Name, false, consumer);
            dequeuedMessages = new Dictionary<EndpointMessage, BasicDeliverEventArgs>();
        }

    }
    [Serializable]
    public class EndpointClosedException : Exception
    {
        public EndpointClosedException() { }
        public EndpointClosedException(string message) : base(message) { }
        public EndpointClosedException(string message, Exception inner) : base(message, inner) { }
        protected EndpointClosedException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}