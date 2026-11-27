# Hop-Up
오픈소스프로젝트

## 사용한 오픈소스 · 외부 에셋

이 저장소의 코드는 [MIT License](LICENSE)를 따릅니다. 아래 외부 에셋과 패키지는 각자의 라이선스를 따릅니다.

### 3D 모델

| 에셋 | 제작자 | 라이선스 | 위치 |
|---|---|---|---|
| [Forest Nature Pack 1.0](https://kaylousberg.com/) | Kay Lousberg (KayKit) | CC0 1.0 | `Assets/ThirdParty/KayKit_Forest` |
| [Farm Buildings](https://quaternius.com/packs/farmbuildings.html) | Quaternius | CC0 1.0 | `Assets/ThirdParty/Quaternius_FarmBuildings` |
| [Bunny](https://poly.pizza/m/irZjWFARyl) (Ultimate Monsters Bundle) | Quaternius | CC0 1.0 | `Assets/ThirdParty/Quaternius_Bunny` |

CC0 에셋은 출처 표기 의무가 없지만, 감사의 뜻으로 적어 둡니다.

### 폰트

| 폰트 | 라이선스 | 위치 |
|---|---|---|
| [Noto Sans KR](https://fonts.google.com/noto/specimen/Noto+Sans+KR) | SIL Open Font License 1.1 ([전문](<Assets/Fonts/NotoSansKR - OFL.txt>)) | `Assets/Fonts` |
| Liberation Sans (TextMesh Pro 기본 폰트) | SIL Open Font License 1.1 ([전문](<Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt>)) | `Assets/TextMesh Pro/Fonts` |

### 라이브러리 · 패키지

| 패키지 | 라이선스 | 용도 |
|---|---|---|
| [Newtonsoft.Json](https://www.newtonsoft.com/json) (`com.unity.nuget.newtonsoft-json`) | MIT | 세이브 데이터 · 밸런스 설정 직렬화 |
| Unity Input System, Universal RP, uGUI · TextMesh Pro, Timeline, AI Navigation, Test Framework | [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) | 입력, 렌더링, UI, 연출, 길찾기, 테스트 |

### 직접 만든 리소스

- `Assets/Textures`의 지구 · 달 · 별 텍스처는 에디터 스크립트(`OpeningSetupMenu`, `EndingSetupMenu`)가 코드로 생성합니다.
