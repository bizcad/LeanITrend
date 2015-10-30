using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Reflection;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Logging;
using QuantConnect.Packets;
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Queues;
using QuantConnect.Messaging;
using QuantConnect.Api;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Lean.Engine.HistoricalData;

namespace Optimization
{
    public class RunClass : MarshalByRefObject
    {
        private Api _api;
        private Messaging _notify;
        private JobQueue _jobQueue;
        private IResultHandler _resultshandler;

        private FileSystemDataFeed _dataFeed;
        private ConsoleSetupHandler _setup;
        private BacktestingRealTimeHandler _realTime;
        private ITransactionHandler _transactions;
        private IHistoryProvider _historyProvider;

        private readonly Engine _engine;

        public RunClass()
        {

        }
        public decimal Run(string val)
        {
            //Config.Set("algorithm-type-name", val);
            //Log.LogHandler = Composer.Instance.GetExportedValueByTypeName<ILogHandler>(Config.Get("log-handler", "CompositeLogHandler"));

            LaunchLean(val);
            
            if (_resultshandler != null)
            {
                var dsktophandler = (OptimizationResultHandler)_resultshandler;
                //var dsktophandler = (ConsoleResultHandler)_resultshandler;
                var sharpe_ratio = 0.0m;
                string ratio = "0";
                if (dsktophandler.FinalStatistics.Count > 0)
                {
                    ratio = dsktophandler.FinalStatistics["Sharpe Ratio"];
                    Decimal.TryParse(ratio, out sharpe_ratio);
                }
                //_engine = null;
                return sharpe_ratio;
            }
            return -1.0m;
        }
        private void LaunchLean(string val)
        {

            Config.Set("environment", "backtesting");
            string algorithm = val;

            Config.Set("algorithm-type-name", algorithm);
            //string datapath = Config.Get("data-folder");
            _jobQueue = new JobQueue();
            _notify = new Messaging();
            _api = new Api();
            _resultshandler = new OptimizationResultHandler();
            //_resultshandler = new ConsoleResultHandler();
            _dataFeed = new FileSystemDataFeed();
            _setup = new ConsoleSetupHandler();
            _realTime = new BacktestingRealTimeHandler();
            _historyProvider = new SubscriptionDataReaderHistoryProvider();
            _transactions = new BacktestingTransactionHandler();
            Log.LogHandler = (ILogHandler)new FileLogHandler();
            Log.DebuggingEnabled = false;
            Log.DebuggingLevel = 1;

            var systemHandlers = new LeanEngineSystemHandlers(_jobQueue, _api, _notify);
            systemHandlers.Initialize();

            var algorithmHandlers = new LeanEngineAlgorithmHandlers(_resultshandler, _setup, _dataFeed, _transactions, _realTime, _historyProvider);
            string algorithmPath;
            
            AlgorithmNodePacket job = systemHandlers.JobQueue.NextJob(out algorithmPath);
            try
            {
                var _engine = new Engine(systemHandlers, algorithmHandlers, Config.GetBool("live-mode"));
                _engine.Run(job, algorithmPath);
            }
            finally
            {
                //Delete the message from the job queue:
                //systemHandlers.JobQueue.AcknowledgeJob(job);
                Log.Trace("Engine.Main(): Packet removed from queue: " + job.AlgorithmId);

                // clean up resources
                systemHandlers.Dispose();
                algorithmHandlers.Dispose();
                Log.LogHandler.Dispose();
            }

        }

    }
    class MainClass
    {
        //private static RunClass rc;
        private static int runnumber = 0;
        private static AppDomainSetup _ads;
        private static string _callingDomainName;
        private static string _exeAssembly;
        public static void Main(string[] args)
        {


            //Initialize:
            string mode = "RELEASE";
            var liveMode = Config.GetBool("live-mode");
            

#if DEBUG
            mode = "DEBUG";
#endif


            //			Console.WriteLine("Running " + algorithm + "...");
            Config.Set("live-mode", "false");
            Config.Set("messaging-handler", "QuantConnect.Messaging.Messaging");
            Config.Set("job-queue-handler", "QuantConnect.Queues.JobQueue");
            Config.Set("api-handler", "QuantConnect.Api.Api");
            //Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.OptimizationResultHandler");
            Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.ConsoleResultHandler");
            //Config.Set("EMA_VAR1", "10");

            _ads = SetupAppDomain();


            //rc = new RunClass();
            const double crossoverProbability = 0.65;
            const double mutationProbability = 0.08;
            const int elitismPercentage = 5;

            

            List<string > algos = new List<string>();
            //algos.Add("DecycleInverseFisherAlgorithm");
            //algos.Add("CyberCycleAlgorithm");
            //algos.Add("InstantTrendAlgorithmOriginal");
            //algos.Add("InstantaneousTrendAlgorithmQC");
            //algos.Add("InstantaneousTrendAlgorithm");
            algos.Add("MultiSignalAlgorithm");
            algos.Add("MultiSignalAlgorithmQC");
            //algos.Add("MultiSignalAlgorithmTicketQueue2");
            
            RunAlgorithm(algos);

            //create the population
            //var population = new Population(100, 44, false, false);

            //var population = new Population();

            ////create the chromosomes
            //for (var p = 0; p < 100; p++)
            //{
            //    var chromosome = new Chromosome();
            //    for (int i = 0; i < 100; i++)
            //        chromosome.Genes.Add(new Gene(i));
            //    chromosome.Genes.ShuffleFast();
            //    population.Solutions.Add(chromosome);
            //}



            ////create the genetic operators 
            //var elite = new Elite(elitismPercentage);

            //var crossover = new Crossover(crossoverProbability, true)
            //{
            //    CrossoverType = CrossoverType.SinglePoint
            //};

            //var mutation = new BinaryMutate(mutationProbability, true);

            ////create the GA itself 
            //var ga = new GeneticAlgorithm(population, CalculateFitness);

            ////subscribe to the GAs Generation Complete event 
            //ga.OnGenerationComplete += ga_OnGenerationComplete;

            ////add the operators to the ga process pipeline 
            //ga.Operators.Add(elite);
            //ga.Operators.Add(crossover);
            //ga.Operators.Add(mutation);

            ////run the GA 
            //ga.Run(Terminate);
        }
        static AppDomainSetup SetupAppDomain()
        {
            _callingDomainName = Thread.GetDomain().FriendlyName;
            //Console.WriteLine(callingDomainName);

            // Get and display the full name of the EXE assembly.
            _exeAssembly = Assembly.GetEntryAssembly().FullName;
            //Console.WriteLine(exeAssembly);

            // Construct and initialize settings for a second AppDomain.
            AppDomainSetup ads = new AppDomainSetup();
            ads.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;

            ads.DisallowBindingRedirects = false;
            ads.DisallowCodeDownload = true;
            ads.ConfigurationFile =
                AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            return ads;
        }

