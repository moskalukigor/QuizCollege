using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Sockets;
using System.Collections;
using System.Net;
using System.Threading;

namespace QuizCollegeServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        struct ClientInfo
        {
            public Socket socket;
            public string strName;
        }

        public delegate void UpdateRichEditCallback(string text);
        public delegate void UpdateClientListCallback();
        public AsyncCallback pfnWorkerCallBack;

        ArrayList clientList = ArrayList.Synchronized(new System.Collections.ArrayList());
        Socket serverSocket;
        private int clientCount = 0;
        private bool IsStarted = false; //false - off/true - on

        byte[] byteData = new byte[1024];

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void OnClientConnect(IAsyncResult ar)
        {
            try 
            {
                Socket clientSocket = serverSocket.EndAccept(ar);

                Interlocked.Increment(ref clientCount);

                clientList.Add(clientSocket);

                string msg = "Welcome client" + clientCount + "\n";
                SendMsgToClient(msg, clientCount);

                UpdateClientListControl();

                WaitForData(clientSocket, clientCount);

                serverSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);

            }
            catch(ObjectDisposedException)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\n OnClientConnection: Socket has been closed\n");
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "SGSserverTCP",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        public class SocketPacket
        {
            public SocketPacket(Socket socket, int clientNumber)
            {
                currentSocket = socket;
                m_clientNumber = clientNumber;
            }
            public System.Net.Sockets.Socket currentSocket;
            public int m_clientNumber;

            public byte[] dataBuffer = new byte[1024];
        }
        public void WaitForData(Socket soc, int clientNumber)
        {
            try
            {
                if(pfnWorkerCallBack == null)
                {
                    pfnWorkerCallBack = new AsyncCallback(OnDataReceived);
                }
                SocketPacket theSocPkt = new SocketPacket(soc, clientNumber);

                soc.BeginReceive(theSocPkt.dataBuffer, 0,
                    theSocPkt.dataBuffer.Length,
                    SocketFlags.None,
                    pfnWorkerCallBack,
                    theSocPkt);
            }
            catch(SocketException se)
            {
                MessageBox.Show(se.Message);
            }
        }
        public void OnDataReceived(IAsyncResult asyn)
        {
            SocketPacket socketData = (SocketPacket)asyn.AsyncState;
            try
            {
                int iRx = socketData.currentSocket.EndReceive(asyn);
                char[] chars = new char[iRx + 1];
                Decoder d = Encoding.UTF8.GetDecoder();
                int charLen = d.GetChars(socketData.dataBuffer,
                    0, iRx, chars, 0);

                String szData = new String(chars);
                string msg = "" + socketData.currentSocket + ":";
                AppendToRichEditControl(msg + szData);

                string replyMsg = "Server Reply: " + szData.ToUpper();
                byte[] byData = Encoding.ASCII.GetBytes(replyMsg);

                Socket workerSocket = (Socket)socketData.currentSocket;
                workerSocket.Send(byData);

                WaitForData(socketData.currentSocket, socketData.m_clientNumber);
            }
            catch(ObjectDisposedException )
            {
                System.Diagnostics.Debugger.Log(0, "1", "\nOnDataReceived: Socket has been closed\n");
            }
            catch(SocketException se)
            {
                if(se.ErrorCode == 10054) // Error code for Connection reset by  peer
                {
                    string msg = "Client " + socketData.m_clientNumber + " Disconnected" + "\n";
                    AppendToRichEditControl(msg);

                    clientList[socketData.m_clientNumber - 1] = null;
                    UpdateClientListControl();
                }
                else
                {
                    MessageBox.Show(se.Message);
                }
            }
        }

        private void AppendToRichEditControl(string msg)
        {
            if (this.Dispatcher.CheckAccess())
            {
                object[] pList = { msg };
                rtxbReceiveMsg.Dispatcher.BeginInvoke(new UpdateRichEditCallback(OnUpdateRichEdit), pList);
            }
            else
            {
                OnUpdateRichEdit(msg);
            }
        }
        private void OnUpdateRichEdit(string msg)
        {
            rtxbReceiveMsg.AppendText(msg);
        }

        private void UpdateClientListControl()
        {
            if(this.Dispatcher.CheckAccess())
            {
                lbxClientList.Dispatcher.BeginInvoke(new UpdateClientListCallback(UpdateClientList), null);
            }
            else
            {
                UpdateClientList();
            }
        }
        void UpdateClientList()
        {
            lbxClientList.Items.Clear();
            for(int i = 0; i < clientList.Count; i++)
            {
                string clientKey = Convert.ToString(i + 1);
                Socket workerSocket = (Socket)clientList[i];
                if (workerSocket != null)
                {
                    if(workerSocket.Connected)
                    {
                        lbxClientList.Items.Add(clientKey);
                    }
                }
            }
        }


        void SendMsgToClient(string msg, int clientNumber)
        {
            byte[] byData = System.Text.Encoding.ASCII.GetBytes(msg);

            Socket workerSocket = (Socket)clientList[clientNumber - 1];
            workerSocket.Send(byData);
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if(!IsStarted)
            { 
                try
                {
                    serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
                        ProtocolType.Tcp);

                    int port = Convert.ToInt32(txbPort.Text);

                    IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, port);

                    serverSocket.Bind(ipEndPoint);
                    serverSocket.Listen(4);

                    serverSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);

                    IsStarted = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "SGSserverTCP",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else { }
        }


    }
}
