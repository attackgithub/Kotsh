// System
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// Modules
using Kotsh.Models;

namespace Kotsh.Instance
{
    /// <summary>
    /// Tasker is the main function used to run checks
    /// </summary>
    public class Tasker
    {
        /// <summary>
        /// Core instance
        /// </summary>
        private Manager core;

        /// <summary>
        /// Progression of proxies
        /// </summary>
        private int proxy_i = 0;

        /// <summary>
        /// Proxy Regex
        /// </summary>
        private string proxyRegex = @"\d{1,3}(\.\d{1,3}){3}:\d{1,5}";

        /// <summary>
        /// Store the core instance
        /// </summary>
        /// <param name="core">Kotsh instance</param>
        public Tasker(Manager core)
        {
            // Store the core
            this.core = core;
        }

        /// <summary>
        /// Return threads in integer (if not set, it will set 1 thread)
        /// </summary>
        /// <returns>Thread Count</returns>
        private int GetThreads()
        {
            // Check if threads are set
            if (core.runStats.Get("threads") == null)
            {
                // No threads
                return 1; // Set on 1 thread
            }
            else
            {
                // Threads are set
                return int.Parse(core.runStats.Get("threads"));
            }
        }

        /// <summary>
        /// Get a proxy and increment progression
        /// </summary>
        /// <param name="increment">If true, it will increment proxies</param>
        /// <returns>Proxy as host:port</returns>
        public string GetProxy(bool increment = false)
        {
            // Check proxies
            if (this.proxy_i >= core.Proxies.Count)
            {
                this.proxy_i = 0;
            }

            // Get proxy
            string host = core.Proxies.Keys[this.proxy_i];
            string port = core.Proxies.Get(host);

            // Associate proxy
            string proxy = host + ":" + port;

            // Increment proxy
            if (increment)
            {
                this.proxy_i++;
            }

            // Parse proxy
            Match match = Regex.Match(proxy, proxyRegex);
            if (match.Success)
            {
                return match.Groups[0].Value;
            }
            else
            {
                // Recursive call
                return GetProxy();
            }
        }

        /// <summary>
        /// Check every combo using multi-threading
        /// </summary>
        /// <param name="function">Checking function</param>
        public void RunCombo(Func<string, Response> function)
        {
            // Open file stream
            var stream = File.ReadLines(core.runSettings["combolist"]);

            // Store line 
            core.runStats["count"] = stream.Count().ToString();

            // Set on started
            core.status = 1;

            // Log CPM
            RegisterCPM();

            // Assign threads
            Parallel.ForEach(
                // File stream
                stream,
                // Parallel Options
                new ParallelOptions
                {
                    // Max threads 
                    MaxDegreeOfParallelism = GetThreads()

                    // Combo => Line
                    // Controller => Parallel control variable
                    // Count => Number of lines
                }, (combo, controller, count) =>
                {
                    // Execute combo
                    Response res = function.Invoke(combo);

                    // Handle banned or retry
                    while (res.type == (Models.Type.BANNED | Models.Type.RETRY))
                    {   
                        // Relaunch check
                        res = function.Invoke(combo);
                    }

                    // Call response handler
                    core.Handler.Check(res);
                }
            );

            // Set on finished
            core.status = 2;

            // Update title
            core.Program.UpdateTitle();
        }

        /// <summary>
        /// Support used for infinite loops
        /// </summary>
        /// <returns>Boolean</returns>
        private IEnumerable<bool> Infinite()
        {
            while (true)
            {
                yield return true;
            }
        }

        /// <summary>
        /// Execute function into a infinite loop
        /// </summary>
        /// <param name="function">Checking function</param>
        public void RunInfinite(Func<Response> function)
        {
            // Store line 
            // TODO: Actually set on max int value
            core.runStats["count"] = int.MaxValue.ToString();

            // Set on started
            core.status = 1;

            // Log CPM
            RegisterCPM();

            // Assign threads
            Parallel.ForEach(
                // Infinite stream
                Infinite(),
                // Parallel Options
                new ParallelOptions
                {
                    // Max threads 
                    MaxDegreeOfParallelism = GetThreads()
                },
                // Arguments
                new Action<bool>((val) => 
                {
                    // Execute combo
                    Response res = function.Invoke();

                    // Handle banned or retry
                    while (res.type == (Models.Type.BANNED | Models.Type.RETRY))
                    {
                        // Relaunch check
                        res = function.Invoke();
                    } 

                    // Call response handler
                    core.Handler.Check(res);
                }
            ));

            // Set on finished
            core.status = 2;

            // Update title
            core.Program.UpdateTitle();
        }

        /// <summary>
        /// Starts a thread to log CPM
        /// </summary>
        private void RegisterCPM()
        {
            // Start CPM calculator thread
            Task.Run(() =>
            {
                // While checking
                while (core.status == 1)
                {
                    // Get initial checks
                    int initial_check = int.Parse(core.runStats["checked"]);

                    // Wait 2 seconds
                    Thread.Sleep(2000);

                    // Get actual checks
                    int actual_check = int.Parse(core.runStats["checked"]);

                    // Calculate it
                    int cpm = (actual_check - initial_check) * 30;

                    // Assign CPM
                    core.runStats["cpm"] = cpm.ToString();
                }
            });
        }
    }
}
