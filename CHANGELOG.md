# Changelog

## [1.0.9] - 2026-11-12

### Removed
* **실험적 기능인 온실 작업(`CanWorkInGreenhouse` / `VisitGreenhouse`) 및 관련 옵션 제거**:
  * 온실 외벽 타일에 부딪혀 주니모를 소멸·재소환하던 불안정한 편법 구현으로 인해 농장 작물보다 온실 외벽이 가까울 때 주니모 동선이 꼬이고 매 스캔마다 온실 맵을 추가 순회하던 문제를 없애기 위해, `온실에서 작업 가능`(`CanWorkInGreenhouse`) 옵션과 `VisitGreenhouseAbility`, `JunimoGreenhouse`를 전면 제거했습니다.

---

## [1.0.8] - 2026-11-12

### Removed
* **오두막 조기 해금 및 건설 비용 변경 옵션 제거**:
  * 바닐라 밸런스 및 세이브 무결성(마법 잉크 퀘스트 진행) 유지를 위해 `마을 회관 완료 후 건설 가능`(`AvailableAfterCommunityCenterComplete`), `조건 없이 즉시 건설 가능`(`AvailableImmediately`), `오두막 건설 비용 절감`(`ReducedCostToConstruct`), `오두막 무료 건설`(`FreeToConstruct`) 옵션과 `BuildingEditor.cs`를 제거했습니다.
* **미사용 `[CP] Better Junimos Config Wizard` 디렉토리 저장소에서 완전 삭제**:
  * Git 저장소에 남아있던 `[CP] Better Junimos Config Wizard` 폴더를 완전히 제거했습니다.

---

## [1.0.7] - 2026-11-12

### Fixed
* **4번째 이상 주니모(`junimoNumber >= 3`) 스폰 시 중도 소멸(`destroy = true`) 및 잔여 타일 누락 버그 수정**:
  * 바닐라 `JunimoHarvester` 생성자가 0~2번 주니모만 처리하고 3번(4번째) 이상 주니모에 대해 무작위 길찾기를 시도하다 실패 시 `destroy = true`를 설정하던 문제를 방지하기 위해, 스폰 시점(`Util.SpawnJunimoAtPosition`)에 농장 여부를 불문하고 `destroy = false`로 초기화하고 생성자 내부의 불필요한 무작위 길찾기를 건너뛰도록 수정했습니다.
* **스폰 직후 무작위 배회(`pathfindToRandomSpotAroundHut`)로 인한 구역 이탈 방지**:
  * 스폰 직후 `pathfindToNewCrop()` 호출 시 남은 작업이 없어 귀가(`SendJunimoHome`)가 결정된 경우, `pathfindToRandomSpotAroundHut()`가 덮어써져 밭 외곽 통로나 인접 구역 경계로 배회하던 현상을 제거했습니다.
  * `noHarvest` 모드에서 무작위 배회를 할 때도 경작지(`HoeDirt`) 타일을 우선 선택하도록 개선했습니다.
* **타일 예약 판정(`IsTileClaimedByOtherJunimo`) 정밀화**:
  * 귀가 중이거나 소멸 중인 주니모가 지나가는 타일이 예약된 타일로 오인되지 않도록 명시적 작업 할당(`AssignedTargets`) 기준으로만 타일 점유를 판정하고, 귀가·개체수 축소 시 즉시 예약을 해제하도록 개선했습니다.

---

## [1.0.6] - 2026-11-11

### Changed
* **모드 고유 식별자(`UniqueID`) 변경**:
  * SMAPI가 원본 모드(`hawkfalcon.BetterJunimos`, Nexus `2221`)로 인식하여 불필요한 업데이트 알림을 표시하던 문제를 방지하기 위해 `manifest.json`의 `UniqueID`를 `bemuse01.BetterJunimosCustomNext`로 변경했습니다.

---

## [1.0.5] - 2026-11-11

### Added
* **바닐라 건포도(`Raisins`) 간식 보너스 연동 및 속도 버프 추가**:
  * 오두막 상자에 건포도(`Raisins`)를 넣어두면 바닐라의 20% 2배 수확 효과에 더해, 주니모들의 이동 속도(`speed` 3 → 4)와 작업 반응 속도가 빨라지고 알림 메시지(*"주니모들이 건포도를 먹고 신나서 더 빠르게 일합니다!"*)가 표시되도록 추가했습니다.
  * 상자에 건포도를 넣고 닫는 즉시(`OnMenuChanged`) 및 겨울 작업 시(`ReplaceJunimoHutdayUpdate`)에도 건포도 효과가 즉시 발동·유지되도록 개선했습니다.

