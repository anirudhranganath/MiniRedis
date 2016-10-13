using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MR
{
    class Listener
    {
        static MiniRedis mrInstance = new MiniRedis();
        static int port = 5555;
        static void Main(string[] args)
        {
            TcpListener server = null;
            TcpClient client = null;
            try
            {
                int counter = 0; // count no. of connections
                IPAddress localhost = IPAddress.Parse("127.0.0.1");

                server = new TcpListener(localhost, port);

                // Start listening.
                server.Start();
                Console.WriteLine(">> Listener Started");

                // Enter the listening loop. 
                while (true)
                {
                    counter += 1;

                    // Accept connection
                    client = server.AcceptTcpClient();
                    Console.WriteLine(String.Format(">> Connection {0} connected", counter));

                    //Delegate to a handler for multiple simultaneous connections
                    ClientHandler handler = new ClientHandler();
                    handler.startClient(client, counter.ToString(), ref mrInstance);                    
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }


            Console.WriteLine("\nHit enter to continue...");
            Console.Read();
        }

        // A handler for each client that connected to server
        private class ClientHandler
        {
            TcpClient client;
            string clientNo;
            MiniRedis mrInstance;

            public void startClient(TcpClient client, string cNo, ref MiniRedis mr)
            {
                this.client = client;
                this.clientNo = cNo;
                mrInstance = mr;
                Thread ctThread = new Thread(interact);
                ctThread.Start();
            }

            private void interact()
            {
                int requestCount = 0;
                byte[] bytesFrom = new byte[10025];
                string dataFromClient = string.Empty;
                Byte[] sendBytes = null;
                //string serverResponse = null;
                requestCount = 0;

                while ((true))
                {
                    try
                    {
                        requestCount = requestCount + 1;
                        NetworkStream networkStream = client.GetStream();
                        int i;
                        dataFromClient = string.Empty;
                        while ((i = networkStream.Read(bytesFrom, 0, bytesFrom.Length)) != 0)
                        {
                            // Translate data bytes to a ASCII string.
                            String recChar = System.Text.Encoding.ASCII.GetString(bytesFrom, 0, i);
                            dataFromClient += recChar;
                            if (recChar.Equals("\r\n") || recChar.Equals("\r") || recChar.Equals("\n"))
                            {
                                break;
                            }

                            // Send back a response - to write their query on client.
                            //sendBytes = System.Text.Encoding.ASCII.GetBytes(recChar);
                            //networkStream.Write(sendBytes, 0, sendBytes.Length);
                            //Console.WriteLine("Sent: {0}", recChar);
                        }
                        Console.WriteLine("Client {0} request {1}: {2}", clientNo, requestCount, dataFromClient);
                        String response = mrInstance.Handle(dataFromClient.Trim());
                        sendBytes = System.Text.Encoding.ASCII.GetBytes(response+"\r\n");
                        networkStream.Write(sendBytes, 0, sendBytes.Length);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(" >> " + ex.ToString());
                    }
                }
            }
        }
    }
}
