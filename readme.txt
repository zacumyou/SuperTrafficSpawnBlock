Super Traffic Spawn Block 0.4.0

사용 방법
1. 도로 > Maintenance에서 Super Traffic Spawn Block 또는 Super Traffic Sink Block을 배치합니다.
2. 스폰 블록을 클릭합니다. 선택 패널에서 차량 목적지와 차량 생성 배율을 설정합니다.
3. 목적지는 내장 체크박스로 복수 선택할 수 있습니다. 아무 목적지도 선택하지 않으면 도시 외부가 기본입니다.
4. 배율은 내장 라디오 버튼으로 1 / 2 / 5 / 10 / 20 / 50배를 선택합니다. 새 블록은 기본 20배입니다.

블록별 설정
- 새 배치 목록의 스폰 블록을 하나로 통합했습니다. UI 정렬은 스폰 9000, 흡입 9001입니다.
- 기존 배율별 블록은 세이브 호환을 위해 내부 프리팹을 유지하며 새 메뉴에서 숨깁니다. 이미 배치한 블록의 원래 배율은 유지되고 선택 패널에서 변경할 수 있습니다.
- 기존 단일 목적지 선택은 새 목적지 목록으로 자동 이전됩니다. 목적지 엔티티 참조와 개별 배율은 세이브에 저장합니다.
- 차량 목적지와 배율은 블록마다 독립적입니다. 설정 변경은 이후 생성 차량부터 적용됩니다.

복수 목적지
- 차량 한 대마다 선택된 목적지 중 하나를 동일한 1/N 확률로 추첨합니다. 두 곳은 각 50%, 세 곳은 각 1/3입니다. 짧은 구간의 실제 대수는 무작위 추첨이므로 정확히 반반일 필요는 없습니다.
- 도시 외부도 목적지 하나로 함께 체크할 수 있습니다. 기본 외부 상태에서 첫 흡입 블록을 체크하면 그 흡입 블록으로 전환됩니다. 외부도 포함하려면 도시 외부를 다시 체크합니다.
- 목적지별 경로를 별도로 계산하고 재사용합니다. 모든 선택 목적지의 경로가 준비되어야 생성하므로 경로가 없는 곳의 몫을 다른 곳으로 몰지 않습니다.
- 흡입 블록 철거 시 목록에서 제외합니다. 남는 목적지가 없으면 도시 외부로 전환합니다.
- 흡입 블록은 생성하지 않고, 도착한 모드 생성 차량만 기본 차량 삭제 처리로 제거합니다.

기존 기능
- 배율은 256 시뮬레이션 프레임당 목표 차량 수입니다. 차로·정체·경로에 따라 실제 배출량은 달라집니다.
- 출발 시간 분산, 연결 가능한 차로 분산, 출발 지연 시 자동 중지/재개를 유지합니다. 정체 중 못 만든 차량을 누적해서 나중에 몰아 생성하지 않습니다.
- 대형 차량 허용 옵션은 모든 블록에 공통이며 기본 꺼짐입니다.
- 로딩 중에는 생성하지 않고 게임 시작 후 경로를 확보하면 생성합니다.
- 상태 설명과 드롭다운은 제거했습니다. 내장 접기/펼치기·체크박스·라디오를 사용하며 제목과 내용 좌우 여백을 20rem에 맞췄습니다.

검증
- C#/UI 빌드, 체크박스/라디오 이벤트, 차량별 균등 확률 분배와 기존 동작 검사를 통과했습니다.
- 브라우저 레이아웃 미리보기에서 정렬을 확인했습니다. 미리보기는 게임 UI를 대체 렌더링한 것이며 실제 Gameface와 같다고 보장하지 않습니다.
- 실제 게임의 메뉴 통합, 복수 경로 주행과 새 저장 형식의 저장/재로드는 미검증입니다. 모드가 필요한 세이브에서는 모드를 유지하세요.

English
One placeable source plus one sink. Select a source to set its per-block rate (1/2/5/10/20/50x) using native radio controls and destinations using native checkboxes.
Each new vehicle draws one selected destination with equal 1/N probability. All selected paths must be ready before generating traffic. No selections defaults to outside city.
Old rate variants are hidden compatibility prefabs; existing instances retain their original rates until changed. Old single-destination data migrates to the new target buffer.
Build and targeted tests pass. In-game multi-route operation and save/reload remain unverified.

0.4.0 성능 보완
- 목적지가 늘어도 블록당 총 생성량은 배율 값을 넘지 않습니다.
- 전체 모드의 길찾기 요청을 생성 주기당 최대 4건, 처리 중 최대 16건으로 제한합니다. 목적지가 많으면 초기 경로 준비 시간이 길어질 수 있습니다.
- 외부 연결 목록과 경로 중복 검사, 선택 패널 목록 갱신을 줄였습니다. 실게임 CPU/프레임 측정은 미실시입니다.


0.4.0: 생성 블록은 CSSuperTrafficSpawnBlockOut, 목적지 블록은 CSSuperTrafficSpawnBlockIn의 모델/재질/LOD/썸네일을 사용합니다. 두 커스텀 에셋이 설치되어 있어야 합니다. 기존 도로 유지보수 메뉴의 모드 블록을 배치하세요. 원본 커스텀 에셋 파일과 기존 배율/목적지 저장 형식은 변경하지 않습니다.
0.4.0: 생성/목적지 블록 부지를 기본 건물 최소 크기인 1x1칸(8x8m)으로 변경. 모델 자체는 4x4m 유지.
0.4.0: 유지보수 메뉴에서 새로 배치하는 생성 블록은 기본 2x. 기존 블록의 배율은 유지됩니다.
