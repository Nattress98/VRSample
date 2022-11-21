using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
namespace Networking
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Netcode/" + nameof(NetworkPCUser))]
    [DefaultExecutionOrder(100000)] // this is needed to catch the update time after the transform was updated by user scripts

    public class NetworkPCUser : NetworkBehaviour
    {
        public UnityEvent setupLocalUser = new UnityEvent();

        public const float PositionThresholdDefault = .001f;
        public const float RotAngleThresholdDefault = .01f;
        public delegate (Vector3 pos, Vector2 rot) OnClientRequestChangeDelegate(Vector3 pos, Vector2 rot);
        public OnClientRequestChangeDelegate OnClientRequestChange;

        internal struct NetworkTransformState : INetworkSerializable
        {
            private const int k_InLocalSpaceBit = 0;
            private const int k_PositionXBit = 1;
            private const int k_PositionYBit = 2;
            private const int k_PositionZBit = 3;
            private const int k_RotAngleXBit = 4;
            private const int k_RotAngleYBit = 5;
            private const int k_TeleportingBit = 6;

            // 11-15: <unused>
            private ushort m_Bitset;

            public bool InLocalSpace
            {
                get => (m_Bitset & (1 << k_InLocalSpaceBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_InLocalSpaceBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_InLocalSpaceBit)); }
                }
            }

            // Position
            public bool HasPositionX
            {
                get => (m_Bitset & (1 << k_PositionXBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_PositionXBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_PositionXBit)); }
                }
            }

            public bool HasPositionY
            {
                get => (m_Bitset & (1 << k_PositionYBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_PositionYBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_PositionYBit)); }
                }
            }

            public bool HasPositionZ
            {
                get => (m_Bitset & (1 << k_PositionZBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_PositionZBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_PositionZBit)); }
                }
            }

            // RotAngles
            public bool HasRotAngleX
            {
                get => (m_Bitset & (1 << k_RotAngleXBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_RotAngleXBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_RotAngleXBit)); }
                }
            }

            public bool HasRotAngleY
            {
                get => (m_Bitset & (1 << k_RotAngleYBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_RotAngleYBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_RotAngleYBit)); }
                }
            }

            public bool IsTeleportingNextFrame
            {
                get => (m_Bitset & (1 << k_TeleportingBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_TeleportingBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_TeleportingBit)); }
                }
            }

            public float PositionX, PositionY, PositionZ;
            public float RotAngleX, RotAngleY;
            public double SentTime;

            public Vector3 Position
            {
                get { return new Vector3(PositionX, PositionY, PositionZ); }
                set
                {
                    PositionX = value.x;
                    PositionY = value.y;
                    PositionZ = value.z;
                }
            }

            public Vector2 Rotation
            {
                get { return new Vector2(RotAngleX, RotAngleY); }
                set
                {
                    RotAngleX = value.x;
                    RotAngleY = value.y;
                }
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref SentTime);
                // InLocalSpace + HasXXX Bits
                serializer.SerializeValue(ref m_Bitset);
                // Position Values
                if (HasPositionX)
                {
                    serializer.SerializeValue(ref PositionX);
                }

                if (HasPositionY)
                {
                    serializer.SerializeValue(ref PositionY);
                }

                if (HasPositionZ)
                {
                    serializer.SerializeValue(ref PositionZ);
                }

                // RotAngle Values
                if (HasRotAngleX)
                {
                    serializer.SerializeValue(ref RotAngleX);
                }

                if (HasRotAngleY)
                {
                    serializer.SerializeValue(ref RotAngleY);
                }
            }
        }

        public float PositionThreshold = PositionThresholdDefault;
        public float RotAngleThreshold = RotAngleThresholdDefault;

        [Tooltip("Sets whether this transform should sync in local space or in world space")]
        public bool InLocalSpace = false;

        public bool Interpolate = true;

        public bool CanCommitToTransform;
        protected bool m_CachedIsServer;
        protected NetworkManager m_CachedNetworkManager;

        private readonly NetworkVariable<NetworkTransformState> m_ReplicatedNetworkState = new NetworkVariable<NetworkTransformState>(new NetworkTransformState());

        private NetworkTransformState m_LocalAuthoritativeNetworkState;

        private NetworkTransformState m_PrevNetworkState;

        private const int k_DebugDrawLineTime = 10;

        private bool m_HasSentLastValue = false; // used to send one last value, so clients can make the difference between lost replication data (clients extrapolate) and no more data to send.

        private BufferedLinearInterpolator<float> m_PositionXInterpolator; // = new BufferedLinearInterpolatorFloat();
        private BufferedLinearInterpolator<float> m_PositionYInterpolator; // = new BufferedLinearInterpolatorFloat();
        private BufferedLinearInterpolator<float> m_PositionZInterpolator; // = new BufferedLinearInterpolatorFloat();
        private BufferedLinearInterpolator<float> m_RotationXInterpolator; // = new BufferedLinearInterpolatorQuaternion(); // rotation is a single Quaternion since each euler axis will affect the quaternion's final value
        private BufferedLinearInterpolator<float> m_RotationYInterpolator; // = new BufferedLinearInterpolatorQuaternion(); // rotation is a single Quaternion since each euler axis will affect the quaternion's final value
        private readonly List<BufferedLinearInterpolator<float>> m_AllFloatInterpolators = new List<BufferedLinearInterpolator<float>>(6);

        private Transform m_Transform; // cache the transform component to reduce unnecessary bounce between managed and native
        public Transform camTransform; // cache the transform component to reduce unnecessary bounce between managed and native
        private int m_LastSentTick;
        private NetworkTransformState m_LastSentState;

        protected void TryCommitTransformToServer(Transform transformToCommit, double dirtyTime)
        {
            var isDirty = ApplyTransformToNetworkState(ref m_LocalAuthoritativeNetworkState, dirtyTime, transformToCommit);
            TryCommit(isDirty);
        }

        private void TryCommitValuesToServer(Vector3 position, Vector2 rotation, double dirtyTime)
        {
            var isDirty = ApplyTransformToNetworkStateWithInfo(ref m_LocalAuthoritativeNetworkState, dirtyTime, position, rotation);

            TryCommit(isDirty.isDirty);
        }

        private void TryCommit(bool isDirty)
        {
            void Send(NetworkTransformState stateToSend)
            {
                if (m_CachedIsServer)
                {
                    // server RPC takes a few frames to execute server side, we want this to execute immediately
                    CommitLocallyAndReplicate(stateToSend);
                }
                else
                {
                    CommitTransformServerRpc(stateToSend);
                }
            }

            // if dirty, send
            // if not dirty anymore, but hasn't sent last value for limiting extrapolation, still set isDirty
            // if not dirty and has already sent last value, don't do anything
            // extrapolation works by using last two values. if it doesn't receive anything anymore, it'll continue to extrapolate.
            // This is great in case there's message loss, not so great if we just don't have new values to send.
            // the following will send one last "copied" value so unclamped interpolation tries to extrapolate between two identical values, effectively
            // making it immobile.
            if (isDirty)
            {
                Send(m_LocalAuthoritativeNetworkState);
                m_HasSentLastValue = false;
                m_LastSentTick = m_CachedNetworkManager.LocalTime.Tick;
                m_LastSentState = m_LocalAuthoritativeNetworkState;
            }
            else if (!m_HasSentLastValue && m_CachedNetworkManager.LocalTime.Tick >= m_LastSentTick + 1) // check for state.IsDirty since update can happen more than once per tick. No need for client, RPCs will just queue up
            {
                m_LastSentState.SentTime = m_CachedNetworkManager.LocalTime.Time; // time 1+ tick later
                Send(m_LastSentState);
                m_HasSentLastValue = true;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void CommitTransformServerRpc(NetworkTransformState networkState, ServerRpcParams serverParams = default)
        {
            if (serverParams.Receive.SenderClientId == OwnerClientId) // RPC call when not authorized to write could happen during the RTT interval during which a server's ownership change hasn't reached the client yet
            {
                CommitLocallyAndReplicate(networkState);
            }
        }

        private void CommitLocallyAndReplicate(NetworkTransformState networkState)
        {
            m_ReplicatedNetworkState.Value = networkState;
            AddInterpolatedState(networkState);
        }

        private void ResetInterpolatedStateToCurrentAuthoritativeState()
        {
            var serverTime = NetworkManager.ServerTime.Time;
            m_PositionXInterpolator.ResetTo(m_LocalAuthoritativeNetworkState.PositionX, serverTime);
            m_PositionYInterpolator.ResetTo(m_LocalAuthoritativeNetworkState.PositionY, serverTime);
            m_PositionZInterpolator.ResetTo(m_LocalAuthoritativeNetworkState.PositionZ, serverTime);

            m_RotationXInterpolator.ResetTo(m_LocalAuthoritativeNetworkState.RotAngleX, serverTime);
            m_RotationYInterpolator.ResetTo(m_LocalAuthoritativeNetworkState.RotAngleY, serverTime);

        }
        internal bool ApplyTransformToNetworkState(ref NetworkTransformState networkState, double dirtyTime, Transform transformToUse)
        {
            return ApplyTransformToNetworkStateWithInfo(ref networkState, dirtyTime, transformToUse).isDirty;
        }

        private (bool isDirty, bool isPositionDirty, bool isRotationDirty) ApplyTransformToNetworkStateWithInfo(ref NetworkTransformState networkState, double dirtyTime, Transform transformToUse)
        {
            var position = InLocalSpace ? transformToUse.localPosition : transformToUse.position;
            var rotAngles = InLocalSpace ? new Vector2(camTransform.localEulerAngles.x, transformToUse.localEulerAngles.y) : new Vector2(camTransform.eulerAngles.x, transformToUse.eulerAngles.y);
            return ApplyTransformToNetworkStateWithInfo(ref networkState, dirtyTime, position, rotAngles);
        }

        private (bool isDirty, bool isPositionDirty, bool isRotationDirty) ApplyTransformToNetworkStateWithInfo(ref NetworkTransformState networkState, double dirtyTime, Vector3 position, Vector2 rotAngles)
        {
            var isDirty = false;
            var isPositionDirty = false;
            var isRotationDirty = false;

            // hasPositionZ set to false when it should be true?

            if (InLocalSpace != networkState.InLocalSpace)
            {
                networkState.InLocalSpace = InLocalSpace;
                isDirty = true;
            }

            // we assume that if x, y or z are dirty then we'll have to send all 3 anyway, so for efficiency
            //  we skip doing the (quite expensive) Math.Approximately() and check against PositionThreshold
            //  this still is overly costly and could use more improvements.
            //
            // (ditto for scale components)
            if (Mathf.Abs(networkState.PositionX - position.x) > PositionThreshold)
            {
                networkState.PositionX = position.x;
                networkState.HasPositionX = true;
                isPositionDirty = true;
            }

            if (Mathf.Abs(networkState.PositionY - position.y) > PositionThreshold)
            {
                networkState.PositionY = position.y;
                networkState.HasPositionY = true;
                isPositionDirty = true;
            }

            if (Mathf.Abs(networkState.PositionZ - position.z) > PositionThreshold)
            {
                networkState.PositionZ = position.z;
                networkState.HasPositionZ = true;
                isPositionDirty = true;
            }

            if (Mathf.Abs(networkState.RotAngleX - rotAngles.x) > RotAngleThreshold)
            {
                networkState.RotAngleX = UnclampRotations(networkState.RotAngleX, rotAngles.x);
                networkState.HasRotAngleX = true;
                isRotationDirty = true;
            }

            if (Mathf.Abs(networkState.RotAngleY - rotAngles.y) > RotAngleThreshold)
            {
                networkState.RotAngleY = UnclampRotations(networkState.RotAngleY, rotAngles.y);
                networkState.HasRotAngleY = true;
                isRotationDirty = true;
            }


            isDirty |= isPositionDirty || isRotationDirty;

            if (isDirty)
            {
                networkState.SentTime = dirtyTime;
            }

            return (isDirty, isPositionDirty, isRotationDirty);
        }

        private void ApplyInterpolatedNetworkStateToTransform(NetworkTransformState networkState, Transform transformToUpdate)
        {
            m_PrevNetworkState = networkState;

            var interpolatedPosition = InLocalSpace ? transformToUpdate.localPosition : transformToUpdate.position;

            // todo: we should store network state w/ quats vs. euler angles
            var interpolatedRotAngles = InLocalSpace ? transformToUpdate.localEulerAngles : transformToUpdate.eulerAngles;
            var interpolatedCamRotAngles = InLocalSpace ? camTransform.localEulerAngles : camTransform.eulerAngles;

            // InLocalSpace Read
            InLocalSpace = networkState.InLocalSpace;
            // Position Read

            interpolatedPosition.x = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.Position.x : m_PositionXInterpolator.GetInterpolatedValue();

            interpolatedPosition.y = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.Position.y : m_PositionYInterpolator.GetInterpolatedValue();

            interpolatedPosition.z = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.Position.z : m_PositionZInterpolator.GetInterpolatedValue();

            //                UnclampRotations(interpolatedCamRotAngles.x,
            interpolatedCamRotAngles.x = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.Rotation.x : m_RotationXInterpolator.GetInterpolatedValue();

            interpolatedRotAngles.y = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.Rotation.y : m_RotationYInterpolator.GetInterpolatedValue();

            if (InLocalSpace)
            {
                transformToUpdate.localPosition = interpolatedPosition;
            }
            else
            {
                transformToUpdate.position = interpolatedPosition;
            }

            m_PrevNetworkState.Position = interpolatedPosition;


            if (InLocalSpace)
            {
                transformToUpdate.localRotation = Quaternion.Euler(new Vector3(0, interpolatedRotAngles.y, 0));

                camTransform.localRotation = Quaternion.Euler(new Vector3(interpolatedCamRotAngles.x, 0, 0));
            }
            else
            {
                transformToUpdate.rotation = Quaternion.Euler(interpolatedRotAngles);
                camTransform.rotation = Quaternion.Euler(interpolatedCamRotAngles.x, 0, 0);

            }

            m_PrevNetworkState.Rotation = new Vector2(interpolatedCamRotAngles.x, interpolatedRotAngles.y);

        }
        private float UnclampRotations(float current, float next)
        {
            if (current > 270.0f && next < 90.0f)
                return next + 360.0f;
            else if (current < 90.0f && next > 270.0f)
                return next - 360.0f;
            return next;
        }
        private void AddInterpolatedState(NetworkTransformState newState)
        {
            var sentTime = newState.SentTime;

            if (newState.HasPositionX)
            {
                m_PositionXInterpolator.AddMeasurement(newState.PositionX, sentTime);
            }

            if (newState.HasPositionY)
            {
                m_PositionYInterpolator.AddMeasurement(newState.PositionY, sentTime);
            }

            if (newState.HasPositionZ)
            {
                m_PositionZInterpolator.AddMeasurement(newState.PositionZ, sentTime);
            }
            if (newState.HasRotAngleX)
            {
                m_RotationXInterpolator.AddMeasurement(newState.RotAngleX, sentTime);
            }
            if (newState.HasRotAngleY)
            {
                m_RotationYInterpolator.AddMeasurement(newState.RotAngleY, sentTime);
            }
        }
        private void OnNetworkStateChanged(NetworkTransformState oldState, NetworkTransformState newState)
        {
            if (!NetworkObject.IsSpawned)
            {
                // todo MTT-849 should never happen but yet it does! maybe revisit/dig after NetVar updates and snapshot system lands?
                return;
            }

            if (CanCommitToTransform)
            {
                // we're the authority, we ignore incoming changes
                return;
            }

            Debug.DrawLine(newState.Position, newState.Position + Vector3.up + Vector3.left, Color.green, 10, false);

            AddInterpolatedState(newState);

            if (m_CachedNetworkManager.LogLevel == LogLevel.Developer)
            {
                var pos = new Vector3(newState.PositionX, newState.PositionY, newState.PositionZ);
                Debug.DrawLine(pos, pos + Vector3.up + Vector3.left * UnityEngine.Random.Range(0.5f, 2f), Color.green, k_DebugDrawLineTime, false);
            }
        }
        private void Awake()
        {
            // we only want to create our interpolators during Awake so that, when pooled, we do not create tons
            //  of gc thrash each time objects wink out and are re-used
            m_PositionXInterpolator = new BufferedLinearInterpolatorFloat();
            m_PositionYInterpolator = new BufferedLinearInterpolatorFloat();
            m_PositionZInterpolator = new BufferedLinearInterpolatorFloat();
            m_RotationXInterpolator = new BufferedLinearInterpolatorFloat();
            m_RotationYInterpolator = new BufferedLinearInterpolatorFloat();

            if (m_AllFloatInterpolators.Count == 0)
            {
                m_AllFloatInterpolators.Add(m_PositionXInterpolator);
                m_AllFloatInterpolators.Add(m_PositionYInterpolator);
                m_AllFloatInterpolators.Add(m_PositionZInterpolator);
                m_AllFloatInterpolators.Add(m_RotationXInterpolator);
                m_AllFloatInterpolators.Add(m_RotationYInterpolator);

            }
        }
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Debug.Log("Test");
            if (GetComponent<NetworkObject>().IsOwner)
                setupLocalUser.Invoke();

            m_Transform = transform;
            m_ReplicatedNetworkState.OnValueChanged += OnNetworkStateChanged;

            CanCommitToTransform = IsOwner;
            m_CachedIsServer = IsServer;
            m_CachedNetworkManager = NetworkManager;

            if (CanCommitToTransform)
            {
                TryCommitTransformToServer(m_Transform, m_CachedNetworkManager.LocalTime.Time);
            }
            m_LocalAuthoritativeNetworkState = m_ReplicatedNetworkState.Value;

            // crucial we do this to reset the interpolators so that recycled objects when using a pool will
            //  not have leftover interpolator state from the previous object
            Initialize();

        }
        public override void OnNetworkDespawn()
        {
            m_ReplicatedNetworkState.OnValueChanged -= OnNetworkStateChanged;
        }

        public override void OnGainedOwnership()
        {
            Initialize();
        }

        public override void OnLostOwnership()
        {
            Initialize();
        }
        private void Initialize()
        {
            ResetInterpolatedStateToCurrentAuthoritativeState(); // useful for late joining

            if (CanCommitToTransform)
            {
                m_ReplicatedNetworkState.SetDirty(true);
            }
            else
            {
                ApplyInterpolatedNetworkStateToTransform(m_ReplicatedNetworkState.Value, m_Transform);
            }
        }

        #region state set

        /// <summary>
        /// Directly sets a state on the authoritative transform.
        /// This will override any changes made previously to the transform
        /// This isn't resistant to network jitter. Server side changes due to this method won't be interpolated.
        /// The parameters are broken up into pos / rot / scale on purpose so that the caller can perturb
        ///  just the desired one(s)
        /// </summary>
        /// <param name="posIn"></param> new position to move to.  Can be null
        /// <param name="rotIn"></param> new rotation to rotate to.  Can be null
        /// <param name="scaleIn">new scale to scale to. Can be null</param>
        /// <param name="shouldGhostsInterpolate">Should other clients interpolate this change or not. True by default</param>
        /// new scale to scale to.  Can be null
        /// <exception cref="Exception"></exception>
        public void SetState(Vector3? posIn = null, Vector2? rotIn = null, Vector3? scaleIn = null, bool shouldGhostsInterpolate = true)
        {
            if (!IsOwner)
            {
                throw new Exception("Trying to set a state on a not owned transform");
            }

            if (m_CachedNetworkManager && !(m_CachedNetworkManager.IsConnectedClient || m_CachedNetworkManager.IsListening))
            {
                return;
            }

            Vector3 pos = posIn == null ? transform.position : (Vector3)posIn;
            Vector2 rot = rotIn == null ? new Vector2(camTransform.eulerAngles.x, transform.eulerAngles.y) : (Vector2)rotIn;

            if (!CanCommitToTransform)
            {
                if (!m_CachedIsServer)
                {
                    SetStateServerRpc(pos, rot, shouldGhostsInterpolate);
                }
            }
            else
            {
                m_Transform.position = pos;
                m_Transform.rotation = Quaternion.Euler(new Vector3(0, rot.y, 0));
                camTransform.rotation = Quaternion.Euler(new Vector3(rot.x, 0, 0));
                m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame = shouldGhostsInterpolate;
            }
        }

        [ServerRpc]
        private void SetStateServerRpc(Vector3 pos, Vector2 rot, bool shouldTeleport)
        {
            // server has received this RPC request to move change transform.  Give the server a chance to modify or
            //  even reject the move
            if (OnClientRequestChange != null)
            {
                (pos, rot) = OnClientRequestChange(pos, rot);
            }
            m_Transform.position = pos;
            m_Transform.rotation = Quaternion.Euler(new Vector3(0, rot.y, 0));
            camTransform.rotation = Quaternion.Euler(new Vector3(rot.x, 0, 0));
            m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame = shouldTeleport;
        }
        #endregion

        protected virtual void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (CanCommitToTransform)
            {
                if (m_CachedIsServer)
                {
                    TryCommitTransformToServer(m_Transform, m_CachedNetworkManager.LocalTime.Time);
                }

                m_PrevNetworkState = m_LocalAuthoritativeNetworkState;
            }

            // apply interpolated value
            if (m_CachedNetworkManager.IsConnectedClient || m_CachedNetworkManager.IsListening)
            {
                // eventually, we could hoist this calculation so that it happens once for all objects, not once per object
                var cachedDeltaTime = Time.deltaTime;
                var serverTime = NetworkManager.ServerTime;
                var cachedServerTime = serverTime.Time;
                var cachedRenderTime = serverTime.TimeTicksAgo(1).Time;

                foreach (var interpolator in m_AllFloatInterpolators)
                {
                    interpolator.Update(cachedDeltaTime, cachedRenderTime, cachedServerTime);
                }

                //Below is a floatInterpolator
                //m_RotationInterpolator.Update(cachedDeltaTime, cachedRenderTime, cachedServerTime);

                if (!CanCommitToTransform)
                {
#if NGO_TRANSFORM_DEBUG
                    if (m_CachedNetworkManager.LogLevel == LogLevel.Developer)
                    {
                        // TODO: This should be a component gizmo - not some debug draw based on log level
                        var interpolatedPosition = new Vector3(m_PositionXInterpolator.GetInterpolatedValue(), m_PositionYInterpolator.GetInterpolatedValue(), m_PositionZInterpolator.GetInterpolatedValue());
                        Debug.DrawLine(interpolatedPosition, interpolatedPosition + Vector3.up, Color.magenta, k_DebugDrawLineTime, false);

                        // try to update previously consumed NetworkState
                        // if we have any changes, that means made some updates locally
                        // we apply the latest ReplNetworkState again to revert our changes
                        var oldStateDirtyInfo = ApplyTransformToNetworkStateWithInfo(ref m_PrevNetworkState, 0, m_Transform);

                        // there are several bugs in this code, as we the message is dumped out under odd circumstances
                        //  For Matt, it would trigger when an object's rotation was perturbed by colliding with another
                        //  object vs. explicitly rotating it
                        if (oldStateDirtyInfo.isPositionDirty || oldStateDirtyInfo.isScaleDirty || (oldStateDirtyInfo.isRotationDirty && SyncRotAngleX && SyncRotAngleY && SyncRotAngleZ))
                        {
                            // ignoring rotation dirty since quaternions will mess with euler angles, making this impossible to determine if the change to a single axis comes
                            // from an unauthorized transform change or euler to quaternion conversion artifacts.
                            var dirtyField = oldStateDirtyInfo.isPositionDirty ? "position" : oldStateDirtyInfo.isRotationDirty ? "rotation" : "scale";
                            Debug.LogWarning($"A local change to {dirtyField} without authority detected, reverting back to latest interpolated network state!", this);
                        }
                    }
#endif

                    // Apply updated interpolated value
                    ApplyInterpolatedNetworkStateToTransform(m_ReplicatedNetworkState.Value, m_Transform);
                }
            }

            m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame = false;

            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsListening))
            {
                if (CanCommitToTransform)
                {
                    TryCommitTransformToServer(transform, NetworkManager.LocalTime.Time);
                }
            }

        }
        public void Teleport(Vector3 newPosition, Vector2 newRotation)
        {
            if (!CanCommitToTransform)
            {
                throw new Exception("Teleport not allowed");
            }

            var stateToSend = m_LocalAuthoritativeNetworkState;
            stateToSend.IsTeleportingNextFrame = true;
            stateToSend.Position = newPosition;
            stateToSend.Rotation = newRotation;
            ApplyInterpolatedNetworkStateToTransform(stateToSend, transform);
            // set teleport flag in state to signal to ghosts not to interpolate
            m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame = true;
            // check server side
            TryCommitValuesToServer(newPosition, newRotation, m_CachedNetworkManager.LocalTime.Time);
            m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame = false;
        }

    }
}