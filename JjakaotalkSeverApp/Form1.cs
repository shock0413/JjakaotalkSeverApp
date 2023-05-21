using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;

namespace JjakaotalkSeverApp
{
    public partial class Form1 : Form
    {
        private Socket mSocket;
        Thread t;
        public List<Receiver> mReceiverList = new List<Receiver>();
        int port;
        bool isClosed = true;   // 서버가 닫혔는지

        public Form1()
        {
            InitializeComponent();
            // this.MaximizeBox = false;   // 최대화 불가능
        }

        private void ServerThreadBody()
        {
            try
            {
                IPEndPoint iep = new IPEndPoint(IPAddress.Any, port);
                mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                
                mSocket.Bind(iep);
                mSocket.NoDelay = true;
                mSocket.Blocking = true;
                mSocket.Listen(10);
                
                mSocket.BeginAccept(AcceptCallback, mSocket);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private void AcceptCallback(IAsyncResult ar)    // BeginAccept 함수의 콜백 함수
        {
            try
            {
                if (isClosed)
                    return;

                // Socket client = (Socket)ar.AsyncState;
                Socket client = mSocket.EndAccept(ar);

                mSocket.BeginAccept(AcceptCallback, mSocket);

                this.Invoke(new MethodInvoker(  // 크로스스레드 오류 해결 Invoke
                        delegate ()
                        {
                            textBox2.Text += client.RemoteEndPoint + "\r\n";
                            textBox2.Text += mReceiverList.Count + "\r\n";
                        }
                ));
                
                Receiver receiver = new Receiver(this, client);
                mReceiverList.Add(receiver);
                receiver.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.Equals(""))
            {
                MessageBox.Show("포트번호를 입력해주세요.");
                return;
            }

            isClosed = false;
            port = int.Parse(textBox1.Text);
            textBox2.Text = "";
            panel2.Visible = true;
            panel1.Visible = false;

            t = new Thread(new ThreadStart(ServerThreadBody));
            t.Start();
            textBox1.Text = "";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            panel1.Visible = true;
            panel2.Visible = false;

            t.Interrupt();

            foreach (Receiver r in mReceiverList)
                r.Close();

            mReceiverList.Clear();

            isClosed = true;
            
            try
            {
                mSocket.Close();
                mSocket.Dispose();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.HResult);
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void Form1_Closing(object sender, FormClosingEventArgs e)
        {
            if (!panel2.Visible)
                return;

            if (mReceiverList.Count > 0)
            {
                foreach (Receiver r in mReceiverList)
                {
                    r.Close();
                }
            }

            isClosed = true;

            if (mSocket != null)
            {
                try
                {
                    mSocket.Close();
                    mSocket.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.HResult);
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')    // 엔터 키 누를 시 비프음 제거 '\r' == 13
            {
                e.Handled = true;
            }

            if (e.KeyChar == (char)Keys.Enter)
            {
                button1_Click(null, null);
            }
            else if (!Char.IsDigit(e.KeyChar) & !(e.KeyChar == (char)Keys.Back))
            {
                MessageBox.Show("숫자만 입력해주세요.");
                e.Handled = true;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}