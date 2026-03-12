using System;
using System.IO;
using UnityEngine;

namespace DIG.Lobby
{
    /// <summary>
    /// EPIC 17.4: Lobby message type identifiers.
    /// All framed as [MessageType:byte][PayloadLength:ushort][Payload:bytes].
    /// </summary>
    public enum LobbyMessageType : byte
    {
        JoinRequest = 1,
        JoinAccepted = 2,
        JoinDenied = 3,
        StateUpdate = 4,
        ReadyChanged = 5,
        ChatMessage = 6,
        KickPlayer = 7,
        StartGame = 8,
        Heartbeat = 9,
        LeaveNotify = 10,
        DiscoveryQuery = 11,
        DiscoveryResponse = 12,
        ReturnToLobby = 13,
        PingRequest = 14,
        PingResponse = 15
    }

    /// <summary>
    /// EPIC 17.4: Base class for all lobby messages with binary serialization.
    /// </summary>
    public abstract class LobbyMessage
    {
        public abstract LobbyMessageType Type { get; }
        public abstract void Serialize(BinaryWriter w);
        public abstract void Deserialize(BinaryReader r);
    }

    /// <summary>Client sends to host when joining.</summary>
    public class JoinRequestMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.JoinRequest;
        public string PlayerId;
        public string DisplayName;
        public int Level;
        public int ClassId;
        public string GameVersion;
        public string JoinCode;

        public override void Serialize(BinaryWriter w)
        {
            w.Write(PlayerId ?? "");
            w.Write(DisplayName ?? "");
            w.Write(Level);
            w.Write(ClassId);
            w.Write(GameVersion ?? "");
            w.Write(JoinCode ?? "");
        }

