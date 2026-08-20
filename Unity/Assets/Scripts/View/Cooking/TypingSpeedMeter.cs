// 仕様書: Unity/docs/.sdd/cooking-anim/01-cooking-animation.md §4.1
// 直近 N 打の間隔から打鍵速度（KPM = keys per minute）を出す。UnityEngine に依存しない純クラス。
//
// 計測するだけで段階は決めない（決めるのは TypingSpeedTierRule）。

namespace Takoda99.View.Cooking
{
    /// <summary>直近の打鍵時刻をリングバッファに持ち、そこから KPM を算出する。</summary>
    public sealed class TypingSpeedMeter
    {
        private readonly double[] timestamps; // 秒。リングバッファ
        private int count;                    // 溜まった件数（capacity で頭打ち）
        private int head;                     // 次に書く位置

        /// <param name="windowKeys">KPM の算出に使う直近の打鍵数。2 未満は 2 に切り上げる。</param>
        public TypingSpeedMeter(int windowKeys)
        {
            var capacity = windowKeys < 2 ? 2 : windowKeys;
            timestamps = new double[capacity];
        }

        /// <summary>KPM を出すのに足るだけ打鍵が溜まっているか。</summary>
        public bool HasSample => count >= 2;

        /// <summary>
        /// リングバッファが満杯か（<c>windowKeys</c> ぶん溜まったか）。
        /// 溜まる前の KPM は、打ち始めの数打の間隔だけで計算されるため、
        /// 偶然の連打で異常に高い値が出ることがある（穴数がいきなり跳ね上がる原因）。
        /// 段階の判定はこれが true になってから行う（cooking-anim/01 §4.1）。
        /// </summary>
        public bool HasFullWindow => count >= timestamps.Length;

        /// <summary>1打ぶん記録する。<paramref name="nowSeconds"/> は単調増加する時刻（Time.unscaledTimeAsDouble 等）。</summary>
        public void Record(double nowSeconds)
        {
            timestamps[head] = nowSeconds;
            head = (head + 1) % timestamps.Length;

            if (count < timestamps.Length)
            {
                count++;
            }
        }

        /// <summary>すべての記録を捨てる。客が入れ替わった・試合が始まった等の区切りで呼ぶ。</summary>
        public void Reset()
        {
            count = 0;
            head = 0;
        }

        /// <summary>
        /// 直近の打鍵速度（KPM）。サンプルが足りなければ 0。
        /// <paramref name="nowSeconds"/> を渡すと、最後の打鍵からの無音時間も分母に入れる
        /// （打鍵が途切れたら KPM が自然に落ちる）。
        /// </summary>
        public float CalculateKpm(double nowSeconds)
        {
            if (!HasSample)
            {
                return 0f;
            }

            var oldestIndex = count < timestamps.Length ? 0 : head;
            var oldest = timestamps[oldestIndex];
            var latest = timestamps[(head - 1 + timestamps.Length) % timestamps.Length];

            // 最後の打鍵より現在が進んでいれば、その無音ぶんも経過時間に含める。
            var elapsed = (nowSeconds > latest ? nowSeconds : latest) - oldest;
            if (elapsed <= 0d)
            {
                return 0f;
            }

            // 区間の数は「打鍵数 - 1」。ただし無音を含めたぶん、最後の1打も区間として数える。
            var intervals = nowSeconds > latest ? count : count - 1;
            return (float)(intervals / elapsed * 60d);
        }
    }
}
