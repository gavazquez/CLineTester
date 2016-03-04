using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ConsoleApplication
{
    public class Program
    {
        private static readonly CryptoBlock ReceiveBlock = new CryptoBlock();
        private static readonly CryptoBlock SendBlock = new CryptoBlock();
        private static readonly Socket Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        
        
        static void Main(string[] args)
        {
            var server = "my.ccamserver.com";
            var port = 99999;
            var username = "user";
            var password = "passw";
            
            Socket.Connect(server, port);
            try
            {
                var helloBytes = new byte[16];
                Socket.Receive(helloBytes);
                Console.WriteLine("Received hello: " + Encoding.UTF8.GetString(helloBytes));

                if (CheckConnectionChecksum(helloBytes, 16))
                {
                    CryptoBlock.cc_crypt_xor(helloBytes);

                    SHA1 sha = new SHA1Managed();
                    var sha1Hash = sha.ComputeHash(helloBytes);

                    ReceiveBlock.cc_crypt_init(sha1Hash, 20);
                    ReceiveBlock.cc_decrypt(helloBytes, 16);

                    SendBlock.cc_crypt_init(helloBytes, 16);
                    SendBlock.cc_decrypt(sha1Hash, 20);

                    SendMsg(20, sha1Hash); //Handshake

                    byte[] userName = new byte[20];
                    Array.Copy(GetBytes(username), userName, GetBytes(username).Length);
                    SendMsg(20, userName);

                    byte[] pwd = new byte[63];
                    Array.Copy(GetBytes(password), userName, GetBytes(password).Length);
                    SendBlock.cc_encrypt(pwd, pwd.Length);

                    byte[] cCcam = new byte[6];
                    Array.Copy(GetBytes("CCcam"), cCcam, GetBytes("CCcam").Length);
                    SendMsg(6, cCcam);

                    byte[] receiveBytes = new byte[20];
                    Socket.Receive(receiveBytes, 20, SocketFlags.None);
                    ReceiveBlock.cc_decrypt(receiveBytes, 20);

                    Console.WriteLine(Encoding.Default.GetString(receiveBytes) == "CCcam"
                        ? "SUCCESS!"
                        : "NO ACK received!");
                    //I don't understand why the ACK is not received...
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("{0} Error code: {1}.", e.Message, e.ErrorCode);
            }
            Socket.Close();
        }

        private static bool CheckConnectionChecksum(byte[] buf, int len)
        {
            if (len == 16)
            {
                byte sum1 = (byte)(buf[0] + buf[4] +buf[8]);
                byte sum2 = (byte)(buf[1] + buf[5] + buf[9]);
                byte sum3 = (byte)(buf[2] + buf[6] + buf[10]);
                byte sum4 = (byte)(buf[3] + buf[7] + buf[11]);

                var valid = (sum1 == buf[12]) && (sum2 == buf[13]) && (sum3 == buf[14]) && (sum4 == buf[15]);
                return valid;
            }
            return false;
        }

        static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return Encoding.ASCII.GetBytes(str);
        }

        static int SendMsg(int len, byte[] buf)
        {
            byte[] netbuf = new byte[len];
            Array.Copy(buf, 0, netbuf, 0, len);
            SendBlock.cc_encrypt(netbuf, len);

            try
            {
                Socket.Send(netbuf);
                return len;
            }
            catch (IOException e)
            {
                Console.WriteLine("Connection closed while sending");
                Socket.Close();
            }
            return -1;
        }
    }
}
