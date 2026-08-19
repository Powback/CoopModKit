using System;
using System.Collections.Generic;

namespace CoopKit
{
    /// <summary>
    /// Scoped field swap: per-player resource pools over a shared save object.
    ///
    /// The game's own state-touching code (damage math, modifiers, resource
    /// gain) runs unmodified — for the duration of one player's call, the
    /// shared fields hold that player's pool, and what vanilla computed is
    /// written back to the pool afterwards. Register each field once; open a
    /// scope per call; restore via Finalizer.
    /// </summary>
    public sealed class PoolSwap<TPlayer> where TPlayer : class
    {
        private sealed class Field
        {
            internal Func<int> GetShared;
            internal Action<int> SetShared;
            internal Func<TPlayer, int> GetPool;
            internal Action<TPlayer, int> SetPool;
        }

        private readonly List<Field> _fields = new List<Field>();

        // Reentrancy: a wrapped method calling another wrapped method on the
        // SAME player must not re-snapshot — the shared fields already hold
        // that player's pool, so an inner swap would capture a stale snapshot
        // and its End would clobber the inner computation. Only the outermost
        // scope per player does the work; inner scopes pass through.
        // (Single-threaded by contract: game main thread.)
        private readonly Dictionary<TPlayer, int> _depth = new Dictionary<TPlayer, int>();

        public PoolSwap<TPlayer> Add(
            Func<int> getShared, Action<int> setShared,
            Func<TPlayer, int> getPool, Action<TPlayer, int> setPool)
        {
            _fields.Add(new Field
            {
                GetShared = getShared, SetShared = setShared,
                GetPool = getPool, SetPool = setPool,
            });
            return this;
        }

        /// <summary>Swap the player's pools in; null when player is null.</summary>
        public Scope Begin(TPlayer player)
        {
            if (player == null) return null;

            _depth.TryGetValue(player, out var depth);
            _depth[player] = depth + 1;
            if (depth > 0) return new Scope(this, player, null);   // passthrough

            var saved = new int[_fields.Count];
            for (var i = 0; i < _fields.Count; i++)
            {
                saved[i] = _fields[i].GetShared();
                _fields[i].SetShared(_fields[i].GetPool(player));
            }
            return new Scope(this, player, saved);
        }

        public sealed class Scope
        {
            private readonly PoolSwap<TPlayer> _owner;
            private readonly TPlayer _player;
            private readonly int[] _saved;
            private bool _ended;

            internal Scope(PoolSwap<TPlayer> owner, TPlayer player, int[] saved)
            {
                _owner = owner; _player = player; _saved = saved;
            }

            /// <summary>
            /// Keep what vanilla computed for the player, give the shared
            /// object back its values. Idempotent; call from a Finalizer.
            /// </summary>
            public void End()
            {
                if (_ended) return;
                _ended = true;

                if (_owner._depth.TryGetValue(_player, out var depth))
                {
                    if (depth <= 1) _owner._depth.Remove(_player);
                    else _owner._depth[_player] = depth - 1;
                }

                if (_saved == null) return;   // passthrough scope

                for (var i = 0; i < _owner._fields.Count; i++)
                {
                    var f = _owner._fields[i];
                    f.SetPool(_player, f.GetShared());
                    f.SetShared(_saved[i]);
                }
            }
        }
    }
}
