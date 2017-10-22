using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace XjunkFix
{

    class Program
    {
        private static Listener listener;


        static void Main(string[] args)
        {
            //TcpClient tcp = new TcpClient("127.0.0.1", 25566);
            //return;
            listener = new Listener(1, 12345);
            Input();
            Console.WriteLine("Всё останавливаем");
            listener.Dispose();
        }

        static void Input()
        {
            string input = string.Empty;
            do
            {
                input = Console.ReadLine();
            } while (input.ToLower() != "stop");
        }
    }

    class Listener : IDisposable
    {
        int port;
        Socket[] sockets = new Socket[2];
        List<Handler> handlers;



        public Listener(byte proto, int port)
        {
            this.port = port;
            
            if (proto == 3)
            {
                BuildSocket(1, 1);
                BuildSocket(3, 2);
            }
            else
            {
                BuildSocket(proto, 1);
            }


        }

        void BuildSocket(byte sub, int num)
        {
            try
            {
                ProtocolType protocolType = GetProtocol(sub);
                sockets[num] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, protocolType);
                sockets[num].Bind(new IPEndPoint(IPAddress.Any, port));
                sockets[num].Listen(10);
                Console.WriteLine("bind(): " + port);
                Thread accepting = new Thread(delegate ()
                {
                    while (true)
                    {
                        Socket accepted = sockets[num].Accept();
                        Handler handler = new Handler(protocolType, accepted);
                        Thread processing = new Thread(handler.Processing);
                        processing.Start();
                    }
                });
                Console.Write("handle(): " + "многопоточный");
                accepting.Start();
                Console.WriteLine("Всё запустил " + sockets[num].ProtocolType.ToString().Replace("System.Net.Sockets.ProtocolType", "").ToUpper());
            }
            catch (SocketException ex) { Console.WriteLine(ex.NativeErrorCode); Console.WriteLine(ex.ToString()); }
        }

        public void Dispose()
        {
            Console.WriteLine("Остановка...");
            foreach (var h in handlers)
            {
                h.remote.Dispose();
            }
            foreach (var s in sockets)
            {
                if (s != null)
                    s.Dispose();
            }
            Console.WriteLine("Остановлено");
        }

        public static ProtocolType GetProtocol(byte ssub)
        {
            return ssub == 1 ? ProtocolType.Tcp : ProtocolType.Udp;
        }
    }

    class Handler
    {
        public Socket remote;
        public string addr;
        ProtocolType type;

        public Handler(ProtocolType type, Socket remote)
        {
            this.type = type;
            this.remote = remote;
            addr = remote.RemoteEndPoint.ToString().Split(':')[0];
        }

        public void Processing()
        {
            Proxy proxy = new Proxy(remote, addr, "46.105.45.20", 25565);
            proxy.Start();

            Thread remoteB = null;

            remoteB = new Thread(() =>
            {
                byte[] directBuffer = new byte[65535];
                try
                {
                    while (true)
                    {
                        
                        int recv = remote.Receive(directBuffer);
                        //Console.Write("remote -> local recv {0}", recv);
                        if (recv == 0)
                        {
                            break;
                        }
                        //Console.WriteLine(" ok.");
                        byte[] data = new byte[recv];
                        Array.Copy(directBuffer, data, recv);
                        if (BitConverter.ToString(data) == "0F-00-2F-09-31-32-37-2E-30-2E-30-2E-31-30-39-01-01-00")
                        {
                            Console.WriteLine("Игрок взял мотд " + addr);
                        }
                        if (BitConverter.ToString(data) == "0F-00-2F-09-31-32-37-2E-30-2E-30-2E-31-30-39-02-08-00-06-44-6F-6A-64-69-6B")
                        {
                            Console.WriteLine("Игрок вошёл в игру " + addr);
                        }
                        //Console.WriteLine("remote: " + BitConverter.ToString(data) + "\r\n");
                        //File.AppendAllText("buffer.txt", "remote: " + BitConverter.ToString(data) + "\r\n");
                        proxy.Send(data);

                    }
                }
                catch (SocketException ex)
                {
                    Console.WriteLine("remote: " + ex.SocketErrorCode);

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                finally
                {
                    proxy.Stop();
                    remoteB.Abort();
                }
            });

            remoteB.Start();
        }
    }

    class Proxy
    {

        Socket remote;
        Socket local;
        Thread thread;
        bool run = true;
        string addr;

        public Proxy(Socket remote, string addr, string host, int port)
        {
            this.remote = remote;
            this.addr = addr;
        }

        public void Start()
        {
            local = new Socket(SocketType.Stream, ProtocolType.Tcp);
            local.Connect("46.105.45.20", 25565);
            thread = new Thread(() =>
            {
                byte[] directBuffer = new byte[65535];
                try
                {
                    while (run)
                    {
                        int recv = local.Receive(directBuffer);
                        //Console.Write("remote -> local recv {0}", recv);
                        //Console.WriteLine(" ok.");
                        byte[] data = new byte[recv];
                        Array.Copy(directBuffer, data, recv);
                        //File.AppendAllText("buffer.txt", "local: " + BitConverter.ToString(data) + "\r\n");

                        remote.Send(data);

                    }
                }
                catch (SocketException ex)
                {
                    Console.WriteLine("local: " + ex.SocketErrorCode);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
            thread.Start();
        }
        
        public void Send(byte[] data)
        {

            if (data.Length == 0)
            {
                Console.WriteLine("Пакет " + BitConverter.ToString(data) + " от " + addr);
                remote.Close();
            }
            try
            {
                local.Send(data);
            }
            catch { }
        }

        public void Stop()
        {
            Console.WriteLine("Соединение закрыто " + addr);
            run = false;
            while (thread.IsAlive)
                Thread.Sleep(1);
            local.Dispose();
        }

    }
}
