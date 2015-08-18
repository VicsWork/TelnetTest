using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MinimalisticTelnet;
using System.Text.RegularExpressions;

namespace ConsoleApplication1
{
    class TelnetTest
    {
        class Program
        {
            static void Main(string[] args)
            {
                //create a new telnet connection
                TelnetConnection tc = new TelnetConnection("localhost", 4900);

                bool isconnected = tc.IsConnected;

                tc.WriteLine("cu cs5480_pload");
                string datain = tc.Read();

                string rawCurrentPattern = "Raw IRMS: ([0-9,A-F]{8})";
                string rawVoltagePattern = "Raw VRMS: ([0-9,A-F]{8})";

                Match match = Regex.Match(datain, rawCurrentPattern);
                string current_hexstr = match.Groups[1].Value;
                match = Regex.Match(datain, rawVoltagePattern);
                string voltage_hexstr = match.Groups[1].Value;

                while (true)
                {

                    datain = tc.Read();
                }

            }
        }
    }
}