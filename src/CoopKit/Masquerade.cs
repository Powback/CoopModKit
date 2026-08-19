using System;

namespace CoopKit
{
    /// <summary>
    /// Singleton masquerade: temporarily point a game's "the player" reference
    /// at another actor for the duration of a call that gates on identity, and
    /// guarantee the restore.
    ///
    /// The scope object is deliberately storable (Harmony `__state`) so the
    /// restore can live in a Finalizer — Postfixes are skipped when the
    /// patched method throws, and a singleton left pointing at an impostor is
    /// the worst failure mode this pattern has.
    /// </summary>
    public sealed class Masquerade<T> where T : class
    {
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        public Masquerade(Func<T> get, Action<T> set)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        /// <summary>Begin impersonation; null when no scope was needed.</summary>
        public Scope Impersonate(T impostor)
        {
            if (impostor == null) return null;
            var scope = new Scope(this, _get());
            _set(impostor);
            return scope;
        }

        /// <summary>Run an action while the singleton points at the impostor.</summary>
        public void Run(T impostor, Action action)
        {
            var scope = Impersonate(impostor);
            try { action(); }
            finally { scope?.Restore(); }
        }

        public sealed class Scope
        {
            private readonly Masquerade<T> _owner;
            private readonly T _saved;
            private bool _restored;

            internal Scope(Masquerade<T> owner, T saved)
            {
                _owner = owner;
                _saved = saved;
            }

            /// <summary>Idempotent; safe to call from any exit path.</summary>
            public void Restore()
            {
                if (_restored) return;
                _restored = true;
                _owner._set(_saved);
            }
        }
    }
}
