using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Diagnostics;
using System.Threading;

namespace powercal
{
    class MultiMeter
    {
        public bool WaitForDsrHolding
        {
            get { return _waitForDsrHolding; }
            set { _waitForDsrHolding = value; }
        }

        private bool _waitForDsrHolding = true;
        private string _portName;
        private SerialPort _serialPort = new SerialPort();
        private string _value_txt = "";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="portName"></param>
        public MultiMeter(string portName)
        {
            this._portName = portName;
        }

        /// <summary>
        /// Open serial port
        /// </summary>
        /// <returns></returns>
        public SerialPort OpenComPort()
        {
            //if (_serialPort != null && _serialPort.IsOpen)
            //{
            _serialPort.Close();
            //}
            //_serialPort = new SerialPort(_portName, 600, Parity.None, 8, StopBits.One);
            _serialPort.PortName = _portName;
            _serialPort.BaudRate = 9600;
            _serialPort.Parity = Parity.None;
            _serialPort.DataBits = 8;
            _serialPort.StopBits = StopBits.One;
            _serialPort.Handshake = Handshake.None;
            _serialPort.DtrEnable = true;

            _serialPort.DataReceived += _serialPort_DataReceived;
            _serialPort.Open();

            return _serialPort;
        }

        /// <summary>
        /// Handle data recieved
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            lock (_value_txt)
            {
                _value_txt += _serialPort.ReadExisting();
            }
        }

        /// <summary>
        /// Waits for data
        /// </summary>
        /// <returns></returns>
        string waitForData()
        {
            int n = 0;
            while (_value_txt == "")
            {
                Thread.Sleep(100);
                n++;
                if (n > 5)
                {
                    break;
                }
            }
            n = 0;
            while (_serialPort.BytesToRead > 0)
            {
                Thread.Sleep(250);
                n++;
                if (n > 10)
                {
                    break;
                }
            }

            return _value_txt;
        }

        /// <summary>
        /// Clears our data holder
        /// </summary>
        void clearData()
        {
            lock (_value_txt)
                _value_txt = "";
        }

        /// <summary>
        /// Writes to meter
        /// </summary>
        /// <param name="cmd"></param>
        public void writeLine(string cmd)
        {
            int n = 0;

            if (_waitForDsrHolding)
            {
                while (!_serialPort.DsrHolding)
                {
                    Thread.Sleep(250);
                    n++;
                    if (n > 20)
                        throw new Exception("Multimeter not responding to serial commands.  Make sure multi-meter is on and serial cable connected");
                }
            }

            _serialPort.WriteLine(cmd);
            Thread.Sleep(250);

            n = 0;
            while (_serialPort.BytesToWrite > 0)
            {
                Thread.Sleep(100);
                n++;
                if (n > 20)
                    throw new Exception("Multimeter write buffer not empty");
            }
        }

        /// <summary>
        /// Clears the meters error status
        /// </summary>
        public void ClearError()
        {
            writeLine("*CLS");
        }

        /// <summary>
        /// Sets meter to remote mode
        /// </summary>
        public void SetToRemote()
        {
            writeLine("SYST:REM");
        }

        /// <summary>
        /// Gets the meter id
        /// </summary>
        /// <returns>meter id string</returns>
        public string IDN()
        {
            clearData();
            writeLine("*IDN?");
            string data = waitForData();
            return data;
        }

        /// <summary>
        /// Sets up the meter for V AC measurement
        /// </summary>
        public void SetupForVAC()
        {
            writeLine(":CONF:VOLT:AC 1000,0.01");
            writeLine(":TRIG:SOUR BUS");
        }

        /// <summary>
        /// Sets up the meter for I AC measurement
        /// </summary>
        public void SetupForIAC()
        {
            writeLine(":CONF:CURR:AC 1,0.000001");
            writeLine(":TRIG:SOUR BUS");
        }

        /// <summary>
        /// Trigers meter and returns measurement
        /// </summary>
        /// <returns>measurement</returns>
        public string Measure()
        {
            clearData();
            writeLine(":INIT");
            writeLine("*TRG");
            writeLine(":FETC?");
            string data = waitForData();
            return data;
        }

        /// <summary>
        /// Closes the serial port
        /// </summary>
        public void CloseSerialPort()
        {
            this._serialPort.Close();
        }
    }
}
