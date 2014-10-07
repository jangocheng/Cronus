using System;
using System.Linq;
using System.Collections.Generic;
using Elders.Cronus.DomainModeling;
using Elders.Cronus.EventSourcing;
using Elders.Cronus.Pipeline.Config;
using Elders.Cronus.Serializer;
using Elders.Cronus.UnitOfWork;
using Elders.Cronus.IocContainer;

namespace Elders.Cronus.Pipeline.Hosts
{

    public interface IHaveCommandPublisher
    {
        Lazy<IPublisher<ICommand>> CommandPublisher { get; set; }
    }

    public interface IHaveEventPublisher
    {
        Lazy<IPublisher<IEvent>> EventPublisher { get; set; }
    }

    public interface IHaveConsumers
    {
        List<Lazy<IEndpointConsumer>> Consumers { get; set; }
    }

    public interface IHaveContainer
    {
        IContainer Container { get; set; }
    }

    public interface ICronusSettings : IHaveContainer, ICanConfigureSerializer, IHaveCommandPublisher, IHaveEventPublisher, IHaveConsumers, ISettingsBuilder<CronusHost>
    {

    }

    public class CronusSettings : ICronusSettings
    {
        public CronusSettings()
        {
            (this as IHaveConsumers).Consumers = new List<Lazy<IEndpointConsumer>>();
            (this as IHaveContainer).Container = new Container();
        }

        Lazy<IPublisher<ICommand>> IHaveCommandPublisher.CommandPublisher { get; set; }

        List<Lazy<IEndpointConsumer>> IHaveConsumers.Consumers { get; set; }

        IContainer IHaveContainer.Container { get; set; }

        Lazy<IPublisher<IEvent>> IHaveEventPublisher.EventPublisher { get; set; }

        ISerializer IHaveSerializer.Serializer { get; set; }

        Lazy<CronusHost> ISettingsBuilder<CronusHost>.Build()
        {
            ICronusSettings settings = this as ICronusSettings;

            return new Lazy<CronusHost>(() =>
            {
                var cronusConfiguration = new CronusHost();

                if (settings.CommandPublisher != null)
                    cronusConfiguration.CommandPublisher = settings.CommandPublisher.Value;

                if (settings.EventPublisher != null)
                    cronusConfiguration.EventPublisher = settings.EventPublisher.Value;

                if (settings.Consumers != null && settings.Consumers.Count > 0)
                    cronusConfiguration.Consumers = settings.Consumers.Select(x => x.Value).ToList();

                cronusConfiguration.Serializer = settings.Serializer;

                return cronusConfiguration;
            });
        }
    }

    public static class CronusConfigurationExtensions
    {
        public static T UsePipelineEventPublisher<T>(this T self, Action<EventPipelinePublisherSettings> configure = null) where T : ICronusSettings
        {
            EventPipelinePublisherSettings settings = new EventPipelinePublisherSettings();
            self.CopySerializerTo(settings);
            if (configure != null)
                configure(settings);
            self.EventPublisher = settings.GetInstanceLazy();
            return self;
        }

        public static T UsePipelineCommandPublisher<T>(this T self, Action<CommandPipelinePublisherSettings> configure = null) where T : ICronusSettings
        {
            CommandPipelinePublisherSettings settings = new CommandPipelinePublisherSettings();
            self.CopySerializerTo(settings);
            if (configure != null)
                configure(settings);
            self.CommandPublisher = settings.GetInstanceLazy();
            return self;
        }

        public static T UseInMemoryCommandPublisher<T>(this T self, string boundedContext, Action<InMemoryCommandPublisherSettings> configure = null) where T : ICronusSettings
        {
            InMemoryCommandPublisherSettings settings = new InMemoryCommandPublisherSettings();
            if (configure != null)
                configure(settings);
            self.CommandPublisher = settings.GetInstanceLazy();
            return self;
        }

        public static T UseInMemoryEventPublisher<T>(this T self, string boundedContext, Action<InMemoryEventPublisherSettings> configure = null) where T : ICronusSettings
        {
            InMemoryEventPublisherSettings settings = new InMemoryEventPublisherSettings();
            if (configure != null)
                configure(settings);
            self.EventPublisher = settings.GetInstanceLazy();
            return self;
        }
    }

    public class ApplicationServiceBatchUnitOfWork : IBatchUnitOfWork
    {
        IAggregateRepository repository;
        IEventStorePersister persister;
        IPublisher<IEvent> publisher;

        public ApplicationServiceBatchUnitOfWork(IAggregateRepository repository, IEventStorePersister persister, IPublisher<IEvent> publisher)
        {
            this.repository = repository;
            this.persister = persister;
            this.publisher = publisher;
        }

        public void Begin()
        {
            var appServiceGateway = new ApplicationServiceGateway(repository, persister);
            Lazy<IAggregateRepository> lazyRepository = new Lazy<IAggregateRepository>(() => (appServiceGateway));
            Lazy<IApplicationServiceGateway> gateway = new Lazy<IApplicationServiceGateway>(() => (appServiceGateway));
            Context.Set<Lazy<IAggregateRepository>>(lazyRepository);
            Context.Set<Lazy<IApplicationServiceGateway>>(gateway);
        }

        public void End()
        {
            var gateway = Context.Get<Lazy<IApplicationServiceGateway>>().Value;
            gateway.CommitChanges(@event => publisher.Publish(@event));
        }

        public IUnitOfWorkContext Context { get; set; }
    }
}