### Changed
* **`수확 시 플레이어 경험치 획득`(`GiveExperience`) 옵션 이동**:
  * 임금 카테고리 제거에 따라 `GiveExperience` 옵션을 `주니모 작업 조건 및 능력` 카테고리로 이동했습니다.

### Removed
* **일일 임금(`WorkForWages` / `JunimoPayments`) 및 마법사 설정 이벤트 팩 전면 제거**:
  * 매일 채집물·꽃·과일·와인을 요구하며 보관된 수확물을 임의로 소모하거나 파업하던 `WorkForWages` 기능과 `[CP] Better Junimos Config Wizard` 번들 팩을 전면 제거했습니다.

---

## [1.0.4] - 2026-11-11

### Fixed
* **탐색 반경 확장 시 이웃 오두막 구역 침범(도미노 파종) 문제 수정**:
  * 탐색 반경을 넓혀 옆 오두막 밭까지 반경이 닿을 때, 두 방향 이상이 겹치는 오두막(예: 3번 오두막)이 순수 비겹침 구역을 먼저 끝낸 뒤 자기 밭 테두리보다 주니모 현재 위치에서 더 가까운 '통로 건너 옆집 밭 타일'로 넘어가 씨앗을 소모하던 문제를 해결했습니다.
  * 후보 타일을 **3단계 관할 구역 우선순위 (`0순위: 완전 전용 구역` → `1순위: 반경은 겹치지만 내 오두막에서 가장 가까운 내 밭 구역` → `2순위: 다른 오두막에 더 가까운 이웃 밭 구역`)**로 분류하여, 자기 오두막 관할 구역(`0·1순위`)을 100% 끝내기 전에는 절대 이웃 오두막 관할 구역(`2순위`)으로 넘어가지 않도록 개선했습니다 (`Util.GetDistanceFromHut`, `Util.TryAssignWorkPath`).
* **파종 시 씨앗 유실/중복 차감 방어 코드 보강**:
  * `PlantCropsAbility.Plant()`에서 대상 타일이 빈 `HoeDirt`(`crop == null`)가 아닐 경우 `true`가 반환되어 씨앗만 차감될 수 있던 구문을 `false`를 반환하도록 수정했습니다.

---

## [1.0.3] - 2026-11-11

### Added
* **주니모 세부 작업 능력 인게임(GMCM) On/Off 설정 추가**:
  * 기존에 스킬 해금 퀘스트나 `config.json` 수동 편집에 묶여 있던 7가지 작업 능력(`일반 작물 수확`, `씨앗 파종`, `비료 뿌리기`, `작물에 물주기`, `죽은 작물 제거`, `덤불 수확`, `채집 작물 수확`)을 Generic Mod Config Menu(GMCM)에서 공물 없이 즉시 켜고 끌 수 있도록 옵션을 추가했습니다 (모든 능력의 기본값은 `true`).
  * 게임 도중 GMCM에서 작업 능력을 켜거나 끄면 게임 재시작 없이 즉시 반영되도록 `JunimoAbilities` 구조를 개선했습니다.

### Changed
* **수확 및 파종 부가 기능 카테고리 분리**:
  * `꽃 수확 방지`, `거대작물 수확 방지`, `28일에는 모든 작물 수확`, `커피 콩 파종 방지`, `계절에 맞지 않는 작물 파종 방지`, `모둠 씨앗 파종 허용` 6가지 부가 옵션을 별도의 **`수확 및 파종 세부 설정` (`Harvest & Planting Options`)** 카테고리로 분리하여 관리하도록 개편했습니다.
* **한국어(`i18n/ko.json`) 번역 전면 보완 및 개선**:
  * 영어로만 표시되던 미번역 항목(`온실에서 작업 가능`, `28일에는 모든 작물 수확`, `계절에 맞지 않는 작물 파종 방지`, `모둠 씨앗 파종 허용`, `수확 시 플레이어 경험치 획득` 및 각 툴팁)을 모두 한국어로 번역하고, 기존 기계 번역 문구를 자연스러운 한국어로 전면 다듬었습니다.

