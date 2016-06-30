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

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace Migradeiro
{
    public partial class Migradeiro : ServiceBase
    {
        // Variables globales
        public string logRoute = @"C:\Migradeiro\Logs\";
        public string tempRoute = @"C:\Migradeiro\Temp\";
        public string tempFile = "HLRTemp.txt";
        public string logFile = "MIGRALOG.txt";

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
        }

        // Función de parada
        ///////////////////////////////////////////////////////////////////////////////////////////////////
        protected override void OnStop()
        {
        }
    }
}
