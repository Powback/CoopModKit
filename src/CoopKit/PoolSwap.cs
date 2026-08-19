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
