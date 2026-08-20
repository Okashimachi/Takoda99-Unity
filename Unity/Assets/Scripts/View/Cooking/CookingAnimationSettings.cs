// 仕様書: Unity/docs/.sdd/cooking-anim/01-cooking-animation.md §5
// たこ焼き調理アニメーションの調整値を一手に持つ ScriptableObject。
//
// **演出の数値をコードに直書きしない。** 企画が実機を見ながら調整するため、
// 尺・振れ幅・しきい値はすべてここに集約する。各 View はこの asset を参照するだけで、
// 既定値も含めて自前の数値を持たない。

using System;
using UnityEngine;

namespace Takoda99.View.Cooking
{
    /// <summary>たこ焼き調理アニメーションの全調整値。Assets/Settings/CookingAnimationSettings.asset に1つ置く。</summary>
    [CreateAssetMenu(
        fileName = "CookingAnimationSettings",
        menuName = "Takoda99/Cooking Animation Settings",
        order = 100)]
    public sealed class CookingAnimationSettings : ScriptableObject
    {
        /// <summary>打鍵速度（KPM）と、そのとき生地を流す穴の数の対応1件。</summary>
        [Serializable]
        public struct SpeedTier
        {
            [Tooltip("この KPM 以上でこの段階になる。")]
            [SerializeField] private float minKpm;

            [Tooltip("生地を流す穴の数（1..24）。")]
            [SerializeField] private int slotCount;

            public float MinKpm => minKpm;
            public int SlotCount => slotCount;
        }

        // ── 手（企画書 2, 3番）────────────────────────────────────────
        [Header("手：打鍵ごとの反応（2番）")]
        [Tooltip("縦に1往復する尺（ミリ秒）。下げと戻しで折半する。")]
        [SerializeField] private int handKeyDurationMs = 50;

        [Tooltip("下げる量（px）。下向きなので負値。")]
        [SerializeField] private float handKeyOffsetY = -12f;

        [Header("手：ミス反応（3番）")]
        [Tooltip("横に1往復する尺（ミリ秒）。左28ms・右28ms・中央14ms の比率で割る。")]
        [SerializeField] private int handMissDurationMs = 70;

        [Tooltip("左右に振れる量（px）。")]
        [SerializeField] private float handMissOffsetX = 10f;

        // ── 窪み（企画書 5, 6, 7番）──────────────────────────────────
        [Header("窪み：生地投入（5番）")]
        [Tooltip("生地が落ちる尺（ミリ秒）。")]
        [SerializeField] private int batterFallMs = 60;

        [Tooltip("落ちた生地が広がる尺（ミリ秒）。")]
        [SerializeField] private int batterSpreadMs = 40;

        [Tooltip("落下開始時のスケール。1 より大きいと上から降ってきたように見える。")]
        [SerializeField] private float batterFallStartScale = 1.3f;

        [Header("窪み：焼き上がり（6番。単語を打ち切った瞬間に切り替える）")]
        [Tooltip("生地→焼きのクロスフェード尺（ミリ秒）。たこ焼きの回転もこの尺に合わせる。")]
        [SerializeField] private int cookedFadeMs = 90;

        [Header("手・窪み：ひっくり返し演出（タイプ完了と同時）")]
        [Tooltip("手が対象の穴まで移動して戻ってくる尺（ミリ秒）。往復で折半する。")]
        [SerializeField] private int handFlipDurationMs = 160;

        [Tooltip("焼き上がりの切り替え中にたこ焼きが回転する角度（度）。360で1回転して元の向きに戻る。")]
        [SerializeField] private float takoyakiFlipRotationDegrees = 360f;

        [Header("舟皿：盛り付けの出来（7番）")]
        [Tooltip("盛り付け済みの絵が出るときのフェードイン尺（ミリ秒）。")]
        [SerializeField] private int trayServedFadeMs = 80;

        [Tooltip("1注文ぶんの打鍵ミス率がこの値以下なら「きれい」。0 ならノーミスのみ。")]
        [SerializeField, Range(0f, 1f)] private float trayCleanMaxMissRatio = 0f;

        [Tooltip("1注文ぶんの打鍵ミス率がこの値以下なら「ふつう」。超えたら「汚い」。")]
        [SerializeField, Range(0f, 1f)] private float trayNormalMaxMissRatio = 0.15f;

        // ── 舟皿へ飛ぶ（企画書 8番）──────────────────────────────────
        [Header("完成：舟皿へ（8番）")]
        [Tooltip("窪みから浮き上がる尺（ミリ秒）。")]
        [SerializeField] private int flyRiseMs = 90;

