using System;

namespace HyperFrame.Core
{
    public enum Ease
    {
        Linear, InQuad, OutQuad, InOutQuad, InCubic, OutCubic, InOutCubic,
        InBack, OutBack, InOutBack, OutElastic, OutBounce, InSine, OutSine, InOutSine
    }

    public static class Easing
    {
        public static float Evaluate(Ease ease, float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c1 = 1.70158f, c2 = c1 * 1.525f, c3 = c1 + 1f;
            switch (ease)
            {
                case Ease.Linear: return t;
                case Ease.InQuad: return t * t;
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Pow(-2f * t + 2f, 2) / 2f;
                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: return 1f - Pow(1f - t, 3);
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Pow(-2f * t + 2f, 3) / 2f;
                case Ease.InBack: return c3 * t * t * t - c1 * t * t;
                case Ease.OutBack: return 1f + c3 * Pow(t - 1f, 3) + c1 * Pow(t - 1f, 2);
                case Ease.InOutBack:
                    return t < 0.5f
                        ? Pow(2f * t, 2) * ((c2 + 1f) * 2f * t - c2) / 2f
                        : (Pow(2f * t - 2f, 2) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;
                case Ease.OutElastic:
                    return (float)(Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * (2 * Math.PI / 3)) + 1);
                case Ease.OutBounce: return OutBounce(t);
                case Ease.InSine: return 1f - (float)Math.Cos(t * Math.PI / 2);
                case Ease.OutSine: return (float)Math.Sin(t * Math.PI / 2);
                case Ease.InOutSine: return -((float)Math.Cos(Math.PI * t) - 1f) / 2f;
                default: return t;
            }
        }

        static float Pow(float x, int p)
        {
            float r = 1f;
            for (int i = 0; i < p; i++) r *= x;
            return r;
        }

        static float OutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
