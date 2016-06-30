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


///////////////////////////////////////////////////////////////////////////////////////////////////

namespace Migradeiro
{
    public partial class Migradeiro : ServiceBase
    {
        // Variables globales
        private string logRoute = @"C:\Ejecutables\Migra2\LOGS\";
        private string tempRoute = @"C:\Ejecutables\Migra2\Temp\";
        private string logFile = "MIGRALOG.txt";
        private Log log;
        private TelnetConnection tc;
        OracleConnection conn;
        OracleDataReader reader;

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
            System.Timers.Timer timer = new System.Timers.Timer();
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
            try
            {
                tc = new TelnetConnection("10.2.144.75", 23);
                log.WriteLine("Conexión realizada correctamente");
            }
            catch (Exception e)
            {
                log.WriteLine("No se ha podido abrir la conexión TELNET", "ERROR");
                log.WriteLine("Mensaje: " + e.Message.ToString(), "ERROR");
                return;
            }
            string s = DateTime.Now.ToShortDateString().Replace("/", "");
            s += "-" + DateTime.Now.ToShortTimeString().Replace(":", "") + ".txt";
            Thread.Sleep(600);
            tc.Write("ingenieria\n\r");
            Thread.Sleep(600);
            tc.Write("ing2010AXE\n\r");
            Thread.Sleep(600);
            tc.Write("\n\r");
            Thread.Sleep(600);
            tc.WriteLine("mml\n\r");
            Thread.Sleep(600);
            StreamWriter sw = new StreamWriter(Path.Combine(tempRoute,s));
            sw.WriteLine("Fecha de creación del informe: " + DateTime.Now.ToLongDateString());
            sw.WriteLine("Resultados de consulta de HLR");
            sw.WriteLine();
            sw.Write(tc.Read() + "\n\r");
            log.WriteLine("Autenticado");
            log.WriteLine("Petición de líneas en espera");
            //tc.Write("HGICP:NIMSI=ALL;\n\r");                       // Línea de pruebas
            tc.Write("HGICP:NIMSI=ALL,EXEC;\n\r");                //Línea de ejecución
            log.WriteLine("Estableciendo conexión con BBDD");
            Thread.Sleep(500);
            sw.Write(tc.Read());
            sw.Close();
            try
            {
                conn = new OracleConnection(@"Data Source=migxunta; User ID=migxunta; Password=migxunta");
                conn.Open();
                log.WriteLine("Conexión abierta con BBDD");
            }
            catch (Exception e)
            {
                log.WriteLine("No se ha podido abrir la conexión con BBDD", "ERROR");
                log.WriteLine("Error code: " + e.Message, "ERROR");
                return;
            }
            StreamReader sr = new StreamReader(Path.Combine(tempRoute, s));
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
                        string msisdn = splitted[0];
                        string sql = "SELECT * FROM MIGHOST_CHEQUEONAV WHERE(MSISDN=:msisdn)";
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
                        if (!reader.HasRows)
                        {
                            if (!reader.HasRows)
                            {
                                sql = "INSERT INTO MIGHOST_CHEQUEONAV VALUES (:msisdn , \'pendiente\')";
                                comm.CommandText = sql;
                                try
                                {
                                    int rowsAffected = comm.ExecuteNonQuery();
                                    log.WriteLine("Se ha añadido el número " + msisdn.ToString());
                                }
                                catch (Exception e)
                                {
                                    log.WriteLine("Problema al agregar la línea", "ERROR");
                                    log.WriteLine(e.Message, "ERROR");
                                    continue;
                                }
                            }
                        }
                    }
                }
            }
            log.WriteLine("Cerrando conexión con BBDD");
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
