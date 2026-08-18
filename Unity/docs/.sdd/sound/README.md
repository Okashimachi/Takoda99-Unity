# `sound/` — SE（効果音）

SEの実体（`AudioClip`）と音量を **ScriptableObject 1つ**に集約し、鳴らす側は識別子（`SoundId`）だけを知る。
`FontTheme` / `CustomerSpriteLibrary` と同じ「見た目・音の実体は SO に外出しする」方針の音版。

| # | ファイル | 内容 |
|---|---|---|
| 01 | [01-sound-library.md](./01-sound-library.md) | `SoundLibrary` / `SoundPlayer` と、ゲーム内イベントへの割り当て一覧 |

## 素材

素材は [OtoLogic](https://otologic.jp/) から取得している。**音源ファイル本体はリポジトリにコミットしていない**
（`.meta` のみ管理）。入手・配置手順は [Unity/Assets/Sounds/README.md](../../../Assets/Sounds/README.md)。
