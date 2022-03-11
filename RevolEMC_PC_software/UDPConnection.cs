using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Management;

namespace RevolEMC
{
    public class UDPSocket
    {
        private Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private const int bufSize = 8 * 1024;
        private State state = new State();
        private EndPoint epFrom = new IPEndPoint(IPAddress.Any, 0);
        private AsyncCallback recv = null;
        public class Data
        {
            public char action;
            public long steps;
        }
        public event EventHandler<Data> ReceivedData;

        public void setIP(string ip_address, string subnet_mask, string gateway)
        {
            try
            {
                ManagementClass objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
                ManagementObjectCollection objMOC = objMC.GetInstances();
                foreach (ManagementObject objMO in objMOC)
                {
                    if ((bool)objMO["IPEnabled"])
                    {
                        ManagementBaseObject newIP = objMO.GetMethodParameters("EnableStatic");
                        newIP["IPAddress"] = new string[] { ip_address };
                        newIP["SubnetMask"] = new string[] { subnet_mask };
                        objMO.InvokeMethod("EnableStatic", newIP, null);
                        ManagementBaseObject newGateway = objMO.GetMethodParameters("SetGateways");
                        newGateway["DefaultIPGateway"] = new string[] { gateway };
                        newGateway["GatewayCostMetric"] = new int[] { 1 };
                        objMO.InvokeMethod("SetGateways", newGateway, null);
                    }
                }
            }
            catch (Exception) { }
        }

        public class State
        {
            public byte[] buffer = new byte[bufSize];
        }

        public void Dispose()
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }

        public void Server(int port)
        {
            try
            {
                _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
                _socket.Bind(new IPEndPoint(IPAddress.Any, port));
                Receive();
            }
            catch (Exception) { }
        }

        public void Client(string address, int port)
        {
            try
            {
                _socket.Connect(IPAddress.Parse(address), port);
                Receive();
            }
            catch (Exception) { }
        }

        public void Send(string text)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(text);
                _socket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
                {
                    State so = (State)ar.AsyncState;
                    int bytes = _socket.EndSend(ar);
                    //Console.WriteLine("SEND: {0}, {1}", bytes, text);
                }, state);
            }
            catch (ObjectDisposedException e) { }
        }

        private void Receive()
        {
            try
            {
                _socket.BeginReceiveFrom(state.buffer, 0, bufSize, SocketFlags.None, ref epFrom, recv = (ar) =>
                {
                    try
                    {
                        State so = (State)ar.AsyncState;
                        int bytes = _socket.EndReceiveFrom(ar, ref epFrom);
                        _socket.BeginReceiveFrom(so.buffer, 0, bufSize, SocketFlags.None, ref epFrom, recv, so);
                        string data = Encoding.ASCII.GetString(so.buffer, 0, bytes);
                        ReceivedData?.Invoke(this, new Data { action = data[0], steps = long.Parse(data.Substring(1, data.Length - 1)) });
                        //Console.WriteLine("RECV: {0}: {1}, {2}", epFrom.ToString(), bytes, Encoding.ASCII.GetString(so.buffer, 0, bytes));
                    }
                    catch (ObjectDisposedException e) { }
                }, state);
            }
            catch (ObjectDisposedException e) { }
        }
    }
}
