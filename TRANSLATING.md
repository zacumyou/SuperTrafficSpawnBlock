# Community translations / 사용자 번역

English and Korean are built in and work without I18NEveryWhere. The same source dictionaries are shipped in `lang/en-US.json` and `lang/ko-KR.json`. All 33 keys, including settings, legacy asset names/descriptions, panel labels, warnings and registration status, use the game's localization system. Do not rename keys; change only values. Repeated legacy asset entries should receive consistent translations. Player-assigned building names are not translation keys.

## Try a translation

1. Enable I18NEveryWhere. Copy `lang/en-US.json` to a game-supported locale filename such as `lang/de-DE.json` next to the installed mod DLL (inside its `lang` folder).
2. Translate the JSON string values. Keep valid UTF-8 JSON, with no comments or trailing commas. Missing keys may be omitted and fall back to English.
3. Select that language in the game and use I18NEveryWhere's Reload localization option. Reopen the selection/options panel if it still displays cached text.
4. To replace this mod's built-in Korean/English, enable I18NEveryWhere's Overwrite option. If a separate language pack should override the bundled `lang` files, its Restrict option must not block duplicate keys.

Keep a copy outside the installed mod folder: updates may replace bundled language files. Submit the locale JSON to this mod's author or an I18NEveryWhere language-pack maintainer; editing a local file does not upload it automatically. No translation website or external submission is created by this mod.

한국어: `en-US.json`을 언어 코드 이름으로 복사하여 값만 번역하세요. I18NEveryWhere에서 다시 불러온 뒤 게임 언어를 선택합니다. 기존 한국어/영어를 수정하려면 I18NEveryWhere의 덮어쓰기를 켜세요. 업데이트에 대비하여 번역본은 별도로 보관하고 제작자에게 전달하세요. 이 모드 자체에는 온라인 제출 기능이 없습니다.

## Maintainers

Edit `lang/en-US.json` and `lang/ko-KR.json`, then rebuild. Both dictionaries are embedded into the DLL as the native `IDictionarySource` and copied as loose files for I18NEveryWhere. Only en-US/ko-KR are registered natively; do not register English into every locale, which would interfere with missing-translation detection. The stable source type is `SuperTrafficSpawnBlock.Locale`, available to I18NEveryWhere's export function. No binary dependency or Harmony patch is required here.

Reference reviewed 2026-09-25: https://github.com/baka-gourd/I18NEveryWhere/tree/d9285c2490079c6d67303da536207b47b106ce64
Relevant implementation: `I18NEverywhere/Mod.cs` (CacheMods, LoadEmbedLocales, LoadEmbedLocaleFromJson), `HookLocalizationDictionary.cs`, and `Setting.cs` (ExportModLocalization). Live integration in the game remains unverified.
