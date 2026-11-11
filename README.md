# Better Junimos Custom Next

스타듀밸리 모드 [Better Junimos](https://github.com/hawkfalcon/Stardew-Mods) (원작자: **hawkfalcon**, **ceruleandeep**)를 기반으로 주니모 AI 탐색 알고리즘을 개선하고 구조적인 버그들을 수정한 커스텀 모드입니다.

## 주요 개선 사항
* **탐색 반경 일관화**: 주니모 오두막(3×2) 외곽을 기준으로 상·하·좌·우 모든 방향의 탐색 및 행동 반경이 설정된 반경(기본값 `7`)과 정확히 일치하도록 개선했습니다.
* **주니모 AI 탐색 및 작업 배정 최적화**:
  * 이동 중인 목표 타일뿐만 아니라 현재 작업 수행 중인 타일까지 예약(Reservation)하여 주니모들의 행동이 겹치지 않도록 개선했습니다.
  * 현재 주니모 위치에서 가장 가까운 작업 타일부터 A* 길찾기(스프링클러, 허수아비 등 장애물 우회)를 수행하며, 도달 불가능한 타일은 일시 제외하여 멈춤 없이 다음 타일로 이동합니다.
  * 한 타일에서 `[죽은 작물 제거 → 수확 → 비료 → 파종 → 물주기]` 등 연속 작업이 가능한 경우 자리를 뜨지 않고 즉시 연달아 처리합니다.
  * 스폰 직후 불필요한 배회, 작업 중 3.5% 확률 배회, 할 일이 남았음에도 25% 확률로 조기 퇴근하던 문제를 제거했습니다.
  * 바닐라 `pokeToHarvest()`의 30% 호출 누락 및 타이머 전환 조건 누락으로 인해 주니모가 아무것도 하지 않고 멈춰 서 있던 현상을 해결했습니다.
* **성능(CPU/메모리) 최적화**: 타일 스캔 시 불필요한 객체(`Object`, `Crop`, 리스트 복사본) 반복 생성을 제거하고 캐싱을 적용했습니다.
* **불필요/치트 옵션 정리**: `InfiniteJunimoInventory`(무한 인벤토리 치트), `WorkFaster`, `WorkRidiculouslyFast` 옵션을 제거하고 최적화된 작업 반응 속도를 기본 동작으로 내장했습니다.

## 변경 이력
버전별 상세 변경 사항은 [CHANGELOG.md](CHANGELOG.md)에서 확인할 수 있습니다.

## 크레딧 (Credits)
* **원본 모드 (Original Mod)**: [Better Junimos](https://www.nexusmods.com/stardewvalley/mods/2221) by **hawkfalcon** & **ceruleandeep**
* **커스텀 수정 (Modified by)**: **bemuse01**