### Removed
* **스킬 해금(`Progression`) 및 진행도 추적기(`Perfection Tracker`) 전면 제거**:
  * 공물 아이템을 바쳐 능력을 해금하던 스킬 진행 시스템(`Progression.Enabled`, `assets/unlock_costs.json`), 오두막 좌클릭/단축키로 열리던 진행도 추적기 편지 팝업(`HutClickEnabled`, `HutMenuKeybind`), 관련 콘솔 명령어(`bj_prompt_all`, `bj_prompt`, `bj_unlock`)를 제거했습니다.

---

## [1.0.2] - 2026-11-11

### Changed
* **오두막 영역 겹침 시 전용 영역 우선 작업 로직 추가**:
  * 여러 주니모 오두막의 탐색 반경이 서로 겹칠 때, 다른 오두막 반경과 겹치지 않는 **자기 오두막 전용 영역(Exclusive Zone)** 타일을 1순위로 먼저 작업하고, 전용 영역의 할 일이 모두 끝난 뒤에만 **겹치는 공유 영역(Shared Zone)**으로 이동하여 작업하도록 개선했습니다 (`Util.TryAssignWorkPath`).
  * 이를 통해 오두막 상자 인벤토리를 각자 독립적으로 유지하면서도, 공유 영역에서 씨앗을 먼저 소진해 정작 자기 전용 구역에 씨앗이 모자라게 되는 문제를 방지했습니다.
* **탐색 루프 CPU 최적화**:
  * `Util.TryAssignWorkPath` 및 `PatchSearchAroundHut`에서 탐색 범위를 현재 맵의 유효 타일 경계(`0 ~ LayerWidth-1`, `0 ~ LayerHeight-1`)로 클램핑하여 맵 외곽 헛스캔을 제거했습니다.
  * 타일 스캔 시 다른 주니모의 예약 여부(`IsTileClaimedByOtherJunimo`)를 매 타일마다 먼저 순회하던 순서를 바꾸어, 해당 타일에 실제 수행 가능한 작업(`IdentifyJunimoAbility`)이 존재하는지 먼저 확인한 뒤 작업 가능한 타일에만 예약 여부를 검사하도록 최적화했습니다.
* **최대 설정 한도 조정**:
  * Generic Mod Config Menu(GMCM) 및 설정 저장 시 오두막당 최대 주니모 수(`MaxJunimos`) 상한을 `40` → `12`로, 오두막 최대 탐색 반경(`MaxRadius`) 상한을 `40` → `20`으로 제한했습니다.

---

## [1.0.1] - 2026-11-11

### Fixed
* **오두막 상자 아이템 즉시 인식 버그 수정**:
  * 당일 건설한 오두막에 `"JunimoChest"` 태그가 누락되거나, 상자에 아이템을 넣는 순간 바닐라 `Chest.ShowMenu()` 호출로 인해 `ItemGrabMenu.context`가 `JunimoHut`에서 `Chest`로 변경되어 상자를 닫아도 씨앗/아이템이 인식되지 않던 문제를 해결했습니다 (`OnBuildingListChanged`에서 즉시 태그 부여 및 `OnMenuChanged`에서 오두막 상자 참조 직접 대조).
  * `PatchSearchAroundHut`의 1초 주기 스캔 시 해당 오두막 상자의 아이템 보유 상태를 함께 갱신하도록 보강하여 별도 재클릭 없이 즉시 작업이 시작되도록 개선했습니다.

### Changed
* **상자 아이템 검사 및 능력 조회 CPU/메모리 최적화**:
  * 모든 능력(`IJunimoAbility`)의 `RequiredItems()`가 매 호출마다 새 `List<string>`을 힙에 할당하지 않고 정적(static) 리스트를 재사용하도록 변경했습니다.
  * `FertilizeAbility`가 모든 비료 아이템 ID를 개별 순회하던 구조를 카테고리(`-19`, 나무 비료 제외) 단일 검사로 통합하고, `JunimoAbilities.UpdateHutItems`의 LINQ 클로저/리스트 할당을 제거하여 CPU 및 GC 부담을 최소화했습니다.

### Removed
* **주니모 수동 소환 기능 (`J` 키, `SpawnJunimoKeybind`) 전면 삭제**:
  * 구버전 스폰 버그 우회용으로 존재하던 수동 소환 단축키 설정, 이벤트 핸들러 및 번역 키를 제거했습니다.

---

## [1.0.0] - 2026-11-11