        [Tooltip("弧を描いて移動する尺（ミリ秒）。")]
        [SerializeField] private int flyArcMs = 80;

        [Tooltip("舟皿へ着地する尺（ミリ秒）。")]
        [SerializeField] private int flyLandMs = 50;

        [Tooltip("弧の頂点の高さ。窪みの高さに対する倍率。")]
        [SerializeField] private float flyApexHeightScale = 1.4f;

        [Tooltip("一斉に盛り付けるとき、1個ごとに出発を後ろへずらす量（ミリ秒）。0 だと1個の塊に見える。")]
        [SerializeField] private int flyStaggerMs = 40;

        // ── 提供（企画書 9番）────────────────────────────────────────
        [Header("舟皿：提供（9番）")]
        [Tooltip("最後の1個が着地してから提供演出を始めるまでの余韻（ミリ秒）。")]
        [SerializeField] private int serveDelayMs = 180;

        [Tooltip("皿が縮小・スライド・フェードアウトする尺（ミリ秒）。")]
        [SerializeField] private int serveMs = 380;

        [Tooltip("新しい空の皿がフェードインする尺（ミリ秒）。")]
        [SerializeField] private int trayFadeInMs = 220;

        [Tooltip("提供の消失と新しい皿の出現を重ねる時間（ミリ秒）。")]
        [SerializeField] private int trayCrossOverlapMs = 50;

        [Tooltip("提供時に皿が客の方向へ動く量（px）。")]
        [SerializeField] private Vector2 serveSlideOffset = new Vector2(-160f, 40f);

        [Tooltip("提供時に皿が縮む先のスケール。")]
        [SerializeField] private float serveEndScale = 0.6f;

        // ── 打鍵速度と使う穴数（本実装の追加分）──────────────────────
        [Header("打鍵速度 → 使う穴数")]
        [Tooltip("minKpm の昇順に並べる。先頭は必ず minKpm=0 にする。")]
        [SerializeField] private SpeedTier[] speedTiers = new SpeedTier[0];

        [Tooltip("KPM の算出に使う直近の打鍵数。少ないほど反応が速く、荒れる。")]
        [SerializeField] private int speedWindowKeys = 20;

        [Tooltip("段階が下がるとき、この時間だけ低い段階が続いてから反映する（ちらつき防止）。上がるときは即時。")]
        [SerializeField] private int speedTierDropHoldMs = 1500;

        public int HandKeyDurationMs => handKeyDurationMs;
        public float HandKeyOffsetY => handKeyOffsetY;
        public int HandMissDurationMs => handMissDurationMs;
        public float HandMissOffsetX => handMissOffsetX;

        public int BatterFallMs => batterFallMs;
        public int BatterSpreadMs => batterSpreadMs;
        public float BatterFallStartScale => batterFallStartScale;

        public int CookedFadeMs => cookedFadeMs;
        public int HandFlipDurationMs => handFlipDurationMs;
        public float TakoyakiFlipRotationDegrees => takoyakiFlipRotationDegrees;

        public int TrayServedFadeMs => trayServedFadeMs;
        public float TrayCleanMaxMissRatio => trayCleanMaxMissRatio;
        public float TrayNormalMaxMissRatio => trayNormalMaxMissRatio;

        public int FlyRiseMs => flyRiseMs;
        public int FlyArcMs => flyArcMs;
        public int FlyLandMs => flyLandMs;
        public float FlyApexHeightScale => flyApexHeightScale;
        public int FlyStaggerMs => flyStaggerMs;

        public int ServeDelayMs => serveDelayMs;
        public int ServeMs => serveMs;
        public int TrayFadeInMs => trayFadeInMs;
        public int TrayCrossOverlapMs => trayCrossOverlapMs;
        public Vector2 ServeSlideOffset => serveSlideOffset;
        public float ServeEndScale => serveEndScale;

        public SpeedTier[] SpeedTiers => speedTiers;
        public int SpeedWindowKeys => speedWindowKeys;
        public int SpeedTierDropHoldMs => speedTierDropHoldMs;

        /// <summary>ミリ秒を秒へ。0 以下は 0 にする（尺 0 は「即時」を意味する）。</summary>
        public static float ToSeconds(int milliseconds) => milliseconds <= 0 ? 0f : milliseconds / 1000f;

        private void OnValidate()
        {
            if (speedWindowKeys < 2)
            {
                speedWindowKeys = 2;
            }

            if (trayNormalMaxMissRatio < trayCleanMaxMissRatio)
            {
                trayNormalMaxMissRatio = trayCleanMaxMissRatio;
            }
        }
    }
}
