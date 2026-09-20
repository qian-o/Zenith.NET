# 한국어 번역 지침

## 리소스와 구조

영어를 유일한 원문으로 사용합니다. `locales/en-US/strings.yml`의 키와 값만 해당 언어의 `strings.yml`로 복사합니다. 번역 전에는 게시되지 않는 이 영구적인 `translation.md`만 유지합니다. 첫 줄의 `### YamlMime:Resources`를 보존합니다.

탐색 구조, 장 순서, 앵커, 링크, 서식, 코드 및 이미지는 `locales/` 밖에서 공유합니다. 이 디렉터리에 문서, TOC, 템플릿, 스크립트 또는 설정을 추가하지 않습니다.

키는 안정적인 역할과 개념을 나타냅니다. 문구를 수정해도 키 이름을 바꾸지 않으며 문구 자체, 표시 위치, 번호 또는 언어에 종속된 이름을 피합니다. 영어 값만 명확하고 자연스러운 한국어로 번역합니다.

값은 일반 텍스트이며 HTML이나 Markdown이 아닙니다. `{context}`나 `{link}` 같은 이름 있는 자리 표시자는 공유 코드와 링크를 삽입합니다. 순서는 바꿀 수 있지만 이름은 모두 보존하고 추가하거나 제거하지 않습니다. 영어의 모든 키가 필요합니다. 키가 없거나 자리 표시자가 다르면 빌드가 실패합니다.

기존 DocFX 명령을 그대로 사용합니다. 완성된 사전을 추가하면 언어가 자동으로 활성화됩니다. 모든 언어는 같은 페이지 경로를 사용하며 `?lang=ko-KR`로 언어를 선택합니다. 언어 목록은 디렉터리 코드순으로 정렬합니다.

```sh
docfx documents/docfx.json --warningsAsErrors
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

## 용어

| 영어 | 사용할 표현 |
| --- | --- |
| API / GPU / RHI | 약어를 유지하고 원문에 따라 설명 |
| barrier | 배리어 |
| buffer | 버퍼. `Buffer`는 유지 |
| command buffer | 명령 버퍼 |
| command queue | 명령 큐 |
| drawable | 현재 표시할 이미지. `Drawable`은 유지 |
| graphics context | 그래픽스 컨텍스트 |
| pipeline | 파이프라인 |
| readback | 리드백 |
| render pass | 렌더 패스 |
| resource | 리소스 |
| shader | 셰이더 |
| swap chain | 스왑 체인 |
| texture | 텍스처 |
| timeline | 타임라인 |
| upload | 업로드 |
