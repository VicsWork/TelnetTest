using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MinimalisticTelnet;
using powercal;
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
                int current_int = Convert.ToInt32(current_hexstr, 16);
                double current = RegHex_ToDouble(current_int);
                current = current * 15 / 0.6;

                match = Regex.Match(datain, rawVoltagePattern);
                string voltage_hexstr = match.Groups[1].Value;
                int volatge_int = Convert.ToInt32(voltage_hexstr, 16);
                double voltage = RegHex_ToDouble(volatge_int);
                voltage = voltage * 240 / 0.6;


                MultiMeter meter = new MultiMeter("COM1");
                meter.OpenComPort();

                meter.SetupForIAC();
                string meter_current_str = meter.Measure();
                double meter_current = Double.Parse(meter_current_str);

                meter.SetupForVAC();
                string meter_voltage_str = meter.Measure();
                double meter_voltage = Double.Parse(meter_voltage_str);



                meter.CloseSerialPort();
                int test = 1;
            }

            /// <summary>
            /// Converts a 24bit hex (3 bytes) CS register value to a double
            /// </summary>
            /// <example>
            /// byte[] rx_data = new byte[3];
            /// rx_data[2] = 0x5c;
            /// rx_data[1] = 0x28;
            /// rx_data[0] = 0xf6;
            /// Should return midrange =~ 0.36
            /// </example>
            /// <param name="rx_data">data byte array byte[2] <=> MSB ... byte[0] <=> LSB</param>
            /// <returns>range 0 <= value < 1.0</returns>
            private static double RegHex_ToDouble(int data)
            {
                // Maximum 1 =~ 0xFFFFFF
                // Max rms 0.6 =~ 0x999999
                // Half rms 0.36 =~ 0x5C28F6
                double value = ((double)data) / 0x1000000; // 2^24
                return value;
            }

            private static double RegHex_ToDouble(string hexstr)
            {
                int val_int = Convert.ToInt32(hexstr, 16);
                return RegHex_ToDouble(val_int); ;
            }

        }
    }
}