﻿// Copyright 2007-2015 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.RabbitMqTransport.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Builders;
    using MassTransit.Builders;
    using MassTransit.Configurators;
    using PipeConfigurators;
    using Util;


    public class RabbitMqBusFactoryConfigurator :
        IRabbitMqBusFactoryConfigurator,
        IBusFactory
    {
        readonly IList<IRabbitMqHost> _hosts;
        readonly IList<IBusFactorySpecification> _transportBuilderConfigurators;
        readonly IList<IPipeSpecification<ConsumeContext>> _endpointPipeSpecifications;
//        RabbitMqReceiveEndpointConfigurator _defaultEndpointConfigurator;
        IRabbitMqHost _defaultHost;
        Uri _localAddress;

        public RabbitMqBusFactoryConfigurator()
        {
            _hosts = new List<IRabbitMqHost>();
            _transportBuilderConfigurators = new List<IBusFactorySpecification>();
            _endpointPipeSpecifications = new List<IPipeSpecification<ConsumeContext>>();
        }

        public IBusControl CreateBus()
        {
            var builder = new RabbitMqBusBuilder(_hosts, _localAddress, _endpointPipeSpecifications);

            foreach (IBusFactorySpecification configurator in _transportBuilderConfigurators)
                configurator.Configure(builder);

            IBusControl bus = builder.Build();

            return bus;
        }

        public IEnumerable<ValidationResult> Validate()
        {
            if (_hosts.Count == 0)
                yield return this.Failure("Host", "At least one host must be defined");

            foreach (ValidationResult result in _transportBuilderConfigurators.SelectMany(x => x.Validate()))
                yield return result;
            foreach (ValidationResult result in _endpointPipeSpecifications.SelectMany(x => x.Validate()))
                yield return result;
        }

        public IRabbitMqHost Host(RabbitMqHostSettings settings)
        {
            var host = new RabbitMqHost(settings);
            _hosts.Add(host);

            // use first host for default host settings :(
            if (_hosts.Count == 1)
            {
                _defaultHost = host;

                string machineName = GetSanitizedQueueNameString(HostMetadataCache.Host.MachineName);
                string processName = GetSanitizedQueueNameString(HostMetadataCache.Host.ProcessName);
                string queueName = string.Format("bus-{0}-{1}-{2}", processName, machineName, NewId.Next().ToString("NS"));

                _defaultEndpointConfigurator = new RabbitMqReceiveEndpointConfigurator(_defaultHost, queueName);
                _defaultEndpointConfigurator.Exclusive();
                _defaultEndpointConfigurator.Durable(false);
                _defaultEndpointConfigurator.AutoDelete();

                AddBusFactorySpecification(_defaultEndpointConfigurator);

                _localAddress = settings.GetInputAddress(_defaultEndpointConfigurator.Settings);
            }

            return host;
        }

        public void AddBusFactorySpecification(IBusFactorySpecification configurator)
        {
            _transportBuilderConfigurators.Add(configurator);
        }

        public void Mandatory(bool mandatory = true)
        {
//            _publishSettings.Mandatory = mandatory;
        }

        public void ReceiveEndpoint(IRabbitMqHost host, string queueName,
            Action<IRabbitMqReceiveEndpointConfigurator> configure)
        {
            if (host == null)
                throw new EndpointNotFoundException("The host address specified was not configured.");

            var endpointConfigurator = new RabbitMqReceiveEndpointConfigurator(host, queueName);

            configure(endpointConfigurator);

            AddBusFactorySpecification(endpointConfigurator);
        }

        public void AddPipeSpecification(IPipeSpecification<ConsumeContext> specification)
        {
            _endpointPipeSpecifications.Add(specification);
        }

        string GetSanitizedQueueNameString(string input)
        {
            var sb = new StringBuilder(input.Length);

            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else if (c == '.' || c == '_' || c == '-' || c == ':')
                    sb.Append(c);
            }

            return sb.ToString();
        }

        public void OnPublish<T>(Action<RabbitMqPublishContext<T>> callback)
            where T : class
        {
            throw new NotImplementedException();
        }

        public void OnPublish(Action<RabbitMqPublishContext> callback)
        {
            throw new NotImplementedException();
        }

        Uri GetSourceAddress(RabbitMqHostSettings host, string queueName)
        {
            var builder = new UriBuilder();

            builder.Scheme = "rabbitmq";
            builder.Host = host.Host;
            builder.Port = host.Port;


            builder.Path = host.VirtualHost != "/" ? string.Join("/", host.VirtualHost, queueName) : queueName;

            builder.Query += string.Format("temporary=true&prefetch=4");

            return builder.Uri;
        }
    }
}