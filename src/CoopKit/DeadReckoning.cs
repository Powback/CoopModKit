namespace CoopKit
{
    /// <summary>
    /// Dead reckoning with staleness fade (pattern proven by SilklessCoop's
    /// SimpleInterpolator): between updates an entity advances along its last
    /// known velocity, and its presence weight decays toward zero so a silent
    /// peer fades out instead of freezing mid-air. Pure math — the consumer
    /// applies (position, alpha) to whatever renders.
    /// </summary>
    public struct DeadReckoning
    {
        public float X, Y;
        public float VelX, VelY;
        public double UpdatedAt;

        /// <summary>Seconds of silence until alpha reaches zero.</summary>
        public double FadeSeconds;

        public static DeadReckoning At(float x, float y, double now, double fadeSeconds = 1.0)
            => new DeadReckoning { X = x, Y = y, UpdatedAt = now, FadeSeconds = fadeSeconds };

        /// <summary>Feed a fresh update: position + velocity at time <paramref name="now"/>.</summary>
        public void Update(float x, float y, float velX, float velY, double now)
        {
            X = x; Y = y; VelX = velX; VelY = velY; UpdatedAt = now;
        }

        /// <summary>
        /// Extrapolated position and presence alpha (1 fresh → 0 stale) at
        /// time <paramref name="now"/>. Extrapolation stops with the fade so a
        /// dead peer never glides forever.
        /// </summary>
        public (float x, float y, float alpha) Sample(double now)
        {
            var age = now - UpdatedAt;
            if (age <= 0) return (X, Y, 1f);

            var alpha = FadeSeconds <= 0 ? 0.0 : 1.0 - age / FadeSeconds;
            if (alpha < 0) alpha = 0;

            var t = (float)(age < FadeSeconds ? age : FadeSeconds);
            return (X + VelX * t, Y + VelY * t, (float)alpha);
        }
    }
}
