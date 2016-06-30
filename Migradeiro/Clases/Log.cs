using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Migradeiro.Clases
{
    class Log
    {
        public string logRoute { get; set; }
        public string logName { get; set; }

        public Log(string logRoute, string logName)
        {
            this.logRoute = logRoute;
            this.logName = logName;
        }

        public void WriteLine(string message, string mode = "INFO")
        {
            using (StreamWriter sw = File.AppendText(Path.Combine(logRoute, logName)))
            {
                string logDate = DateTime.Now.ToShortDateString() + ":" + DateTime.Now.ToLongTimeString();
                sw.WriteLine(logDate + "({0}) - " + message, mode);
            }
        }
    }
}