        static RunClass CreateRunClassInAppDomain(ref AppDomain ad)
        {

            // Create the second AppDomain.
            var name = Guid.NewGuid().ToString("x");
            ad = AppDomain.CreateDomain(name, null, _ads);

            // Create an instance of MarshalbyRefType in the second AppDomain. 
            // A proxy to the object is returned.
            RunClass rc =
                (RunClass)ad.CreateInstanceAndUnwrap(
                    _exeAssembly,
                    typeof(RunClass).FullName
                );

            return rc;
        }

        //static void ga_OnRunComplete(object sender, GaEventArgs e)
        //{
        //    var fittest = e.Population.GetTop(1)[0];
        //    foreach (var gene in fittest.Genes)
        //    {
        //        Log.Trace(System.Convert.ToString((int) gene.RealValue));
        //    }
        //}

        //private static void ga_OnGenerationComplete(object sender, GaEventArgs e)
        //{
        //    var fittest = e.Population.GetTop(1)[0];
        //    var sharpe = RunAlgorithm(fittest);
        //    Log.Trace("Generation: {0}, Fitness: {1},Distance: {2}", e.Generation, fittest.Fitness, sharpe);
        //}

        //public static double CalculateFitness(Chromosome chromosome)
        //{
        //    var sharpe = RunAlgorithm(chromosome);
        //    return sharpe;
        //}

        private static double RunAlgorithm(List<string> algos )
        {
            string f = AssemblyLocator.ExecutingDirectory();
            var sum_sharpe = 0.0;
            foreach (string s in algos)
            {
                var val = s;
                AppDomain ad = null;
                RunClass rc = CreateRunClassInAppDomain(ref ad);
                Console.WriteLine("Running algorithm {0} with value: {1}", runnumber, val);
                
                try
                {
                    sum_sharpe += (double)rc.Run(val);
                    
                }
                catch (Exception e)
                {
                    Log.Error(e.Message + e.StackTrace);
                }
                AppDomain.Unload(ad);
                try
                {
                    string destfile = f + string.Format(@"mylog{0}.csv",s);
                    if (File.Exists(destfile))
                        File.Delete(destfile);
                    File.Move(f + @"mylog.csv", destfile);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                runnumber++;
            }

            return sum_sharpe;
        }

        //public static bool Terminate(Population population,
        //    int currentGeneration, long currentEvaluation)
        //{
        //    return currentGeneration > 400;
        //}


    }
}

