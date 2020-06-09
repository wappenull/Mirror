using System.IO;
using System.Net.Sockets;

namespace Telepathy
{
    public static class NetworkStreamExtensions
    {
        // .Read returns '0' if remote closed the connection but throws an
        // IOException if we voluntarily closed our own connection.
        //
        // let's add a ReadSafely method that returns '0' in both cases so we don't
        // have to worry about exceptions, since a disconnect is a disconnect...
        public static int ReadSafely( this NetworkStream stream, byte[] buffer, int offset, int size )
        {
            try
            {
                // Wappen: Async version for diagnostic
                // There is strange lock in stream.Read while client still sending legit data seen through wireshark ,100% reproducable weird and scary stuff.
                // Use BeginRead version instead
                float timeWaited = 0f;
                var result = stream.BeginRead( buffer, offset, size, null, null );
                while( result.AsyncWaitHandle.WaitOne( 100 ) == false )
                {
                    // Timeout occur, count time without no traffic
                    timeWaited += 0.1f;
                }

                int totalRead = stream.EndRead( result );
                return totalRead;

                // Original call
                //return stream.Read(buffer, offset, size);
            }
            catch( IOException )
            {
                return 0;
            }
        }

        // helper function to read EXACTLY 'n' bytes
        // -> default .Read reads up to 'n' bytes. this function reads exactly 'n'
        //    bytes
        // -> this is blocking until 'n' bytes were received
        // -> immediately returns false in case of disconnects
        public static bool ReadExactly( this NetworkStream stream, byte[] buffer, int amount )
        {
            // there might not be enough bytes in the TCP buffer for .Read to read
            // the whole amount at once, so we need to keep trying until we have all
            // the bytes (blocking)
            //
            // note: this just is a faster version of reading one after another:
            //     for (int i = 0; i < amount; ++i)
            //         if (stream.Read(buffer, i, 1) == 0)
            //             return false;
            //     return true;
            int bytesRead = 0;
            while( bytesRead < amount )
            {
                // read up to 'remaining' bytes with the 'safe' read extension
                int remaining = amount - bytesRead;
                int result = stream.ReadSafely(buffer, bytesRead, remaining);

                // .Read returns 0 if disconnected
                if( result == 0 )
                    return false;

                // otherwise add to bytes read
                bytesRead += result;
            }
            return true;
        }
    }

}
