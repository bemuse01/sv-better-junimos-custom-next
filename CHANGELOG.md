# Changelog

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
