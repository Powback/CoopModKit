using System;
using System.Collections.Generic;

namespace CoopKit
{
    /// <summary>
    /// Typed message bus (pattern proven by SilklessCoop's SilklessAPI):
    /// register handlers per packet type, dispatch by runtime type, stay
    /// transport-agnostic — the same bus serves Steam P2P, a relay server,
    /// or in-process events. Pure C#; single-threaded by contract.
    /// </summary>
    public sealed class PacketBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers =
            new Dictionary<Type, List<Delegate>>();

        public void AddHandler<T>(Action<T> handler)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<Delegate>();
            list.Add(handler);
        }

        public void RemoveHandler<T>(Action<T> handler)
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        /// <summary>Dispatch to the packet's exact runtime type. Returns handler count.</summary>
        public int Dispatch(object packet)
        {
            if (packet == null) return 0;
            if (!_handlers.TryGetValue(packet.GetType(), out var list)) return 0;

            // Copy: a handler may add/remove handlers while running.
            var snapshot = list.ToArray();
            foreach (var d in snapshot) d.DynamicInvoke(packet);
            return snapshot.Length;
        }
    }
}
