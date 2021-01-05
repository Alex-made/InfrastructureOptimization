﻿using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterEnvironmentSimulator.Models;
using EuqlidFunctionOptimization;
using GeneticSharp.Domain.Fitnesses;
using GeneticSharp.Domain.Populations;
using InfrastructureOptimization.Domain;

namespace InfrastructureOptimization
{
	class Program
	{
		static void Main(string[] args)
		{
			GetData(out var servers, out var services);

			var chromosome = new MyChromosome(servers, services);

			var population = new Population(50, 100, chromosome);

			var fitness = new FuncFitness((c) =>
			{
				var mc = (MyChromosome) c;
				
				var genes = mc.GetGenes();
				var servers = genes.Select(x => (Server) x.Value).ToList();

				var freeServersTerm = servers.Count(x => x.IsFree) * 2;
				var negativeHddFreeTerm = servers.Where(x => x.HddFree < 0).Select(x=>x.HddFree * 3).Sum();
				var positiveHddFreeTerm = servers.Where(x => x.HddFree > 0).Select(x => x.HddFree * 0.5).Sum();
				var negativeRamFreeTerm = servers.Where(x => x.RamFree < 0).Select(x => x.RamFree * 3).Sum();
				var positiveRamFreeTerm = servers.Where(x => x.RamFree > 0).Select(x => x.RamFree * 0.5).Sum();

				var fitness = freeServersTerm + negativeHddFreeTerm + positiveHddFreeTerm + negativeRamFreeTerm +
				              positiveRamFreeTerm;
				
				return fitness;
			});

			//отладка
			for(var i=0;i<30;i++)
			{
				GetData(out var s1, out var s2);
				var myChromosome = new MyChromosome(s1, s2);
				Console.Write(fitness.Evaluate(myChromosome) + " ");
				foreach (var gene in myChromosome.GetGenes())
				{
					var server = (Server) gene.Value;
					var serviceNames = server.Services.Select(x => x.Name.ToString()).ToList();
					Console.Write("Сервер - " + server.Name + ":{ ");
					serviceNames.ForEach(x=>Console.Write(x + ";"));
					Console.Write("} ");
				}
				Console.WriteLine();
				Console.WriteLine("Next");
			}
		}

		private static void GetData(out ISet<Server> servers, out ISet<Service> services)
		{
			services = new HashSet<Service>()
			{
				new Service("Service_1", OsType.Windows, 5, 1),
				new Service("Service_2", OsType.Windows, 12, 2),
				new Service("Service_3", OsType.Windows, 3, 1),
				new Service("Service_4", OsType.Windows, 7, 2),
				new Service("Service_Linux", OsType.Linux, 8, 2)
			};

			servers = new HashSet<Server>()
			{
				new Server ("Server_1", OsType.Windows, 20, 10),
				new Server ("Server_2", OsType.Windows, 9, 10),
				new Server ("Server_Linux", OsType.Linux, 10, 10)
			};
		}
	}
}