### Changed
* **탐색 반경 일관화 및 엄격한 범위 제한**:
  * 주니모 오두막(가로 3칸 × 세로 2칸) 외곽을 기준으로 상·하·좌·우 모든 방향이 동일하게 설정 반경만큼 확장되도록 통일 (`Util.GetHutRadiusBounds`, `Util.IsWithinHutRadius`).
  * 작업 스캔(`PatchSearchAroundHut`), 길찾기(`PatchPathfindDoWork`), 대기 배회(`PatchPathfindToRandomSpotAroundHut`), 온실 탐색(`JunimoGreenhouse`), 인접 타일 채집(`HarvestForageCropsAbility`), 작물 맵(`CropMap`) 등 모든 동작이 반경 내에서만 이루어지도록 수정.
* **주니모 탐색 알고리즘 및 행동 로직 전면 개선**:
  * **행동 겹침 방지**: 이동 경로의 목적지뿐만 아니라 이미 타일에 도착해 작업을 수행 중인 주니모의 타일 및 채집 대상 타일까지 예약 상태로 관리하여 중복 이동/작업 차단.
  * **최단거리 A\* 작업 배정 및 장애물 우회**: 주니모 현재 위치에서 맨해튼 거리가 가장 가까운 후보 타일부터 A* 경로 탐색(노드 한도 2500)을 수행하며, 스프링클러·허수아비 등 장애물에 완전히 막혀 도달할 수 없는 타일은 일시적 도달 불가로 기록하고 즉시 다음 타일을 탐색하도록 개선.
  * **단일 타일 연속 작업 처리**: 능력 우선순위를 `[죽은 작물 제거 → 작물/덤불/채집물 수확 → 비료 → 파종 → 물주기 → 온실 방문]` 순으로 재정렬하고, 한 타일에서 후속 작업이 가능할 경우 이동하지 않고 즉시 연속 수행하도록 개선.
  * **비효율적 동선 및 조기 퇴근 제거**: 스폰 즉시 작업 타일로 이동하도록 개선하고, 작업 중 3.5% 확률로 배회하거나 탐색 실패 시 25% 확률로 조기 퇴근하던 로직을 제거(모든 작업이 완료되었을 때만 오두막 복귀).
* **CPU 및 메모리 부하 최적화**:
  * `HarvestCropsAbility` 및 `PlantCropsAbility`에서 매 타일 검사마다 `StardewValley.Object`, `Crop`, 리스트 복사본을 반복 생성하던 병목을 제거하고 `Game1.objectData`/`Game1.cropData` 조회 및 캐싱으로 대체.
* **기본 설정값(Default Config) 조정**:
  * 탐색 반경(`MaxRadius`): `7`
  * 최대 주니모 수(`MaxJunimos`): `3`
  * 스킬 진행(`Progression.Enabled`): `false` (기본 비활성화)
  * 비 오는 날 작업(`CanWorkInRain`): `true`
  * 겨울 작업(`CanWorkInWinter`): `true`
* **매니페스트(`manifest.json`) 정비**:
  * 모드 이름을 `Better Junimos Custom Next`로 변경, 원작자(`hawkfalcon`, `ceruleandeep`) 및 수정자(`bemuse01`) 명시, SMAPI 업데이트 키(`UpdateKeys`) 제거.

### Fixed
* 바닐라 `JunimoHarvester.pokeToHarvest()`의 30% 확률 무시(`Game1.random.NextDouble() < 0.7`), 작업 실패/종료 시 `harvestTimer = 0` 설정으로 인한 후속 호출 누락, `PathFindController` 5초 충돌 대기 취소 시 복구 누락 등으로 인해 주니모가 멈춰 서서 아무것도 하지 않던 현상 수정.
* 아이템 소모 시 스택이 0 이하가 될 때 상자 슬롯에서 완전히 제거(`clearNulls()`)되도록 수정.
* `WaterAbility`가 죽은 작물 타일에도 물을 주려 하던 문제 수정.
* `VisitGreenhouseAbility`에서 온실에 작업이 없을 때(`Point.Zero`)도 온실에 작업이 있는 것으로 오판하던 버그 수정.

### Removed
* 치트성 옵션인 주니모 무한 인벤토리(`FunChanges.InfiniteJunimoInventory`) 기능 전면 삭제.
* AI 및 타이머 개선으로 불필요해진 `WorkFaster`, `WorkRidiculouslyFast` 설정 및 관련 해금 항목 삭제(최적화된 작업 속도를 기본 동작으로 통합).
