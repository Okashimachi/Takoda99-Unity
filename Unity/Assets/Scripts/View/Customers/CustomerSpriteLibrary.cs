// 全キャラ・全状態の客の画像を ScriptableObject 1つで一括管理する。表示のみ（経営ロジックは持たない）。
// 客オブジェクト側はこのアセットへの参照と「属性 + 状態」だけを持ち、画像そのものは持たない。

using System;
using System.Collections.Generic;
using Takoda99.Proto;
using UnityEngine;

namespace Takoda99.View.Customers
{
    /// <summary>
    /// 客の見た目の状態。行列内の位置と提供の有無から決まる表示状態で、
    /// こちらは「行列内での振る舞い」を表す表示専用の区分。
    /// </summary>
    public enum CustomerVisualState
    {
        /// <summary>列待ち（行列の2番目以降）。</summary>
        Queued = 0,

        /// <summary>注文時（行列先頭に来て注文を告げる）。</summary>
        Ordering = 1,

        /// <summary>提供待機（注文後、たこ焼きが出てくるのを待つ）。モブは専用画像を持たない。</summary>
        WaitingForServe = 2,

        /// <summary>喜び（提供直後）。</summary>
        Delighted = 3,

        /// <summary>怒り。v0.8.0 では通常の試合中に発生しない（客が逃げないため）。</summary>
        Angry = 4,

        /// <summary>退店（離脱・提供後の立ち去り）。</summary>
        Leaving = 5,
    }

    /// <summary>
    /// 1キャラ（1属性）ぶんの全状態の画像。
    /// </summary>
    [Serializable]
    public sealed class CustomerSpriteSet
    {
        [Tooltip("この見た目を割り当てる客の属性（Proto の CustomerAttribute）。")]
        [SerializeField] private CustomerAttribute _attribute;

        [Tooltip("インスペクタ上の識別用。表示には使わない。")]
        [SerializeField] private string _label;

        [SerializeField] private Sprite _queued;
        [SerializeField] private Sprite _ordering;

        [Tooltip("モブなど専用画像を持たないキャラは空にする（Ordering にフォールバックする）。")]
        [SerializeField] private Sprite _waitingForServe;

        [SerializeField] private Sprite _delighted;
        [SerializeField] private Sprite _angry;
        [SerializeField] private Sprite _leaving;

        public CustomerAttribute Attribute => _attribute;

        public string Label => _label;

        /// <summary>提供待機の専用画像を持つか（モブは持たない）。</summary>
        public bool HasWaitingForServe => _waitingForServe != null;

        /// <summary>
        /// 状態に対応する画像を返す。<see cref="CustomerVisualState.WaitingForServe"/> の画像が
        /// 未設定の場合のみ <see cref="CustomerVisualState.Ordering"/> にフォールバックする。
        /// </summary>
        public Sprite Resolve(CustomerVisualState state)
        {
            switch (state)
            {
                case CustomerVisualState.Queued: return _queued;
                case CustomerVisualState.Ordering: return _ordering;
                case CustomerVisualState.WaitingForServe: return _waitingForServe != null ? _waitingForServe : _ordering;
                case CustomerVisualState.Delighted: return _delighted;
                case CustomerVisualState.Angry: return _angry;
                case CustomerVisualState.Leaving: return _leaving;
                default: return _queued;
            }
        }
    }

    /// <summary>
    /// 全キャラ・全状態の客画像を一括管理する ScriptableObject。
    /// ゲーム中の客はこのアセットを参照し、(属性, 状態) から画像を引くだけにする。
    /// </summary>
    [CreateAssetMenu(fileName = "CustomerSpriteLibrary", menuName = "Takoda99/Customer Sprite Library")]
    public sealed class CustomerSpriteLibrary : ScriptableObject
    {
        [SerializeField] private CustomerSpriteSet[] _sets = new CustomerSpriteSet[0];

        [Tooltip("属性に対応する定義が無いときに使う既定の見た目（通常はモブ）。")]
        [SerializeField] private CustomerAttribute _fallbackAttribute = CustomerAttribute.Normal;

        private Dictionary<CustomerAttribute, CustomerSpriteSet> _lookup;

        public IReadOnlyList<CustomerSpriteSet> Sets => _sets;

        private void OnEnable()
        {
            BuildLookup();
        }

        private void OnValidate()
        {
            // インスペクタで属性を編集したときに引き直せるよう、キャッシュを捨てる。
            _lookup = null;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<CustomerAttribute, CustomerSpriteSet>(_sets.Length);
            foreach (var set in _sets)
            {
                if (set == null)
                {
                    continue;
                }

                // 同一属性が重複していたら先勝ち（アセット側の設定ミス）。
                if (!_lookup.ContainsKey(set.Attribute))
                {
                    _lookup.Add(set.Attribute, set);
                }
            }
        }

        /// <summary>属性に対応する画像セットを返す。未定義なら fallback、それも無ければ null。</summary>
        public CustomerSpriteSet ResolveSet(CustomerAttribute attribute)
        {
            if (_lookup == null)
            {
                BuildLookup();
            }

            if (_lookup.TryGetValue(attribute, out var set))
            {
                return set;
            }

            return _lookup.TryGetValue(_fallbackAttribute, out var fallback) ? fallback : null;
        }

        /// <summary>(属性, 状態) から画像を引く。客オブジェクトが呼ぶのはこれだけ。</summary>
        public Sprite Resolve(CustomerAttribute attribute, CustomerVisualState state)
        {
            var set = ResolveSet(attribute);
            return set?.Resolve(state);
        }
    }
}