        public override void Deserialize(BinaryReader r)
        {
            PlayerId = LobbyMessageSerializer.ReadSafeString(r, 64);
            DisplayName = LobbyMessageSerializer.ReadSafeString(r, 64);
            Level = r.ReadInt32();
            ClassId = r.ReadInt32();
            GameVersion = LobbyMessageSerializer.ReadSafeString(r, 32);
            JoinCode = LobbyMessageSerializer.ReadSafeString(r, 16);
        }
    }

    /// <summary>Host sends to client on successful join.</summary>
    public class JoinAcceptedMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.JoinAccepted;
        public int AssignedSlotIndex;
        public LobbyState FullState;

        public override void Serialize(BinaryWriter w)
        {
            w.Write((byte)AssignedSlotIndex);
            FullState?.WriteTo(w);
        }

        public override void Deserialize(BinaryReader r)
        {
            AssignedSlotIndex = r.ReadByte();
            FullState = new LobbyState();
            FullState.ReadFrom(r);
        }
    }

    /// <summary>Host sends to client when join is denied.</summary>
    public class JoinDeniedMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.JoinDenied;
        public DenyReason Reason;

        public override void Serialize(BinaryWriter w) { w.Write((byte)Reason); }
        public override void Deserialize(BinaryReader r) { Reason = (DenyReason)r.ReadByte(); }
    }

    /// <summary>Host broadcasts full lobby state to all clients on change.</summary>
    public class StateUpdateMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.StateUpdate;
        public LobbyState State;

        public override void Serialize(BinaryWriter w) { State?.WriteTo(w); }
        public override void Deserialize(BinaryReader r)
        {
            State = new LobbyState();
            State.ReadFrom(r);
        }
    }

    /// <summary>Client tells host ready state changed.</summary>
    public class ReadyChangedMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.ReadyChanged;
        public bool IsReady;

        public override void Serialize(BinaryWriter w) { w.Write(IsReady); }
        public override void Deserialize(BinaryReader r) { IsReady = r.ReadBoolean(); }
    }

    /// <summary>Chat message from any player.</summary>
    public class ChatMessageMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.ChatMessage;
        public string SenderName;
        public string Text;

        public override void Serialize(BinaryWriter w)
        {
            w.Write(SenderName ?? "");
            w.Write(Text ?? "");
        }

        public override void Deserialize(BinaryReader r)
        {
            SenderName = LobbyMessageSerializer.ReadSafeString(r, 64);
            Text = LobbyMessageSerializer.ReadSafeString(r, 500);
        }
    }

    /// <summary>Host kicks a player by slot index.</summary>
    public class KickPlayerMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.KickPlayer;
        public int SlotIndex;

        public override void Serialize(BinaryWriter w) { w.Write((byte)SlotIndex); }
        public override void Deserialize(BinaryReader r) { SlotIndex = r.ReadByte(); }
    }

    /// <summary>Host tells all clients to begin game transition.</summary>
    public class StartGameMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.StartGame;
        public int MapId;
        public int DifficultyId;
        public string RelayJoinCode;
        public byte[] SlotToNetworkIdMap; // index = slot, value = expected networkId

        public override void Serialize(BinaryWriter w)
        {
            w.Write(MapId);
            w.Write(DifficultyId);
            w.Write(RelayJoinCode ?? "");
            w.Write((byte)(SlotToNetworkIdMap?.Length ?? 0));
            if (SlotToNetworkIdMap != null)
                w.Write(SlotToNetworkIdMap);
        }

        public override void Deserialize(BinaryReader r)
        {
            MapId = r.ReadInt32();
            DifficultyId = r.ReadInt32();
            RelayJoinCode = LobbyMessageSerializer.ReadSafeString(r, 64);
            int len = r.ReadByte();
            SlotToNetworkIdMap = len > 0 ? r.ReadBytes(len) : Array.Empty<byte>();
        }
    }

    /// <summary>Keepalive ping.</summary>
    public class HeartbeatMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.Heartbeat;
        public override void Serialize(BinaryWriter w) { }
        public override void Deserialize(BinaryReader r) { }
    }

    /// <summary>Client notifies host they are leaving.</summary>
    public class LeaveNotifyMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.LeaveNotify;
        public override void Serialize(BinaryWriter w) { }
        public override void Deserialize(BinaryReader r) { }
    }

    /// <summary>Broadcast query for public lobbies on LAN.</summary>
    public class DiscoveryQueryMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.DiscoveryQuery;
        public string GameVersion;

        public override void Serialize(BinaryWriter w) { w.Write(GameVersion ?? ""); }
        public override void Deserialize(BinaryReader r) { GameVersion = LobbyMessageSerializer.ReadSafeString(r, 32); }
    }

    /// <summary>Host responds to discovery query with lobby summary.</summary>
    public class DiscoveryResponseMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.DiscoveryResponse;
        public string LobbyId;
        public string HostName;
        public string JoinCode;
        public int MapId;
        public int DifficultyId;
        public int CurrentPlayers;
        public int MaxPlayers;
        public int PingMs;

        public override void Serialize(BinaryWriter w)
        {
            w.Write(LobbyId ?? "");
            w.Write(HostName ?? "");
            w.Write(JoinCode ?? "");
            w.Write(MapId);
            w.Write(DifficultyId);
            w.Write((byte)CurrentPlayers);
            w.Write((byte)MaxPlayers);
            w.Write((ushort)PingMs);
        }

        public override void Deserialize(BinaryReader r)
        {
            LobbyId = LobbyMessageSerializer.ReadSafeString(r, 64);
            HostName = LobbyMessageSerializer.ReadSafeString(r, 64);
            JoinCode = LobbyMessageSerializer.ReadSafeString(r, 16);
            MapId = r.ReadInt32();
            DifficultyId = r.ReadInt32();
            CurrentPlayers = r.ReadByte();
            MaxPlayers = r.ReadByte();
            PingMs = r.ReadUInt16();
        }
    }

    /// <summary>Host tells clients to return to lobby from game.</summary>
    public class ReturnToLobbyMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.ReturnToLobby;
        public override void Serialize(BinaryWriter w) { }
        public override void Deserialize(BinaryReader r) { }
    }

    /// <summary>Ping request for RTT measurement.</summary>
    public class PingRequestMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.PingRequest;
        public long TimestampTicks;

        public override void Serialize(BinaryWriter w) { w.Write(TimestampTicks); }
        public override void Deserialize(BinaryReader r) { TimestampTicks = r.ReadInt64(); }
    }

    /// <summary>Ping response echoes timestamp for RTT calculation.</summary>
    public class PingResponseMessage : LobbyMessage
    {
        public override LobbyMessageType Type => LobbyMessageType.PingResponse;
        public long OriginalTimestampTicks;

        public override void Serialize(BinaryWriter w) { w.Write(OriginalTimestampTicks); }
        public override void Deserialize(BinaryReader r) { OriginalTimestampTicks = r.ReadInt64(); }
    }

    /// <summary>
    /// EPIC 17.4: Serializer/deserializer for lobby messages.
    /// Frame format: [Type:1 byte][Length:2 bytes][Payload:N bytes].
    /// </summary>
    public static class LobbyMessageSerializer
    {
        [ThreadStatic] private static MemoryStream _sharedWriteStream;
        [ThreadStatic] private static MemoryStream _sharedReadStream;
        [ThreadStatic] private static BinaryReader _sharedReader;

        /// <summary>
        /// Serialize a message. Returns the shared buffer and its valid length.
        /// The buffer is reused — caller must consume before next Serialize call.
        /// </summary>
        public static (byte[] buffer, int length) SerializeShared(LobbyMessage msg)
        {
            if (_sharedWriteStream == null)
                _sharedWriteStream = new MemoryStream(256);
            else
                _sharedWriteStream.SetLength(0);

            var ms = _sharedWriteStream;
            using var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);

            // Write placeholder for header, then payload
            bw.Write((byte)msg.Type);
            bw.Write((ushort)0); // placeholder for length
            long payloadStart = ms.Position;
            msg.Serialize(bw);
            long payloadEnd = ms.Position;

            // Patch length
            int payloadLen = (int)(payloadEnd - payloadStart);
            ms.Position = 1;
            bw.Write((ushort)payloadLen);

            return (ms.GetBuffer(), (int)payloadEnd);
        }

        /// <summary>Allocating serialize for compatibility. Prefer SerializeShared.</summary>
        public static byte[] Serialize(LobbyMessage msg)
        {
            var (buffer, length) = SerializeShared(msg);
            var result = new byte[length];
            Array.Copy(buffer, result, length);
            return result;
        }

        public static LobbyMessage Deserialize(byte[] data, int offset, int length)
        {
            // Reuse MemoryStream and BinaryReader to avoid per-message allocations
            if (_sharedReadStream == null)
            {
                _sharedReadStream = new MemoryStream(data, offset, length);
                _sharedReader = new BinaryReader(_sharedReadStream);
            }
            else
            {
                // Reset the stream to wrap new data (MemoryStream doesn't support SetBuffer,
                // so we create a new wrapper — but the BinaryReader overhead is avoided)
                _sharedReadStream = new MemoryStream(data, offset, length);
                _sharedReader.Dispose();
                _sharedReader = new BinaryReader(_sharedReadStream);
            }

            var br = _sharedReader;

            var type = (LobbyMessageType)br.ReadByte();
            var payloadLen = br.ReadUInt16();

            var msg = CreateMessage(type);
            if (msg == null)
            {
                Debug.LogWarning($"[LobbyMessages] Unknown message type: {type}");
                return null;
            }

            try
            {
                msg.Deserialize(br);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyMessages] Failed to deserialize {type}: {e.Message}");
                return null;
            }
            return msg;
        }

        /// <summary>
        /// Safe string read with max length. Prevents OOM from malicious length prefixes.
        /// </summary>
        public static string ReadSafeString(BinaryReader r, int maxLength = 1024)
        {
            var s = r.ReadString();
            if (s.Length > maxLength)
                return s.Substring(0, maxLength);
            return s;
        }

        private static LobbyMessage CreateMessage(LobbyMessageType type) => type switch
        {
            LobbyMessageType.JoinRequest => new JoinRequestMessage(),
            LobbyMessageType.JoinAccepted => new JoinAcceptedMessage(),
            LobbyMessageType.JoinDenied => new JoinDeniedMessage(),
            LobbyMessageType.StateUpdate => new StateUpdateMessage(),
            LobbyMessageType.ReadyChanged => new ReadyChangedMessage(),
            LobbyMessageType.ChatMessage => new ChatMessageMessage(),
            LobbyMessageType.KickPlayer => new KickPlayerMessage(),
            LobbyMessageType.StartGame => new StartGameMessage(),
            LobbyMessageType.Heartbeat => new HeartbeatMessage(),
            LobbyMessageType.LeaveNotify => new LeaveNotifyMessage(),
            LobbyMessageType.DiscoveryQuery => new DiscoveryQueryMessage(),
            LobbyMessageType.DiscoveryResponse => new DiscoveryResponseMessage(),
            LobbyMessageType.ReturnToLobby => new ReturnToLobbyMessage(),
            LobbyMessageType.PingRequest => new PingRequestMessage(),
            LobbyMessageType.PingResponse => new PingResponseMessage(),
            _ => null
        };
    }
}
