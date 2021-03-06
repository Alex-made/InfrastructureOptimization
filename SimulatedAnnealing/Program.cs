﻿using Common;
using Common.Domain;
using Common.TestData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SimulatedAnnealing
{
    class Program
    {
        static void Main(string[] args)
        {
            //На входе: минимальная температура tMin
            var tMin = 0.0001d;
			//начальная температура tInitial
			var tInitial = 1d;
            
			//Е - функция оптимизации - чем она меньше, тем состояние более оптимально
			var optimizationFunction = new OptimizationFunction(state =>
			{
				var servers = state.Servers;

				var freeServersTerm = servers.Count(x => x.IsFree) * 2.5;
				var negativeHddFreeTerm = servers.Where(x => x.HddFree < 0).Select(x => x.HddFree * 3).Sum();
				var positiveHddFreeTerm = servers.Where(x => x.HddFree > 0).Select(x => x.HddFree * 0.2).Sum();
				var negativeRamFreeTerm = servers.Where(x => x.RamFree < 0).Select(x => x.RamFree * 3).Sum();
				var positiveRamFreeTerm = servers.Where(x => x.RamFree > 0).Select(x => x.RamFree * 0.2).Sum();
				var negativeCpuFreeTerm = servers.Where(x => x.CpuFree < 0).Select(x => x.CpuFree * 3).Sum();
				var positiveCpuFreeTerm = servers.Where(x => x.CpuFree > 0).Select(x => x.CpuFree * 0.2).Sum();

				var fitness = freeServersTerm + negativeHddFreeTerm + positiveHddFreeTerm + negativeRamFreeTerm +
							  positiveRamFreeTerm + negativeCpuFreeTerm + positiveCpuFreeTerm;

				//по каждому серверу нужно вычислить коэффициент заполненности
				var serversFill = new List<double>();
				foreach (var server in servers)
				{
					var serverFill = ((server.HddFull - server.HddFree) / server.HddFull) * 0.2 +
					((server.RamFull - server.RamFree) / server.RamFull) * 0.4 +
					((server.CpuFull - server.CpuFree) / server.CpuFull) * 0.4;
					serversFill.Add(serverFill);
				}
				serversFill = serversFill.OrderByDescending(x => x).ToList();
				//отладочная строка
				//var serversFill = new List<double> { 0.9d, 0.9d, 0.6d, 0.2d,0 };
				//среднее значение заполненности серверов
				var averageFill = serversFill.Sum() / servers.Count();
				//рассчитаем дисперсию у serversFill
				var dispersion = serversFill.Select(eachFill => (eachFill - averageFill) * (eachFill - averageFill)).Sum() / servers.Count;

				return -(dispersion * 3) - fitness;
			});
			//Т - функция изменения температуры (понижения температуры)
			var decreaseTemperatureFunction = new DecreaseTemperatureFunction((initialTemp, i) =>
			{
				return initialTemp * 0.1 / i;
			});
			//F - функция, порождающая новое состояние 
			//за порождение нового состояния отвечает класс State, метод GetMutatedState


			//Задаём произвольное первое состояние нашей инфраструктуры state1
			TestData.GetData(out var servers, out var services);
			//Выполняем алгоритм
			PerformAnnealing(tInitial, tMin, optimizationFunction,
				decreaseTemperatureFunction, servers, services);
		}

		

		/// <summary>
		/// Выводит в консоль результаты алгоритма на каждом шаге
		/// </summary>
		/// <param name="state">Состояние решения.</param>
		/// <param name="e">Значение функции оптимизации.</param>
		/// <param name="elapsedTime">Время работы алгоритма.</param>
		private static void ShowResult(State state, IList<Service> services, double e, TimeSpan elapsedTime)
		{
			Console.WriteLine(elapsedTime.TotalSeconds + " c");
			Console.WriteLine("Заполненность: " + Filling.Calculate(state.Servers, services));
			Console.WriteLine("Значение функции оптимизации:" + e);
			foreach(var server in state.Servers)
			{
				Console.Write("Сервер: " + server.Name +
									  ", HddFull: " + server.HddFull +
									  ", RamFull: " + server.RamFull +
									  ", HddFree: " + server.HddFree +
									  ", RamFree: " + server.RamFree +
									  ", CpuFree: " + server.CpuFree +
									  ", сервисы: ");
				Console.Write(" Заполненность Hdd: " + Math.Round(100 - (server.HddFree / server.HddFull * 100)) + "%");
				Console.Write(" Заполненность Ram: " + Math.Round(100 - (server.RamFree / server.RamFull * 100)) + "%");
				Console.Write(" Заполненность Cpu: " + Math.Round(100 - server.CpuFree) + "%"); 
				foreach (var service in server.Services)
				{
					Console.Write(service.Name + ", ");
				}
				Console.WriteLine();
				Console.WriteLine();
			}
		}

		private static void PerformAnnealing(double tInitial, 
			double tMin, 
			OptimizationFunction optimizationFunction,
			DecreaseTemperatureFunction decreaseTemperatureFunction,
			IList<Server> servers, IList<Service> services)
		{
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			var currentState = new State(servers, services);
			currentState.Initialize();
			//считаем значение функции оптимизации E от первоначального состояния
			var e = optimizationFunction.Evaluate(currentState);

			var currentTemperature = tInitial;
			var iterationNumber = 1;
			while (currentTemperature > tMin)
			{
				//получить новое, немного измененное состояние (немного измененное распределение серверов)
				var newState = currentState.GetMutatedState(currentState);
				//посчитать разницу значений функции оптимизации
				var deltaE = optimizationFunction.Evaluate(newState) - optimizationFunction.Evaluate(currentState);
				//если значение функции оптимизации меньше или равно нулю, то осуществляем переход на новое состояние
				if (deltaE <= 0)
				{
					currentState = newState;
				}
				//иначе, осуществляем переход на новое состояние с вероятностью jumpProbability
				{
					var jumpProbability = TransitionProbability.Evaluate(deltaE, currentTemperature);
					var randNumber = new Random().NextDouble();
					if (randNumber <= jumpProbability)
					{
						currentState = newState;
					}
				}
				//понижаем температуру
				currentTemperature = decreaseTemperatureFunction.Evaluate(tInitial, iterationNumber);
				iterationNumber++;
			}
			stopwatch.Stop();
			ShowResult(currentState, services, optimizationFunction.Evaluate(currentState),stopwatch.Elapsed);
		}
	}
}
