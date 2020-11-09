﻿using Mono.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace MCSlimeClusterFinder
{
    public class MainThread
    {
        private static Supervisor workSupervisor { get; set; }
        private static SettingsResults settingsResults { get; } = new SettingsResults();
        public static void Main(string[] args)
        {
            if (!parseArgs(args))
            {
                return;
            }
            workSupervisor = new Supervisor(settingsResults);
            workSupervisor.Start();
            waitForWorkEnd();
            System.IO.File.AppendAllText(settingsResults.Settings.OutputFile, JsonSerializer.Serialize(settingsResults));
        }

        private static bool parseArgs(string[] args)
        {
            var stng = settingsResults.Settings;
            try
            {
                bool seedInput = false;
                bool shouldShowHelp = false;
                bool printReadme = false;

                var options = new OptionSet
                {
                    { "s|seed=", "the world seed, type long", (long s) => {stng.WorldSeed = s; seedInput = true; } },
                    { "o|out=", "the file to save the results",  o => stng.OutputFile = o },
                    { "h|help", "show this message and exit", h => shouldShowHelp = h != null },
                    { "start=", "work group step to start at. Learn more in readme (-r)", (long s) => stng.Start = s },
                    { "stop=", "work group step to stop at. Learn more in readme (-r)", (long s) => stng.Stop = s },
                    { "r|readme", "print the readme and exit", r => printReadme = r != null }
                };
                options.Parse(args);

                
                if (shouldShowHelp)
                {
                    Console.Write(optionsHeader);
                    options.WriteOptionDescriptions(Console.Out);
                    Console.WriteLine(optionsFooter);
                    return false;
                }
                else if (printReadme)
                {
                    throw new NotImplementedException();
                    return false;
                }
                else if (!seedInput)
                {
                    Console.WriteLine(getOptionsOutputString("A world seed must be specified with -s [world seed]"));
                    return false;
                }

            } catch (OptionException e)
            {
                Console.WriteLine(getOptionsOutputString(e.Message));
                return false;
            }
            return true;
        }

        private static string getOptionsOutputString(string content) =>
            optionsHeader + content + optionsFooter;
        private const string optionsHeader = "MCSlimeClusterFinder: \nUsage: MCSlimeClusterFinder -s WORLD_SEED [OPTIONS]\n\n";
        private const string optionsFooter = "Try `MCSlimeClusterFinder --help' for more information.";

        private static void waitForWorkEnd()
        {
            while (!workSupervisor.Completed)
            {
                Thread.Sleep(100);
                //TODO progress meter
            }
        }
    }
}