using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Packing;
using PurrNet.Pooling;
using PurrNet.Prediction.Profiler;
using PurrNet.Transports;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet.Prediction
{
    [Serializable]
    public struct InputQueueSettings
    {
        public int minInputs;
        public int maxInputs;
    }

    [DefaultExecutionOrder(1000)]
    [AddComponentMenu("PurrDiction/Prediction Manager")]
    public class PredictionManager : NetworkIdentity
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Initialize() => _instances.Clear();

        static readonly Dictionary<int, PredictionManager> _instances = new ();

        [SerializeField] private PredictionPhysicsProvider _physicsProvider;
        [SerializeField] private UpdateViewMode _updateViewMode = UpdateViewMode.Update;
        [SerializeField, PurrLock] private BuiltInSystems _builtInSystems =
            BuiltInSystems.Physics3D |
            BuiltInSystems.Physics2D |
            BuiltInSystems.Time |
            BuiltInSystems.Hierarchy |
            BuiltInSystems.Players;
        [SerializeField] private PredictedPrefabs _predictedPrefabs;
        [SerializeField] private InputQueueSettings _inputQueueSettings = new()
        {
            minInputs = 1,
            maxInputs = 4
        };

        readonly List<PredictedIdentity> _queue = new ();
        readonly List<PredictedIdentity> _systems = new ();

        public static bool TryGetInstance(int sceneHandle, out PredictionManager world)
        {
            return _instances.TryGetValue(sceneHandle, out world);
        }

        GameObjectPoolCollection _pools;

        private void Awake()
        {
            _instances[gameObject.scene.handle] = this;

            if ((_physicsProvider & PredictionPhysicsProvider.UnityPhysics2D) != 0)
                Physics2D.simulationMode = SimulationMode2D.Script;
            if ((_physicsProvider & PredictionPhysicsProvider.UnityPhysics3D) != 0)
                Physics.simulationMode = SimulationMode.Script;

            InitPooling();
        }

        [ServerRpc(requireOwnership: false)]
        public Task ClientRequestedToBeObserver(PredictedComponentID component, RPCInfo info = default)
        {
            if (component.TryGetIdentity<PredictedIdentitySpawner>(this, out var pidSpawner))
                pidSpawner.ClientRequestedToBeObserver(info.sender);
            return Task.CompletedTask;
        }

        private static GameObject _poolParent;

        private void InitPooling()
        {
            if (!_predictedPrefabs)
                return;

            if (!_poolParent)
            {
                _poolParent = new GameObject("PooledPrefabs");
#if !PURRNET_DEBUG_POOLING
                _poolParent.hideFlags = HideFlags.HideAndDontSave;
#endif
                _poolParent.SetActive(false);
            }

            _pools = new GameObjectPoolCollection(_poolParent.transform);
            for (var i = 0; i < _predictedPrefabs.prefabs.Count; i++)
            {
                var prefab = _predictedPrefabs.prefabs[i];
                if (prefab.pooling.usePooling)
                    _pools.Register(prefab.prefab, prefab.pooling.initialSize);
            }
        }

        public float tickDelta { get; private set; }

        public int tickRate { get; private set; }

        public ulong localTick { get; private set; } = 1;

        [UsedImplicitly]
        public ulong localTickInContext { get; private set; } = 1;

        public PredictedHierarchy hierarchy { get; private set; }

        public PredictedPlayers players { get; private set; }

        internal Predicted3DPhysics physics3d { get; private set; }

        internal Predicted2DPhysics physics2d { get; private set; }

        public PredictedTime time { get; private set; }

        private DeltaModule _deltaModuleState;

        bool ShouldRegisterSystem(BuiltInSystems system)
        {
            return (_builtInSystems & system) != 0;
        }

        protected override void OnEarlySpawn()
        {
            var deltaModule = networkManager.GetModule<DeltaModule>(isServer);
            _deltaModuleState = deltaModule;

            RegisterScene();

            tickRate = networkManager.tickModule.tickRate;
            tickDelta = 1f / tickRate;
            _lastVerifiedTick = 0;

            if (ShouldRegisterSystem(BuiltInSystems.Hierarchy))
                hierarchy = RegisterSystem<PredictedHierarchy>();

            if (ShouldRegisterSystem(BuiltInSystems.Players))
                players = RegisterSystem<PredictedPlayers>();

            if (ShouldRegisterSystem(BuiltInSystems.Physics3D))
                physics3d = RegisterSystem<Predicted3DPhysics>();

            if (ShouldRegisterSystem(BuiltInSystems.Physics2D))
                physics2d = RegisterSystem<Predicted2DPhysics>();

            if (ShouldRegisterSystem(BuiltInSystems.Time))
                time = RegisterSystem<PredictedTime>();

            var roots = HashSetPool<GameObject>.Instantiate();
            var pid = -1;

            if (hierarchy)
            {
                for (var i = 0; i < _queue.Count; i++)
                {
                    var queued = _queue[i];
                    var root = queued.GetRoot();

                    if (roots.Add(root))
                    {
                        if (!_poolParent || root.transform.root != _poolParent.transform)
                            hierarchy.RegisterSceneObject(root, pid--);
                    }
                }
            }

            _queue.Clear();

            if ((_physicsProvider & PredictionPhysicsProvider.UnityPhysics2D) != 0 ||
                (_physicsProvider & PredictionPhysicsProvider.UnityPhysics3D) != 0)
            {
                Time.fixedDeltaTime = tickDelta;
            }
        }

        private void RegisterScene()
        {
            var identities = ListPool<PredictedIdentity>.Instantiate();
            SceneObjectsModule.GetScenePredictedIdentities(gameObject.scene, identities);

            int count = identities.Count;
            for (var i = 0; i < count; ++i)
            {
                var pid = identities[i];
                _queue.Add(pid);
            }
            ListPool<PredictedIdentity>.Destroy(identities);
        }

        private TickManager _tickManager;

        protected override void OnSpawned()
        {
            _tickManager = networkManager.tickModule;
            _tickManager.onPreTick += OnPreTick;
            _tickManager.onPostTick += OnPostTick;
        }

        protected override void OnDespawned()
        {
            if (_tickManager != null)
            {
                _tickManager.onPreTick -= OnPreTick;
                _tickManager.onPostTick -= OnPostTick;
            }

            CleanupAllSystems();
        }

        private void CleanupAllSystems()
        {
            if (hierarchy)
                hierarchy.Cleanup();

            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                if (_systems[i])
                    Destroy(_systems[i]);
            }

            _instanceMap.Clear();
            _queue.Clear();
            _systems.Clear();
            _nextSystemId = 0;
            _deltas.Clear();
        }

        private uint _nextSystemId;

        public T RegisterSystem<T>() where T : PredictedIdentity
        {
            var system = gameObject.AddComponent<T>();
            system.hideFlags = HideFlags.NotEditable;
            RegisterInstance(system, new PredictedObjectID(0), _nextSystemId++, null);
            return system;
        }

        public void RegisterInstance(GameObject go, PredictedObjectID objectID, PlayerID? owner, bool reset)
        {
            var components = ListPool<PredictedIdentity>.Instantiate();
            go.GetComponentsInChildren(true, components);
            int count = components.Count;

            for (uint i = 0; i < count; i++)
            {
                var component = components[(int)i];

                if (!_systems.Contains(component))
                {
                    if (reset)
                        component.ResetState();
                    RegisterInstance(component, objectID, i, owner);
                }
            }

            ListPool<PredictedIdentity>.Destroy(components);
        }

        public void UnregisterInstance(GameObject go)
        {
            if (!go)
                return;

            var components = ListPool<PredictedIdentity>.Instantiate();
            go.GetComponentsInChildren(true, components);

            for (var i = 0; i < components.Count; i++)
            {
                if (components[i].hideFlags != HideFlags.NotEditable)
                    UnregisterInstance(components[i]);
            }

            ListPool<PredictedIdentity>.Destroy(components);
        }

        public void UnregisterPooledInstance(GameObject go)
        {
            if (!go) return;

            var components = ListPool<PredictedIdentity>.Instantiate();
            go.GetComponentsInChildren(true, components);

            for (var i = 0; i < components.Count; i++)
            {
                UnregisterInstance(components[i]);
                components[i].TriggerDestroyedEvent();
                components[i].TriggerOnPooledEvent();
            }

            ListPool<PredictedIdentity>.Destroy(components);
        }

        readonly Dictionary<PredictedComponentID, PredictedIdentity> _instanceMap = new ();

        public bool TryGetIdentity(PredictedComponentID id, out PredictedIdentity instance)
        {
            return _instanceMap.TryGetValue(id, out instance);
        }

        public PredictedIdentity GetIdentity(PredictedComponentID id)
        {
            return _instanceMap.GetValueOrDefault(id);
        }

        private void RegisterInstance(PredictedIdentity system, PredictedObjectID objectId, uint componentId, PlayerID? owner)
        {
            if (!isSpawned)
            {
                _queue.Add(system);
                return;
            }

            var pid = new PredictedComponentID(objectId, componentId);
            _instanceMap[pid] = system;
            system.Setup(networkManager, this, pid, owner);

            _systems.Add(system);
        }

        public void UnregisterInstance(PredictedIdentity predictedIdentity)
        {
            _instanceMap.Remove(predictedIdentity.id);
            _systems.Remove(predictedIdentity);
        }


        protected override void OnObserverRemoved(PlayerID player)
        {
            _clientTicks.Remove(player);

            var frames = _clientFrames.Count;
            for (var i = 0; i < frames; i++)
            {
                if (_clientFrames[i].player == player)
                {
                    _clientFrames[i].Dispose();
                    _clientFrames.RemoveAt(i);
                    break;
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            foreach (var packer in _clientFrames)
                packer.Dispose();
            _clientFrames.Clear();

            if (_tickManager != null)
            {
                _tickManager.onPreTick -= OnPreTick;
                _tickManager.onPostTick -= OnPostTick;
            }
        }

        protected override void OnObserverAdded(PlayerID player)
        {
            if (player == localPlayer)
                return;

            _clientTicks[player] = new InputQueue();
            _clientFrames.Add(new PlayerPacker
            {
                player = player,
                packer = BitPackerPool.Get()
            });

            using var frame = BitPackerPool.Get();
            WriteFullFrame(frame, player);
            SyncFullState(player, tickRate, tickDelta, frame);
        }

        private void WriteFullFrame(BitPacker packer, PlayerID target)
        {
            int count = _systems.Count;
            Packer<PackedInt>.Write(packer, count);

            for (var i = 0; i < count; i++)
            {
                if (_systems[i].isEventHandler)
                    continue;

                _systems[i].WriteCurrentState(target, packer, _deltaModuleState);
            }


            for (var i = 0; i < count; i++)
                _systems[i].WriteInput(localTick, target, packer, _deltaModuleState, true);

            for (var i = 0; i < count; i++)
            {
                if (!_systems[i].isEventHandler)
                    continue;
                _systems[i].WriteCurrentState(target, packer, _deltaModuleState);
            }
        }

        [TargetRpc(compressionLevel: CompressionLevel.Best)]
        private void SyncFullState([UsedImplicitly] PlayerID target, int tickRate, float delta, BitPacker data)
        {
            isSimulating = true;

            tickDelta = delta;
            this.tickRate = tickRate;

            PackedInt _count = default;
            Packer<PackedInt>.Read(data, ref _count);
            int count = _count;

            for (var i = 0; i < count; i++)
            {
                var system = _systems[i];
                if (system.isEventHandler)
                    continue;
                system.ReadState(localTick, data, _deltaModuleState);
                system.Rollback(localTick);
                system.ResetInterpolation();
            }

            for (var i = 0; i < count; i++)
                _systems[i].ReadInput(localTick, default, data, _deltaModuleState, true);

            for (var i = 0; i < count; i++)
            {
                if (!_systems[i].isEventHandler)
                    continue;

                _systems[i].ReadState(localTick, data, _deltaModuleState);
            }

            SyncTransforms();

            isSimulating = false;
        }

        readonly List<PlayerPacker> _clientFrames = new (16);

        public bool cachedIsServer { get; private set; }

        void OnPreTick()
        {
            localTickInContext = localTick;

            var myPlayer = localPlayer ?? default;
            cachedIsServer = isServer;
            var cachedIsClient = isClient;

            isSimulating = true;
            if (cachedIsServer)
                isVerified = true;

            var scount = _systems.Count;
            bool hasClients = _clientTicks.Count > 0;

            if (cachedIsServer && hasClients)
                PrepareInputs();

            for (var i = 0; i < scount; i++)
            {
                var system = _systems[i];
                bool controller = system.IsOwner(myPlayer, cachedIsServer);
                system.PrepareInput(cachedIsServer, controller, localTick);
            }

            if (cachedIsServer && hasClients)
            {
                ResetAllPackers();
                WriteInitialFrameToOthers();
            }

            float delta = this.tickDelta;

            if (time)
                delta *= time.timeScale;

            for (var i = 0; i < _systems.Count; i++)
                _systems[i].SimulateTick(localTick, delta);

            DoPhysicsPass();

            if (cachedIsServer && hasClients)
            {
                WriteEventHandles();
                SendFrameToOthers();
            }

            for (var i = 0; i < _systems.Count; i++)
                _systems[i].PostSimulate(localTick, delta);

            if (cachedIsServer)
                 FinalizeTickOnServer(cachedIsClient);
            else FinalizeInputOnClient(myPlayer);

            isSimulating = false;

            localTick += 1;
            localTickInContext = localTick;
        }

        private void PrepareInputs()
        {
            foreach (var (player, queue) in _clientTicks)
            {
                if (queue.Count == 0)
                {
                    queue.waitForInput = true;
                    continue;
                }

                var dequeued = queue.inputQueue.Peek();
                HandleIncomingInput(dequeued.inputPacket, dequeued.count, player);
            }
        }

        private void FinalizeInputOnClient(PlayerID myPlayer)
        {
            const int MTU = 1024;

            using var frame = BitPackerPool.Get();
            uint writtenCount = 0;
            for (var systemIdx = 0; systemIdx < _systems.Count; systemIdx++)
            {
                var system = _systems[systemIdx];
                system.GetLatestUnityState();
                if (system.hasInput && system.IsOwner(myPlayer))
                {
                    Packer<PredictedComponentID>.Write(frame, system.id);
                    system.WriteInput(localTick, default, frame, _deltaModuleState, false);
                    writtenCount += 1;
                }
            }

            if (frame.positionInBytes >= MTU)
                SendInputToServerReliable(localTick, writtenCount, frame);
            else SendInputToServer(localTick, writtenCount, frame);
        }

        private void FinalizeTickOnServer(bool cachedIsClient)
        {
            var count = _systems.Count;
            if (cachedIsClient)
            {
                for (var systemIdx = 0; systemIdx < count; systemIdx++)
                {
                    _systems[systemIdx].GetLatestUnityState();
                    _systems[systemIdx].UpdateRollbackInterpolationState(tickDelta, false);
                    _systems[systemIdx].SaveStateInHistory(localTick);
                }
            }
            else
            {
                for (var systemIdx = 0; systemIdx < count; systemIdx++)
                {
                    _systems[systemIdx].GetLatestUnityState();
                    _systems[systemIdx].SaveStateInHistory(localTick);
                }
            }
        }

        private void ResetAllPackers()
        {
            for (var i = 0; i < _clientFrames.Count; i++)
            {
                var packer = _clientFrames[i];
                packer.packer.ResetPositionAndMode(false);
            }
        }

        private void WriteInitialFrameToOthers()
        {
            var count = _systems.Count;
            var fCount = _clientFrames.Count;

            for (var j = 0; j < fCount; j++)
            {
                var frame = _clientFrames[j].packer;
                var player = _clientFrames[j].player;


                Packer<PackedInt>.Write(frame, count);

                for (var i = 0; i < count; i++)
                {
                    if (_systems[i].isEventHandler)
                        continue;
                    _systems[i].WriteCurrentState(player, frame, _deltaModuleState);
                }

                for (var i = 0; i < count; i++)
                    _systems[i].WriteInput(localTick, player, frame, _deltaModuleState, true);
            }
        }

        private void WriteEventHandles()
        {
            var fCount = _clientFrames.Count;
            int count = _systems.Count;

            for (var i = 0; i < count; i++)
            {
                if (!_systems[i].isEventHandler)
                    continue;

                for (var j = 0; j < fCount; j++)
                    _systems[i].WriteCurrentState(_clientFrames[j].player, _clientFrames[j].packer, _deltaModuleState);
            }
        }

        private void SendFrameToOthers()
        {
            var fCount = _clientFrames.Count;

            for (var j = 0; j < fCount; j++)
            {
                var player = _clientFrames[j].player;
                var packer = _clientFrames[j].packer;

                if (!_clientTicks.TryGetValue(player, out var queue))
                    continue;

                ulong tick = 0;

                if (queue.Count > 0 && !queue.waitForInput)
                {
                    var dequeued = queue.inputQueue.Dequeue();
                    tick = dequeued.clientTick;
                    dequeued.inputPacket.Dispose();
                }

                var deltaLen = packer.ToByteData().length;

                SendFrameToRemote(player, tick, new BitPackerWithLength(deltaLen, packer));
            }
        }

        /// <summary>
        /// Is the prediction manager currently replaying a frame?
        /// </summary>
        [UsedImplicitly]
        public bool isReplaying { get; private set; }

        /// <summary>
        /// Is the prediction manager currently replaying a verified frame?
        /// </summary>
        [UsedImplicitly]
        public bool isVerified { get; private set; }

        public bool isVerifiedAndReplaying
        {
            get => isVerified && isReplaying;
        }


        /// <summary>
        /// Is the prediction manager currently simulating a frame?
        /// This includes replaying frames.
        /// If this is false nothing should act on the state of the game and expect it to be correct.
        /// </summary>
        [UsedImplicitly]
        public bool isSimulating
        {
            get; private set;
        }

        /// <summary>
        /// True if the prediction manager is currently in the physics pass.
        /// </summary>
        [UsedImplicitly]
        public bool isInPhysicsPass
        {
            get; private set;
        }

        private void DoPhysicsPass()
        {
            isInPhysicsPass = true;
            var delta = tickDelta;
            if (time)
                delta *= time.timeScale;

            if ((_physicsProvider & PredictionPhysicsProvider.UnityPhysics2D) != 0)
            {
                var physicsScene = gameObject.scene.GetPhysicsScene2D();
                if (physicsScene.IsValid())
                    physicsScene.Simulate(delta);
            }

            if ((_physicsProvider & PredictionPhysicsProvider.UnityPhysics3D) != 0)
            {
                var physicsScene = gameObject.scene.GetPhysicsScene();
                if (physicsScene.IsValid())
                    physicsScene.Simulate(delta);
            }

            isInPhysicsPass = false;
        }

        struct FrameDelta : IDisposable
        {
            public BitPacker packer;
            public ulong clientTick;

            public void Dispose()
            {
                packer?.Dispose();
            }
        }

        readonly Queue<FrameDelta> _deltas = new ();

        [TargetRpc(compressionLevel: CompressionLevel.Best)]
        private void SendFrameToRemote([UsedImplicitly] PlayerID player, ulong clientLocalTick, BitPackerWithLength delta)
        {
            delta.packer.SkipBytes(delta.originalLength);
            _deltas.Enqueue(new FrameDelta
            {
                packer = delta.packer,
                clientTick = clientLocalTick
            });
        }

        private void RollbackToFrame(BitPacker frame, ulong inputTick, ulong stateTick)
        {
            frame.ResetPositionAndMode(true);

            PackedInt _count = default;
            Packer<PackedInt>.Read(frame, ref _count);
            int count = _count;

            for (var i = 0; i < count; ++i)
            {
                var system = _systems[i];
                if (system.isEventHandler)
                    continue;
                system.ReadState(stateTick, frame, _deltaModuleState);
                system.Rollback(stateTick);
            }

            for (var i = 0; i < count; ++i)
                _systems[i].ReadInput(inputTick, default, frame, _deltaModuleState, true);

            for (var i = 0; i < count; ++i)
            {
                if (!_systems[i].isEventHandler)
                    continue;
                _systems[i].ReadState(inputTick, frame, _deltaModuleState);
                _systems[i].Rollback(inputTick);
            }

            SyncTransforms();
        }

        private void SyncTransforms()
        {
            if ((_physicsProvider & PredictionPhysicsProvider.UnityPhysics2D) != 0)
                Physics2D.SyncTransforms();

            if ((_physicsProvider & PredictionPhysicsProvider.UnityPhysics3D) != 0)
                Physics.SyncTransforms();
        }

        private ulong _lastVerifiedTick = 1;

        private void OnPostTick()
        {
            if (_deltas.Count == 0)
            {
                if (isClient)
                    UpdateInterpolation(false);
                TickBandwidthProfiler.MarkEndOfTick();
                return;
            }

            UpdateInterpolation(false);

            isSimulating = true;
            isReplaying = true;
            isVerified = true;

            bool hasRollback = false;
            ulong verifiedTick = 1;
            while (_deltas.Count > 0)
            {
                using var previousFrame = _deltas.Dequeue();

                if (previousFrame.clientTick != 0)
                    _lastVerifiedTick = previousFrame.clientTick;

                hasRollback = true;
                verifiedTick = _lastVerifiedTick;
                localTickInContext = verifiedTick == 0 ? 0 : verifiedTick - 1;
                RollbackToFrame(previousFrame.packer, verifiedTick, localTickInContext);
                localTickInContext = verifiedTick;
                SimulateFrame(verifiedTick);
            }

            isVerified = false;

            if (hasRollback)
            {
                ReplayToLatestTick(verifiedTick + 1);
                SyncTransforms();
                UpdateInterpolation(true);
            }
            else
            {
                localTickInContext = localTick;
            }

            isReplaying = false;
            isSimulating = false;

            TickBandwidthProfiler.MarkEndOfTick();
        }

        private void UpdateInterpolation(bool accumulateError)
        {
            var scount = _systems.Count;
            for (var j = 0; j < scount; j++)
                _systems[j].UpdateRollbackInterpolationState(tickDelta, accumulateError);
        }

        private void ReplayToLatestTick(ulong verifiedTick)
        {
            var delta = tickDelta;
            if (time)
                delta *= time.timeScale;
            isSimulating = true;
            for (ulong simTick = verifiedTick; simTick < localTick; simTick++)
            {
                localTickInContext = simTick;

                for (var j = 0; j < _systems.Count; j++)
                    _systems[j].SimulateTick(simTick, delta);

                DoPhysicsPass();

                for (var i = 0; i < _systems.Count; i++)
                    _systems[i].PostSimulate(simTick, delta);

                var count = _systems.Count;
                for (var j = 0; j < count; j++)
                    _systems[j].GetLatestUnityState();
            }
            isSimulating = false;
            localTickInContext = localTick;
        }

        private void SimulateFrame(ulong verifiedTick)
        {
            var delta = tickDelta;
            if (time)
                delta *= time.timeScale;

            isSimulating = true;
            for (var j = 0; j < _systems.Count; j++)
                _systems[j].SimulateTick(verifiedTick, delta);

            DoPhysicsPass();

            for (var i = 0; i < _systems.Count; i++)
                _systems[i].PostSimulate(verifiedTick, delta);

            var count = _systems.Count;
            for (var j = 0; j < count; j++)
                _systems[j].GetLatestUnityState();
            isSimulating = false;
        }

        public struct InputQueueValue
        {
            public ulong clientTick;
            public PackedUInt count;
            public BitPacker inputPacket;
        }

        public class InputQueue
        {
            public bool waitForInput;
            public readonly Queue<InputQueueValue> inputQueue = new ();
            public int Count => inputQueue.Count;
        }

        readonly Dictionary<PlayerID, InputQueue> _clientTicks = new ();

        [ServerRpc(requireOwnership: false, channel: Channel.ReliableUnordered)]
        private void SendInputToServerReliable(ulong clientTick, PackedUInt count, BitPacker inputPacket, RPCInfo info = default)
        {
            ReceivedInput(clientTick, count, inputPacket, info);
        }

        [ServerRpc(requireOwnership: false, channel: Channel.Unreliable)]
        private void SendInputToServer(ulong clientTick, PackedUInt count, BitPacker inputPacket, RPCInfo info = default)
        {
            ReceivedInput(clientTick, count, inputPacket, info);
        }

        private void ReceivedInput(ulong clientTick, PackedUInt count, BitPacker inputPacket, RPCInfo info)
        {
            if (!_clientTicks.TryGetValue(info.sender, out var ticks))
            {
                ticks = new InputQueue
                {
                    waitForInput = true
                };
                _clientTicks[info.sender] = ticks;
            }

            // if we are past the max inputs, let's remove until we are at the min inputs
            if (ticks.Count > _inputQueueSettings.maxInputs)
            {
                while (ticks.Count > _inputQueueSettings.minInputs)
                {
                    var oldInput = ticks.inputQueue.Dequeue();
                    oldInput.inputPacket.Dispose();
                }
            }

            ticks.inputQueue.Enqueue(new InputQueueValue
            {
                clientTick = clientTick,
                count = count,
                inputPacket = inputPacket
            });

            if (ticks.waitForInput && ticks.inputQueue.Count >= _inputQueueSettings.minInputs)
                ticks.waitForInput = false;
        }

        private void HandleIncomingInput(BitPacker inputPacket, PackedUInt count, PlayerID sender)
        {
            try
            {
                bool senderIsServer = sender == default;

                for (var i = 0; i < count; i++)
                {
                    PredictedComponentID pid = default;
                    Packer<PredictedComponentID>.Read(inputPacket, ref pid);

                    if (_instanceMap.TryGetValue(pid, out var system) && system.IsOwner(sender, senderIsServer))
                    {
                        system.QueueInput(inputPacket, sender, _deltaModuleState, false);
                    }
                    else break;
                }
            }
            catch
            {
                // ignored
            }
        }

        private void Update()
        {
            if (_updateViewMode != UpdateViewMode.Update)
                return;

            if (!isClient)
                return;

            UpdateView();
        }

        private void LateUpdate()
        {
            if (_updateViewMode != UpdateViewMode.LateUpdate)
                return;

            if (!isClient)
                return;

            UpdateView();
        }

        private void UpdateView()
        {
            int count = _systems.Count;

            for (var i = 0; i < count; i++)
                _systems[i].UpdateView(Time.unscaledDeltaTime);
        }

        public bool TryGetPrefab(int pid, out GameObject prefab)
        {
            if (pid < 0 || pid >= _predictedPrefabs.prefabs.Count)
            {
                prefab = null;
                return false;
            }

            prefab = _predictedPrefabs.prefabs[pid].prefab;
            return true;
        }

        public bool TryGetPrefab(GameObject prefab, out int id)
        {
            if (!_predictedPrefabs)
            {
                PurrLogger.LogError($"No predicted prefabs scriptable found on prediction manager! Make sure you've populated the field.", this);
                id = -1;
                return false;
            }

            var prefabs = _predictedPrefabs.prefabs;
            for (id = 0; id < prefabs.Count; id++)
            {
                if (prefabs[id].prefab == prefab)
                    return true;
            }

            id = -1;
            return false;
        }

        public static void ProperlySetPosAndRot(Transform transform, Vector3 position, Quaternion rotation)
        {
            if (transform.TryGetComponent(out Rigidbody2D rb2d))
            {
                rb2d.position = position;
                rb2d.rotation = rotation.eulerAngles.z;
                transform.SetPositionAndRotation(position, rotation);
            }
            else if  (transform.TryGetComponent(out Rigidbody rb))
            {
                rb.position = position;
                rb.rotation = rotation;
                transform.SetPositionAndRotation(position, rotation);
            }
            else if (transform.TryGetComponent(out CharacterController ctrler) && ctrler.enabled)
            {
                ctrler.enabled = false;
                transform.SetPositionAndRotation(position, rotation);
                ctrler.enabled = true;
            }
            else transform.SetPositionAndRotation(position, rotation);
        }

        internal GameObject InternalCreate(GameObject prefab, Vector3 position, Quaternion rotation, PredictedObjectID objectId, PlayerID? owner)
        {
            if (_pools.TryGetPool(prefab, out var pool))
            {
                var go = pool.Allocate();
                var trs = go.transform;
                ProperlySetPosAndRot(trs, position, rotation);
                trs.SetParent(null);
                RegisterInstance(go, objectId, owner, true);
                return go;
            }
            else
            {
                var go = UnityProxy.InstantiateDirectly(prefab, position, rotation);
                RegisterInstance(go, objectId, owner, false);
                return go;
            }
        }

        internal void InternalDelete(PackedInt prefabId, GameObject instance)
        {
            if (!_predictedPrefabs)
            {
                UnityProxy.DestroyImmediateDirectly(instance);
                return;
            }

            if (prefabId < 0 || prefabId >= _predictedPrefabs.prefabs.Count)
            {
                UnityProxy.DestroyImmediateDirectly(instance);
                return;
            }

            var prefab = _predictedPrefabs.prefabs[prefabId].prefab;

            if (_pools.TryGetPool(prefab, out var pool))
            {
                UnregisterPooledInstance(instance);
                pool.Delete(instance);
            }
            else UnityProxy.DestroyImmediateDirectly(instance);
        }

        public void SetOwnership(PredictedObjectID? root, PlayerID? player)
        {
            if (!hierarchy.TryGetGameObject(root, out var rootGo))
                return;

            var children = ListPool<PredictedIdentity>.Instantiate();

            rootGo.GetComponentsInChildren(true, children);

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                child.owner = player;
            }

            ListPool<PredictedIdentity>.Destroy(children);
        }

        public static bool TryGetClosestPredictedID(GameObject go, out PredictedComponentID pid)
        {
            if (go.TryGetComponent<PredictedIdentity>(out var identity))
            {
                pid = identity.id;
                return true;
            }

            var parent = go.GetComponentInParent<PredictedIdentity>();
            if (parent != null)
            {
                pid = parent.id;
                return true;
            }

            pid = default;
            return false;
        }
    }
}
