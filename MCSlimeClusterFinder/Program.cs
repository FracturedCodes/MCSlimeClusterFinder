﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using Mono.Options;
using MoreLinq;

// Author FracturedCode

// This algorithm isn't perfect, it's just meant as a starting point to find areas that will be useful.
// It all depends on where the player stands to maximize the number of spawning platforms.
// For instance, one of my outputs was labeled as 56, but when tested in the minecraft world, had 58 chunks in range.

/*
>.\MCSlimeClusterFinder -h
Usage: MCSlimeClusterFinder -s WORLD_SEED [OPTIONS]

  -s, --seed=VALUE           the world seed, type long
  -l, --length=VALUE         the length, in blocks, of the square search area
                               centered on 0,0
  -t, --threads=VALUE        the number of cpu threads to run concurrently
  -o, --out=VALUE            the file to save the results
  -h, --help                 show this message and exit

>.\MCSlimeClusterFinder -s 420 -l 20000 -t 4
Brute force searching. Starting 4 threads
Aggregate: 99.00%   Individual: 99%     99%     99%     99%
Brute force search complete using a maximum of 0GB of memory
BruteForceAllTheChunksLMFAO completed in 00:00:01.412
Found 0 candidates with a max of 0 slime chunks.
Seed: 420       Area: 1250^2 chunks

Total runtime completed in 00:00:01.415
*/

namespace MCSlimeClusterFinder
{
    public class Program
    {
        protected const int _threshold = 45;
        private const string _fallbackOutput = "output.txt";

        public static void Main(string[] args)
        {
            Program p = ParseArgs(args);
            if (p == null) System.Environment.Exit(-1);
            Time(p.Run, "Total runtime");
        }
        public static Program ParseArgs(String[] args)
        {
            int length = 20000;
            long worldSeed = 0;
            bool seedInput = false;
            int threads = Environment.ProcessorCount - 2;
            bool shouldShowHelp = false;
            string outputFile = "candidates.txt";

            try
            {
                var options = new OptionSet
                {
                    { "s|seed=", "the world seed, type long", (long s) => {worldSeed = s; seedInput = true; } },
                    { "l|length=", "the length, in blocks, of the square search area centered on 0,0", (int l) => length = l },
                    { "t|threads=", "the number of cpu threads to run concurrently", (int t) => threads = t },
                    { "o|out=", "the file to save the results",  o => outputFile = o },
                    { "h|help", "show this message and exit", h => shouldShowHelp = h != null }
                    
                };
                options.Parse(args);
                if (shouldShowHelp)
                {
                    Console.WriteLine("Usage: MCSlimeClusterFinder -s WORLD_SEED [OPTIONS]\n");
                    options.WriteOptionDescriptions(Console.Out);
                    Console.WriteLine();
                    return null;
                }
                if (!seedInput)
                {
                    Console.Write ("MCSlimeClusterFinder: ");
                    Console.WriteLine("\tYou must provide a world seed with -s=SEED\n");
                    Console.WriteLine ("Try `MCSlimeClusterFinder --help' for more information.");
                    return null;
                }
            } catch (OptionException e) {
                Console.Write ("MCSlimeClusterFinder: ");
                Console.WriteLine (e.Message);
                Console.WriteLine ("Try `MCSlimeClusterFinder --help' for more information.");
                return null;
            }

            return new Program(length, worldSeed, threads, outputFile);
        }

        protected static List<(int x, int z)> _deltas { get; } = CreateDeltas();
        protected int _length { get; }
        private int _chunkHalfLength { get; }
        private int _threadCount { get; } // with the POWA OF AMD, I SUMMON *YOU*! RYZEN 3600
        protected long _worldSeed { get; }
        private string _outputFile { get; }
        public List<(int x, int z, int sc)> Candidates { get; } = new List<(int x, int z, int sc)>();


        public Program(int length, long worldSeed, int threads, string outputFile)
        {
            _length = length;
            _chunkHalfLength = _length / 32;
            _threadCount = threads;
            _worldSeed = worldSeed;
            _outputFile = outputFile;
        }

        public void Run()
        {
            Time(BruteForceAllTheChunksLMFAO);
            SaveAndPrintOutput();
        }
        
        static List<(int, int)> CreateDeltas()
        {
            // Creates deltas of chunks within 128 blocks radius for any generic chunk.
            var deltas = new List<(int, int)>();
            for (int i = -8; i < 9; i++)
            {
                for (int j = -8; j < 9; j++)
                {
                    // 128 / 16 = 8
                    if (Math.Sqrt(Math.Pow(i, 2) + Math.Pow(j, 2)) <= 8.0)
                        deltas.Add((i, j));
                }
            }
            return deltas;
        }

