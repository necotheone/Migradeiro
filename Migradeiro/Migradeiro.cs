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
using System.IO;
using System.ServiceProcess;
using System.Timers;
using MinimalisticTelnet;
using Migradeiro.Clases;
using System.Threading;
using System.Text.RegularExpressions;
using System.Data.OracleClient;
using System.Configuration;


///////////////////////////////////////////////////////////////////////////////////////////////////

namespace Migradeiro
{
    public partial class Migradeiro : ServiceBase
    {
        // Variables globales
        private string logRoute = ConfigurationManager.AppSettings["rutaLog"];   //Línea de ruta en CONF
        private string tempRoute = ConfigurationManager.AppSettings["rutaTemp"];  //Línea de ruta en CONF
        private string logFile = ConfigurationManager.AppSettings["logName"];
        private string tempFile = ConfigurationManager.AppSettings["tempName"];
        private string hlrip = ConfigurationManager.AppSettings["HLRIP"];
        private string hlrstrport = ConfigurationManager.AppSettings["HLRPORT"];
        private string oraConnString = ConfigurationManager.AppSettings["OraConnString"];
        private string timeInterval = ConfigurationManager.AppSettings["timeInterval"];
        private string user = ConfigurationManager.AppSettings["USER"];
        private string password = ConfigurationManager.AppSettings["PASSWORD"];
        private int intInterval = 120000;
        private int hlrport;
        private Log log;
        private TelnetConnection tc;
        OracleConnection conn;
        OracleDataReader reader;

        // Inicia aquí la conexión con la BBDD. Para ello tiene que existir la línea de conexión correspondiente
        // en el fichero de tnsnames.ora

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
            int.TryParse(ConfigurationManager.AppSettings["HLRPORT"],out hlrport);
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
            System.Timers.Timer timer = new System.Timers.Timer();
            Int32.TryParse(timeInterval, out intInterval);
            Int32.TryParse(hlrstrport, out hlrport);
            timer.Interval = intInterval;
            timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer.Start();
        }

        // Función de interrupción del temporizador
        ///////////////////////////////////////////////////////////////////////////////////////////////////
        public void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            // Función principal de servicio. Cada dos minutos, el timer lanzará un evento que se recoge en esta 
            // función. Se debe hacer un telnet al HLR y procesar el fichero resultante hacia la BBDD.
            try
            {
                tc = new TelnetConnection(hlrip, hlrport);
            }
            catch (Exception e)
            {
                log.WriteLine("No se ha podido abrir la conexión TELNET", "ERROR");
                log.WriteLine("Mensaje: " + e.Message.ToString(), "ERROR");
                return;
            }
            Thread.Sleep(600);
            tc.Write("ingenieria\n\r");
            Thread.Sleep(600);
            tc.Write("ing2010AXE\n\r");
            Thread.Sleep(600);
            tc.Write("\n\r");
            Thread.Sleep(600);
            tc.WriteLine("mml\n\r");
            Thread.Sleep(600);
            StreamWriter sw = new StreamWriter(Path.Combine(tempRoute,tempFile));
            sw.WriteLine("Fecha de creación del informe: " + DateTime.Now.ToLongDateString());
            sw.WriteLine("Resultados de consulta de HLR");
            sw.WriteLine();
            sw.Write(tc.Read() + "\n\r");
            tc.Write("HGICP:NIMSI=ALL,EXEC;\n\r");
            Thread.Sleep(500);
            sw.Write(tc.Read());
            sw.Close();
            try
            {
                oraConnString.Replace("USER", user);
                conn = new OracleConnection(oraConnString + password);
                conn.Open();
            }
            catch (Exception e)
            {
                log.WriteLine("No se ha podido abrir la conexión con BBDD", "ERROR");
                log.WriteLine("Error code: " + e.Message, "ERROR");
                return;
            }
            StreamReader sr = new StreamReader(Path.Combine(tempRoute, tempFile));
            string line;
            string[] splitted;
            while ((line = sr.ReadLine()) != null)
            {
                if ((line.Length == 0) || (line.Length < 6)) continue;
                if (line.Substring(0, 6) == "MSISDN")
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line == "END") break;                   // El HLR termina con un END su respuesta
                        line = Regex.Replace(line, @"\s+", " ");    // Reemplaza varios espacios por uno solo
                        splitted = line.Split(' ');                 // Separa la respuesta por espacios
                                                                    // splitted[0] es el MSISDN
                        if ((!Regex.IsMatch(splitted[0], "[0-9]"))
                            || (splitted[0].Length != 11))
                            break;                                  // Si no es un número de 11 dígitos, fin.
                        string msisdn = splitted[0].Substring(2);
                        string sql = "SELECT MSISDN,ESTADO FROM MIGHOST.MIGHOST_CHEQUEO_REG WHERE (MSISDN=:msisdn) AND (ESTADO=\'Pendiente\')";
                        OracleCommand comm = conn.CreateCommand();
                        comm.Parameters.Add(new OracleParameter("msisdn", msisdn));
                        comm.CommandText = sql;
                        try
                        {
                            reader = comm.ExecuteReader();
                        }
                        catch (Exception e)
                        {
                            log.WriteLine("No se ha podido recuperar información de BBDD", "ERROR");
                            log.WriteLine("Error code: " + e.Message, "ERROR");
                            continue;
                        }
                        if (reader.HasRows)
                        {
                            if (reader.HasRows)
                            {
                                sql = "UPDATE MIGHOST.MIGHOST_CHEQUEO_REG SET ESTADO=\'registrado\' WHERE MSISDN=:msisdn";
                                comm.CommandText = sql;
                                try
                                {
                                    int rowsAffected = comm.ExecuteNonQuery();
                                    log.WriteLine("Actualizado estado de " + msisdn.ToString() + " a \"registrado\"");
                                }
                                catch (Exception e)
                                {
                                    log.WriteLine("Problema al actualizar la línea", "ERROR");
                                    log.WriteLine(e.Message, "ERROR");
                                    continue;
                                }
                            }
                        }
                    }
                }
            }
            conn.Close();
            sr.Close();
        }

        // Función de parada
        ///////////////////////////////////////////////////////////////////////////////////////////////////
        protected override void OnStop()
        {
            using (StreamWriter sw = File.AppendText(Path.Combine(logRoute, logFile)))
            {
                sw.WriteLine();
                sw.WriteLine("                        FIN DE SERVICIO                       ");
                sw.WriteLine("--------------------------------------------------------------");
                sw.WriteLine();
                sw.Close();
            }
        }
    }
}
