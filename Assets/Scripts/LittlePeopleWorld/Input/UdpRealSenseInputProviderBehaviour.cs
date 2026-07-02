using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Input
{
    [DisallowMultipleComponent]
    public sealed class UdpRealSenseInputProviderBehaviour : MonoBehaviour, IInteractionInputProvider
    {
        readonly List<InteractionObject> interactionObjects = new();
        readonly Dictionary<int, TrackedObject> trackedObjects = new();
        readonly object messageLock = new();

        [SerializeField] int listenPort = 5005;
        [SerializeField] float staleInputSeconds = 1f;
        [SerializeField] float stationaryVelocityThreshold = 0.015f;
        [SerializeField] bool debugEnabled = true;
        [SerializeField] bool toggleDebugWithD = true;

        UdpClient udpClient;
        Thread receiveThread;
        bool isRunning;
        string pendingMessage;
        string pendingThreadError;
        string lastError;
        float lastPacketRealtime = -1f;
        long lastFrame = -1;

        public IReadOnlyList<InteractionObject> InteractionObjects => interactionObjects;
        public bool DebugEnabled => debugEnabled;
        public int ListenPort => listenPort;
        public long LastFrame => lastFrame;
        public string LastError => lastError;
        public float PacketAgeSeconds => lastPacketRealtime < 0f
            ? float.PositiveInfinity
            : Time.realtimeSinceStartup - lastPacketRealtime;

        void OnEnable()
        {
            StartReceiver();
        }

        void OnDisable()
        {
            StopReceiver();
        }

        void OnDestroy()
        {
            StopReceiver();
        }

        void Update()
        {
            if (toggleDebugWithD && UnityEngine.Input.GetKeyDown(KeyCode.D))
            {
                debugEnabled = !debugEnabled;
            }

            ProcessPendingThreadError();
            ProcessPendingMessage();
            ClearStaleObjects();
        }

        void StartReceiver()
        {
            if (isRunning)
            {
                return;
            }

            try
            {
                listenPort = Mathf.Clamp(listenPort, 1, 65535);
                udpClient = new UdpClient(listenPort);
                isRunning = true;
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "LittlePeopleWorld UDP Input"
                };
                receiveThread.Start();
                lastError = string.Empty;
            }
            catch (Exception exception)
            {
                isRunning = false;
                lastError = $"UDP listen failed: {exception.Message}";
                Debug.LogWarning(lastError);
            }
        }

        void StopReceiver()
        {
            isRunning = false;

            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }

            if (receiveThread != null)
            {
                receiveThread.Join(100);
                receiveThread = null;
            }
        }

        void ReceiveLoop()
        {
            while (isRunning)
            {
                try
                {
                    var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    var bytes = udpClient.Receive(ref remoteEndPoint);
                    var message = Encoding.UTF8.GetString(bytes);
                    lock (messageLock)
                    {
                        pendingMessage = message;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException exception)
                {
                    if (isRunning)
                    {
                        StoreThreadError(exception.Message);
                    }
                }
                catch (Exception exception)
                {
                    if (isRunning)
                    {
                        StoreThreadError(exception.Message);
                    }
                }
            }
        }

        void StoreThreadError(string message)
        {
            lock (messageLock)
            {
                pendingThreadError = message;
            }
        }

        void ProcessPendingThreadError()
        {
            string threadError = null;
            lock (messageLock)
            {
                if (!string.IsNullOrEmpty(pendingThreadError))
                {
                    threadError = pendingThreadError;
                    pendingThreadError = null;
                }
            }

            if (!string.IsNullOrEmpty(threadError))
            {
                lastError = $"UDP receive failed: {threadError}";
                Debug.LogWarning(lastError);
            }
        }

        void ProcessPendingMessage()
        {
            string message = null;
            lock (messageLock)
            {
                if (pendingMessage != null)
                {
                    message = pendingMessage;
                    pendingMessage = null;
                }
            }

            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            try
            {
                var frame = JsonUtility.FromJson<SensorFrameDto>(message);
                if (frame == null)
                {
                    throw new FormatException("UDP JSON root was empty.");
                }

                ApplyFrame(frame);
                lastError = string.Empty;
            }
            catch (Exception exception)
            {
                lastError = $"UDP JSON parse failed: {exception.Message}";
                Debug.LogWarning(lastError);
            }
        }

        void ApplyFrame(SensorFrameDto frame)
        {
            var receiveTime = Time.realtimeSinceStartup;
            var objects = frame.objects ?? Array.Empty<InteractionObjectDto>();
            var seenIds = new HashSet<int>();

            interactionObjects.Clear();
            lastFrame = frame.frame;
            lastPacketRealtime = receiveTime;

            foreach (var source in objects)
            {
                if (source == null || IsRemoved(source.state))
                {
                    continue;
                }

                var id = source.id;
                if (seenIds.Contains(id))
                {
                    continue;
                }

                seenIds.Add(id);
                var kind = ParseKind(string.IsNullOrWhiteSpace(source.kind) ? source.type : source.kind);
                var state = ParseState(source.state);
                var position = new Vector2(Mathf.Clamp01(source.x), Mathf.Clamp01(source.y));
                var size = ParseSize(kind, source.w, source.h);
                var previousPosition = position;
                var previousTime = receiveTime;
                var hasPrevious = trackedObjects.TryGetValue(id, out var previous);
                if (hasPrevious)
                {
                    previousPosition = previous.Position;
                    previousTime = previous.LastSeenRealtime;
                }

                var interactionObject = new InteractionObject(
                    id,
                    kind,
                    previousPosition,
                    size,
                    source.angle,
                    source.height,
                    state);

                var deltaTime = Mathf.Max(0.0001f, receiveTime - previousTime);
                var moved = hasPrevious && Vector2.Distance(previousPosition, position) > 0.0001f;
                if (moved)
                {
                    interactionObject.MoveTo(position, previousTime + deltaTime);
                    if (state == InteractionObjectState.Placed &&
                        interactionObject.Velocity.magnitude <= stationaryVelocityThreshold)
                    {
                        interactionObject.Release();
                    }
                }

                interactionObjects.Add(interactionObject);
                trackedObjects[id] = new TrackedObject(position, receiveTime);
            }

            RemoveDeadTracks(seenIds, receiveTime);
        }

        void RemoveDeadTracks(HashSet<int> seenIds, float receiveTime)
        {
            var deadIds = new List<int>();
            foreach (var pair in trackedObjects)
            {
                if (!seenIds.Contains(pair.Key) &&
                    receiveTime - pair.Value.LastSeenRealtime > Mathf.Max(0.1f, staleInputSeconds))
                {
                    deadIds.Add(pair.Key);
                }
            }

            foreach (var id in deadIds)
            {
                trackedObjects.Remove(id);
            }
        }

        void ClearStaleObjects()
        {
            if (lastPacketRealtime < 0f || PacketAgeSeconds <= Mathf.Max(0.1f, staleInputSeconds))
            {
                return;
            }

            interactionObjects.Clear();
            trackedObjects.Clear();
        }

        static InteractionObjectKind ParseKind(string rawKind)
        {
            var value = (rawKind ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_");
            return value switch
            {
                "hand" => InteractionObjectKind.Hand,
                "round" => InteractionObjectKind.RoundProp,
                "circle" => InteractionObjectKind.RoundProp,
                "round_prop" => InteractionObjectKind.RoundProp,
                "bar" => InteractionObjectKind.BarProp,
                "stick" => InteractionObjectKind.BarProp,
                "bar_prop" => InteractionObjectKind.BarProp,
                "block" => InteractionObjectKind.BlockProp,
                "block_prop" => InteractionObjectKind.BlockProp,
                _ => InteractionObjectKind.BarProp
            };
        }

        static InteractionObjectState ParseState(string rawState)
        {
            var value = (rawState ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_");
            return value switch
            {
                "placing" => InteractionObjectState.Placing,
                "dragging" => InteractionObjectState.Dragging,
                _ => InteractionObjectState.Placed
            };
        }

        static bool IsRemoved(string rawState)
        {
            var value = (rawState ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_");
            return value == "removed";
        }

        static Vector2 ParseSize(InteractionObjectKind kind, float width, float height)
        {
            var fallback = kind == InteractionObjectKind.BarProp
                ? new Vector2(0.12f, 0.026f)
                : new Vector2(0.12f, 0.12f);
            var safeWidth = width > 0.0001f ? width : fallback.x;
            var safeHeight = height > 0.0001f ? height : fallback.y;
            return new Vector2(safeWidth, safeHeight);
        }

        readonly struct TrackedObject
        {
            public readonly Vector2 Position;
            public readonly float LastSeenRealtime;

            public TrackedObject(Vector2 position, float lastSeenRealtime)
            {
                Position = position;
                LastSeenRealtime = lastSeenRealtime;
            }
        }

        [Serializable]
        sealed class SensorFrameDto
        {
            public long frame;
            public float timestamp;
            public InteractionObjectDto[] objects;
        }

        [Serializable]
        sealed class InteractionObjectDto
        {
            public int id;
            public string kind;
            public string type;
            public float x;
            public float y;
            public float w;
            public float h;
            public float angle;
            public float height;
            public string state;
        }
    }
}