        private void SaveAndPrintOutput()
        {
            string output = $"Found {Candidates.Count} candidates with a max of {(Candidates.Any() ? Candidates.Max(c => c.sc) : 0)} slime chunks. \nSeed: {_worldSeed}\tArea: {_chunkHalfLength*2}^2 chunks\n";
            if (Candidates.Any())
            {
                var individualOrdered = Candidates.OrderByDescending(c => c.sc).Select(c => $"{c.x}, {c.z}, {c.sc}");
                string fileOutput = individualOrdered.Aggregate((x, y) => $"{x}\n{y}");
                Console.Write(output + "Saving...");
                try
                {
                    File.WriteAllText(_outputFile, output + fileOutput);
                } catch
                {
                    Console.WriteLine($"File write failed, reattempting with fallback {_fallbackOutput}");
                    File.WriteAllText(_fallbackOutput, output + fileOutput);
                }
            
                Console.WriteLine("Complete\nTop 10 List:\n" + individualOrdered.Take(10).Aggregate((x, y) => $"{x}\n{y}"));
            } else
            {
                Console.WriteLine(output);
            }
        }

        void BruteForceAllTheChunksLMFAO()
        {
            Console.WriteLine($"Brute force searching. Starting {_threadCount} threads");

            var threadObjects = new ThreadParams[_threadCount];
            int sectionLength = (_chunkHalfLength * 2 - 16) / _threadCount;

            for (int i = 0; i < _threadCount; i++)
            {
                // We're moving a circle, not a point around.
                // This means we have to adjust by 8 chunks so we don't go searching through data we don't have.
                // Hence the ternary and the +/-8
                threadObjects[i] = new ThreadParams()
                {
                    StartX = -_chunkHalfLength + 8 + i * sectionLength,
                    StopX = i == _threadCount - 1 ? _chunkHalfLength - 8 : -_chunkHalfLength + 8 + ((i + 1) * sectionLength),
                    ChunkHalfLength = _chunkHalfLength
                };
                var th = new Thread(WorkerThread);
                th.Start(threadObjects[i]);
            }

            double threadWeight = 1.0 / _threadCount;
            long greatestTotalMemory = 0;
            while (!threadObjects.All(tp => tp.Complete))
            {
                Thread.Sleep(200);
                string threadPercentLine = "";
                double percentComplete = 0.0;
                threadObjects.ForEach(tp =>
                {
                    percentComplete += tp.PercentComplete / 100.0 * threadWeight;
                    threadPercentLine += $"\t{tp.PercentComplete}%";
                });

                if (GC.GetTotalMemory(false) > greatestTotalMemory)
                    greatestTotalMemory = GC.GetTotalMemory(false);

                string output = $"\rAggregate: {percentComplete:P}   Individual:" + threadPercentLine;
                Console.Write(output);
            }

            Console.WriteLine($"\nBrute force search complete using a maximum of {(greatestTotalMemory/(double)1000000000):0.##}GB of memory");
        }

        public class ThreadParams
        {
            public int StartX;
            public int StopX;
            public int ChunkHalfLength;
            public int PercentComplete;
            public bool Complete;
        }
        protected void WorkerThread(Object param)
        {
            ThreadParams tParams = (ThreadParams)param;

            int startX = tParams.StartX;    // Optimizing by putting oft-used vars on the heap, maybe it's unnecessary, but I assume compiler or interpreter don't do this automagically
            int stopX = tParams.StopX;
            int diff = stopX - startX;
            int chunkHalfLength = tParams.ChunkHalfLength;

            for (int x = startX; x < stopX; x++)
            {
                if ((int)((x - startX) / (double)(diff) * 100) > tParams.PercentComplete)
                    tParams.PercentComplete++; // TODO Only works with large borders, ie > a thousand

                for (int z = -chunkHalfLength + 8; z < chunkHalfLength / 2 - 7; z++)
                {
                    int slimeRadiusCounter = 0;
                    foreach (var delta in _deltas)
                    {
                        if (isSlimeChunk(x + delta.x, z + delta.z))
                            slimeRadiusCounter++;
                    }
                    if (slimeRadiusCounter >= _threshold)
                        Candidates.Add((x, z, slimeRadiusCounter));
                }
            }
            tParams.Complete = true;
        }
        protected bool isSlimeChunk(int x, int z)
        {
            // Implementation of this from java:
            // new Random(seed + (long) (i * i * 4987142) + (long) (i * 5947611) + (long) (j * j) * 4392871L + (long) (j * 389711) ^ 987234911L).nextInt(10) == 0
            long seed = ((_worldSeed + (long) (x * x * 4987142) + (long) (x * 5947611) + (long) (z * z) * 4392871L + (long) (z * 389711) ^ 987234911L) ^ 0x5DEECE66DL) & ((1L << 48) - 1);
            int bits, val;
            do
            {
                seed = (seed * 0x5DEECE66DL + 0xBL) & ((1L << 48) - 1);
                bits = (int)((ulong)seed >> 17);
                val = bits % 10;
            } while (bits - val + 9 < 0);
            return val == 0;
        }

        protected static TimeSpan Time(Action action, string actionName = null)
        {
            var sw = new Stopwatch();
            sw.Start();
            action.Invoke();
            sw.Stop();
            Console.WriteLine($"{actionName ?? action.Method.Name} completed in {sw.Elapsed:hh\\:mm\\:ss\\.fff}");
            return sw.Elapsed;
        }
    }
}