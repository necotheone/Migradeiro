// Migradeiro.cs
// Implementación del servicio de recopilación de datos Telnet del HLR
// ------------------------------------------------------------------------------------------------
// Autor: Fernando Gallego
// Rev.:   2.0
// Fecha:   25.04.2016
// Descripción: El programa establece una conexión Telnet con el HLR para solicitar las nuevas lí-
//              neas que se han registrado. Para ello, ejecuta un comando y escribe su respuesta 
//              en un fichero cada 2 minutos. Una vez escrito ese fichero, se procesa y se actua-
//              liza una base de datos en la que quedarán dichos MSISDN como pendientes de navega-
//              ción.
///////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using MinimalisticTelnet;
using Migradeiro.Clases;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace Migradeiro
{
    public partial class Migradeiro : ServiceBase
    {
        // Variables globales
        private string logRoute = @"C:\Ejecutables\Migra2\LOGS\";
        private string tempRoute = @"C:\Ejecutables\Migra2\Temp\";
        private string tempFile = "HLRTemp.txt";
        private string logFile = "MIGRALOG.txt";
        private Log log;

        // Inicia aquí la conexión con la BBDD. Para ello tiene que existir la línea de conexión correspondiente
        // en el fichero de tnsnames.ora
        // La línea de conexión a incluir es la siguiente:

        // MIGXUNTA=
        //  (DESCRIPTION=
        //   (ADDRESS=(PROTOCOL=TCP)(HOST=cor003s098)(PORT=1521))
        //   (CONNECT_DATA=
        //    (SERVER=DEDICATED)
        //    (SERVICE_NAME=migxunta)
        //   )
        //  )

        // Inicializador
        ///////////////////////////////////////////////////////////////////////////////////////////////////

        public Migradeiro()
        {
            InitializeComponent();
        }

        // Función de inicio
        ///////////////////////////////////////////////////////////////////////////////////////////////////
        protected override void OnStart(string[] args)
        {
            if (!File.Exists(Path.Combine(logRoute, logFile))) {
                StreamWriter sw = new StreamWriter(Path.Combine(logRoute, logFile));
                sw.Close();
            }
            using (StreamWriter sw = File.AppendText(Path.Combine(logRoute, logFile)))
            {
                sw.WriteLine("--------------------------------------------------------------");
                sw.WriteLine("                       SERVICIO INICIADO                      ");
                sw.WriteLine();
                sw.Close();
            }
            log = new Log(logRoute, logFile);
            Timer timer = new Timer();
            timer.Interval = 120000; // 120 segundos
            timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer.Start();
        }

        // Función de interrupción del temporizador
        ///////////////////////////////////////////////////////////////////////////////////////////////////
        public void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            // Función principal de servicio. Cada dos minutos, el timer lanzará un evento que se recoge en esta 
            // función. Se debe hacer un telnet al HLR y procesar el fichero resultante hacia la BBDD.
            TelnetConnection tc;
            try
            {
                tc = new TelnetConnection("10.2.144.75", 23);
            }
            catch (Exception e)
            {
                log.WriteLine("No se ha podido abrir la conexión TELNET", "ERROR");
                log.WriteLine("Mensaje: " + e.Message.ToString(), "ERROR");
            }
        }

        // Función de parada
        ///////////////////////////////////////////////////////////////////////////////////////////////////
        protected override void OnStop()
        {
            using (StreamWriter sw = File.AppendText(Path.Combine(logRoute, logFile)))
            {
                sw.WriteLine("                        FIN DE SERVICIO                       ");
                sw.WriteLine("--------------------------------------------------------------");
                sw.WriteLine();
                sw.Close();
            }
        }
    }
}
