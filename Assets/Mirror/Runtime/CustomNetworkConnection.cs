using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Wappen extension: For external implementation to use NetworkConnection.
    /// Copied from NetworkConnectionToClient.
    /// </summary>
    public class CustomNetworkConnectionToClient : CustomNetworkConnection
    {
        public CustomNetworkConnectionToClient( int networkConnectionId, Transport t ) : base( networkConnectionId, t ) { }

        public override string address => m_UsingTransport.ServerGetClientAddress( connectionId );

        readonly List<int> singleConnectionId = new List<int> { -1 };

        internal override bool IsClientAlive( ) => true;

        internal override bool Send( ArraySegment<byte> segment, int channelId = Channels.DefaultReliable )
        {
            // validate packet size first.
            if( _ValidatePacketSize( segment, channelId ) )
            {
                singleConnectionId[0] = connectionId;
                return m_UsingTransport.ServerSend( singleConnectionId, channelId, segment );
            }
            return false;
        }

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
        public override void Disconnect( )
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            m_UsingTransport.ServerDisconnect( connectionId );
            RemoveObservers( );
        }
    }

    /// <summary>
    /// Copied from NetworkConnectionToServer.
    /// </summary>
    public class CustomNetworkConnectionToServer : CustomNetworkConnection
    {
        public CustomNetworkConnectionToServer( Transport t ) : base( t ) { }

        public override string address => "";

        internal override bool Send( ArraySegment<byte> segment, int channelId = Channels.DefaultReliable )
        {
            // validate packet size first.
            if( _ValidatePacketSize( segment, channelId ) )
            {
                return m_UsingTransport.ClientSend( channelId, segment );
            }
            return false;
        }

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
        public override void Disconnect( )
        {
            // set not ready and handle clientscene disconnect in any case
            // (might be client or host mode here)
            isReady = false;
            m_UsingTransport.ClientDisconnect( );
        }
    }

    /// <summary>
    /// Base class for connection that use specified transport.
    /// </summary>
    public abstract class CustomNetworkConnection : NetworkConnection
    {
        protected readonly Transport m_UsingTransport;

        internal CustomNetworkConnection( Transport t )
        {
            m_UsingTransport = t;
        }

        internal CustomNetworkConnection( int connectionId, Transport t ) : base( connectionId )
        {
            m_UsingTransport = t;
        }

        protected bool _ValidatePacketSize( ArraySegment<byte> segment, int channelId )
        {
            if( segment.Count > m_UsingTransport.GetMaxPacketSize( channelId ) )
            {
                Debug.LogError( "NetworkConnection.ValidatePacketSize: cannot send packet larger than " + m_UsingTransport.GetMaxPacketSize( channelId ) + " bytes" );
                return false;
            }

            if( segment.Count == 0 )
            {
                // zero length packets getting into the packet queues are bad.
                Debug.LogError( "NetworkConnection.ValidatePacketSize: cannot send zero bytes" );
                return false;
            }

            // good size
            return true;
        }
    }
}
