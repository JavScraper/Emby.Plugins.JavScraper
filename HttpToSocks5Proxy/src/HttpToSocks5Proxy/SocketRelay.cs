using System.Net.Sockets;
using System.Threading.Tasks;

namespace MihaZupan
{
    internal class SocketRelay
    {
        private SocketAsyncEventArgs RecSAEA, SendSAEA;
        private Socket Source, Target;
        private byte[] Buffer;

        public bool Receiving;
        private int Received;
        private int SendingOffset;

        public SocketRelay Other;
        private bool Disposed = false;
        private bool ShouldDispose = false;

        private SocketRelay(Socket source, Socket target)
        {
            Source = source;
            Target = target;
            Buffer = new byte[81920];
            RecSAEA = new SocketAsyncEventArgs()
            {
                UserToken = this
            };
            SendSAEA = new SocketAsyncEventArgs()
            {
                UserToken = this
            };
            RecSAEA.SetBuffer(Buffer, 0, Buffer.Length);
            SendSAEA.SetBuffer(Buffer, 0, Buffer.Length);
            RecSAEA.Completed += OnAsyncOperationCompleted;
            SendSAEA.Completed += OnAsyncOperationCompleted;
            Receiving = true;
        }

        private void OnCleanup()
        {
            if (Disposed)
                return;

            Disposed = ShouldDispose = true;

            Other.ShouldDispose = true;
            Other = null;

            Source.TryDispose();
            Target.TryDispose();
            RecSAEA.TryDispose();
            SendSAEA.TryDispose();

            Source = Target = null;
            RecSAEA = SendSAEA = null;
            Buffer = null;
        }

        private void Process()
        {
            try
            {
                while (true)
                {
                    if (ShouldDispose)
                    {
                        OnCleanup();
                        return;
                    }

                    if (Receiving)
                    {
                        Receiving = false;
                        SendingOffset = -1;

                        if (Source.ReceiveAsync(RecSAEA))
                            return;
                    }
                    else
                    {
                        if (SendingOffset == -1)
                        {
                            Received = RecSAEA.BytesTransferred;
                            SendingOffset = 0;

                            if (Received == 0)
                            {
                                ShouldDispose = true;
                                continue;
                            }
                        }
                        else
                        {
                            SendingOffset += SendSAEA.BytesTransferred;
                        }

                        if (SendingOffset != Received)
                        {
                            SendSAEA.SetBuffer(Buffer, SendingOffset, Received - SendingOffset);

                            if (Target.SendAsync(SendSAEA))
                                return;
                        }
                        else Receiving = true;
                    }
                }
            }
            catch
            {
                OnCleanup();
            }
        }

        private static void OnAsyncOperationCompleted(object _, SocketAsyncEventArgs saea)
        {
            var relay = saea.UserToken as SocketRelay;
            relay.Process();
        }

        public static void RelayBiDirectionally(Socket s1, Socket s2)
        {
            var relayOne = new SocketRelay(s1, s2);
            var relayTwo = new SocketRelay(s2, s1);

            relayOne.Other = relayTwo;
            relayTwo.Other = relayOne;

            Task.Run(relayOne.Process);
            Task.Run(relayTwo.Process);
        }
    }
}
