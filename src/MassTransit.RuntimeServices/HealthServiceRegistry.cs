// Copyright 2007-2008 The Apache Software Foundation.
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
namespace MassTransit.RuntimeServices
{
	using System.IO;
	using FluentNHibernate.Cfg;
	using Infrastructure.Saga;
	using Model;
	using NHibernate;
	using NHibernate.Tool.hbm2ddl;
	using Saga;
	using Services.HealthMonitoring;
	using StructureMap;
	using StructureMap.Configuration.DSL;

    public class HealthServiceRegistry :
		Registry
	{
		private readonly IContainer _container;

		public HealthServiceRegistry(IContainer container)
		{
			_container = container;

			var configuration = container.GetInstance<IConfiguration>();

			For<ISessionFactory>()
				.Singleton()
				.Use(context => CreateSessionFactory());

			For(typeof (ISagaRepository<>))
				.Add(typeof (NHibernateSagaRepository<>));

		    var bus = ServiceBusFactory.New(sbc =>
		        {
                    sbc.UseControlBus();
		            sbc.UseMsmq();
                    sbc.ReceiveFrom(configuration.HealthServiceDataUri);
                    sbc.SetConcurrentConsumerLimit(1);
                    sbc.UseMulticastSubscriptionClient();
		        });

		    For<IServiceBus>().Use(bus);
		}

		private static ISessionFactory CreateSessionFactory()
		{
			return Fluently.Configure()
				.Mappings(m => { m.FluentMappings.Add<HealthSagaMap>(); })
				//.ExposeConfiguration(BuildSchema)
				.BuildSessionFactory();
		}

		private static void BuildSchema(NHibernate.Cfg.Configuration config)
		{
			new SchemaUpdate(config).Execute(false, true);

			string schemaFile = Path.Combine(Path.GetDirectoryName(typeof (HealthService).Assembly.Location), typeof (HealthService).Name + ".sql");

			new SchemaExport(config).SetOutputFile(schemaFile).Execute(false, false, false);
		}
	}
}