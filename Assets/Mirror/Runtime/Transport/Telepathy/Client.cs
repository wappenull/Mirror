using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public class Client : Common
    {
        public TcpClient client;
        Thread receiveThread;
        Thread sendThread;

        // TcpClient.Connected doesn't check if socket != null, which
        // results in NullReferenceExceptions if connection was closed.
        // -> let's check it manually instead
        public bool Connected => client != null &&
                                 client.Client != null &&
                                 client.Client.Connected;

        // TcpClient has no 'connecting' state to check. We need to keep track
        // of it manually.
        // -> checking 'thread.IsAlive && !Connected' is not enough because the
        //    thread is alive and connected is false for a short moment after
        //    disconnecting, so this would cause race conditions.
        // -> we use a threadsafe bool wrapper so that ThreadFunction can remain
        //    static (it needs a common lock)
        // => Connecting is true from first Connect() call in here, through the
        //    thread start, until TcpClient.Connect() returns. Simple and clear.
        // => bools are atomic according to
        //    https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/variables
        //    made volatile so the compiler does not reorder access to it
        volatile bool _Connecting;
        public bool Connecting => _Connecting;

        // send queue
        // => SafeQueue is twice as fast as ConcurrentQueue, see SafeQueue.cs!
        SafeQueue<byte[]> sendQueue = new SafeQueue<byte[]>();

        // ManualResetEvent to wake up the send thread. better than Thread.Sleep
        // -> call Set() if everything was sent
        // -> call Reset() if there is something to send again
        // -> call WaitOne() to block until Reset was called
        ManualResetEvent sendPending = new ManualResetEvent(false);

        // Wappen:
        bool abortConnect;

        // the thread function
        void ReceiveThreadFunction(string ip, int port)
        {
            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try
            {
                // Wappen: Resolve DNS and prepare best socket family   
                // This is also blocking operation so it should be in thread
                AddressFamily family = _ResolveBestIpInterface( ip );

                if( family == AddressFamily.Unknown )
                {
                    throw new OperationCanceledException( "No IPAddress suitable for connect for target ip " + ip );
                }

                client = new TcpClient( family );

                // Wappen Modifying: Use Nonblocking to get rid of Thread Join delay.
                // incoming IP could be DNS name or IPv6, use IPAddress to check that
                IPAddress ipa;
                IAsyncResult result;
                if( IPAddress.TryParse( ip, out ipa ) ) // Use IPAddress version to connect
                    result = client.BeginConnect( ipa, port, null, null );
                else
                    result = client.BeginConnect( ip, port, null, null );

                while( result.AsyncWaitHandle.WaitOne( 100 ) == false )
                {
                    if( abortConnect )
                    {
                        client.Close( );
                        break;
                    }
                }

                if( abortConnect )
                    throw new OperationCanceledException( "abortConnect" );

                // If task is completed and reached here, supposed it is finished!
                client.EndConnect(result);
                _Connecting = false;

                // set socket options after the socket was created in Connect()
                // (not after the constructor because we clear the socket there)
                client.NoDelay = NoDelay;
                client.SendTimeout = SendTimeout;

                // start send thread only after connected
                sendThread = new Thread(() => { SendLoop(0, client, sendQueue, sendPending); });
                sendThread.IsBackground = true;
                sendThread.Start();

                // run the receive loop
                ReceiveLoop(0, client, receiveQueue, MaxMessageSize);
            }
            catch( OperationCanceledException )
            {
                // Connect operation is cancelled
                receiveQueue.Enqueue(new Message(0, EventType.Disconnected, null));
            }
            catch (SocketException exception)
            {
                // this happens if (for example) the ip address is correct
                // but there is no server running on that ip/port
                Logger.Log("Client Recv: failed to connect to ip=" + ip + " port=" + port + " reason=" + exception);

                // Prepare extra reason, this is sending from worker thread to main thread
                byte[] extraDisconnectMessage = null;
                using( System.IO.MemoryStream ms = new System.IO.MemoryStream( ) )
                {
                    using( System.IO.BinaryWriter bw = new System.IO.BinaryWriter( ms ) )
                    {
                        bw.Write( (int)exception.SocketErrorCode );
                        bw.Write( exception.Message );
                    }
                    extraDisconnectMessage = ms.ToArray( );
                }

                // add 'Disconnected' event to message queue so that the caller
                // knows that the Connect failed. otherwise they will never know
                receiveQueue.Enqueue( new Message( 0, EventType.Disconnected, extraDisconnectMessage ) );
            }
            catch (ThreadInterruptedException)
            {
                // expected if Disconnect() aborts it
            }
            catch (ThreadAbortException)
            {
                // expected if Disconnect() aborts it
            }
            catch (Exception exception)
            {
                // something went wrong. probably important.
                Logger.LogError("Client Recv Exception: " + exception);
            }

            // sendthread might be waiting on ManualResetEvent,
            // so let's make sure to end it if the connection
            // closed.
            // otherwise the send thread would only end if it's
            // actually sending data while the connection is
            // closed.
            // => AbortAndJoin is the safest way and avoids race conditions!
            sendThread?.AbortAndJoin();

            // Connect might have failed. thread might have been closed.
            // let's reset connecting state no matter what.
            _Connecting = false;

            // if we got here then we are done. ReceiveLoop cleans up already,
            // but we may never get there if connect fails. so let's clean up
            // here too.
            client?.Close();
        }

        public void Connect(string ip, int port)
        {
            // not if already started
            if (Connecting || Connected) return;

            // We are connecting from now until Connect succeeds or fails
            _Connecting = true;

            // create a TcpClient with perfect IPv4, IPv6 and hostname resolving
            // support.
            //
            // * TcpClient(hostname, port): works but would connect (and block)
            //   already
            // * TcpClient(AddressFamily.InterNetworkV6): takes Ipv4 and IPv6
            //   addresses but only connects to IPv6 servers (e.g. Telepathy).
            //   does NOT connect to IPv4 servers (e.g. Mirror Booster), even
            //   with DualMode enabled.
            // * TcpClient(): creates IPv4 socket internally, which would force
            //   Connect() to only use IPv4 sockets.
            //
            // => the trick is to clear the internal IPv4 socket so that Connect
            //    resolves the hostname and creates either an IPv4 or an IPv6
            //    socket as needed (see TcpClient source)
            //client = new TcpClient(); // creates IPv4 socket
            //client.Client = null; // clear internal IPv4 socket until Connect()

            client = null; // Will construct in thread
            abortConnect = false;

            // clear old messages in queue, just to be sure that the caller
            // doesn't receive data from last time and gets out of sync.
            // -> calling this in Disconnect isn't smart because the caller may
            //    still want to process all the latest messages afterwards
            receiveQueue = new ConcurrentQueue<Message>();
            sendQueue.Clear();

            // client.Connect(ip, port) is blocking. let's call it in the thread
            // and return immediately.
            // -> this way the application doesn't hang for 30s if connect takes
            //    too long, which is especially good in games
            // -> this way we don't async client.BeginConnect, which seems to
            //    fail sometimes if we connect too many clients too fast
            receiveThread = new Thread(() => { ReceiveThreadFunction(ip, port); });
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        public void Disconnect()
        {
            // only if started
            if (Connecting || Connected)
            {
                // close client
                if( client != null )
                    client.Close();

                // Wappen: Mark cancel connection if we are connecting
                abortConnect = true;

                // kill the receive thread
                // => AbortAndJoin is the safest way and avoids race conditions!
                //    this way we can guarantee that when Disconnect() returns,
                //    we are 100% ready for the next Connect!
                receiveThread?.AbortAndJoin();

                // we interrupted the receive Thread, so we can't guarantee that
                // connecting was reset. let's do it manually.
                _Connecting = false;

                // clear send queues. no need to hold on to them.
                // (unlike receiveQueue, which is still needed to process the
                //  latest Disconnected message, etc.)
                sendQueue.Clear();

                // let go of this one completely. the thread ended, no one uses
                // it anymore and this way Connected is false again immediately.
                client = null;
            }
        }

        public bool Send(byte[] data)
        {
            if (Connected)
            {
                // respect max message size to avoid allocation attacks.
                if (data.Length <= MaxMessageSize)
                {
                    // add to send queue and return immediately.
                    // calling Send here would be blocking (sometimes for long times
                    // if other side lags or wire was disconnected)
                    sendQueue.Enqueue(data);
                    sendPending.Set(); // interrupt SendThread WaitOne()
                    return true;
                }
                Logger.LogError("Client.Send: message too big: " + data.Length + ". Limit: " + MaxMessageSize);
                return false;
            }
            Logger.LogWarning("Client.Send: not connected!");
            return false;
        }

        private static AddressFamily _ResolveBestIpInterface( string ip )
        {
#if false
            // IPv6: We need to process each of the addresses return from
            //       DNS when trying to connect.
            // Code snippet from https://referencesource.microsoft.com/#system/net/System/Net/Sockets/TCPClient.cs,eeb78642518c5e2d
            IPAddress[] addresses = Dns.GetHostAddresses( ip );
            foreach( IPAddress address in addresses )
            {
                if( address.AddressFamily == AddressFamily.InterNetwork && Socket.OSSupportsIPv4 )
                    return address;
                else if( address.AddressFamily == AddressFamily.InterNetworkV6 && Socket.OSSupportsIPv6 )
                    return address;
            }
#endif
            // Simple determine by jointer
            if( Socket.OSSupportsIPv6 )
                return AddressFamily.InterNetworkV6;
            else
                return AddressFamily.InterNetwork;
        }
    }
}
