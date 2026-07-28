#!/usr/bin/env bash
#
# Guided demo of the Redfish Diagnostic & Telemetry Emulator.
# Start the API first (in another terminal):
#     dotnet run --project src/RedfishEmulator.Api
# Then run:  ./demo.sh   (override the base URL with:  BASE=http://localhost:5199 ./demo.sh)
#
set -euo pipefail
BASE="${BASE:-http://localhost:5126}"

jqf() { python3 -c "import sys,json;d=json.load(sys.stdin);print(eval(sys.argv[1]))" "$1"; }
pause() { echo; read -rp "  ↵ press enter to continue…" _; echo; }
hdr() { echo; echo "════════════════════════════════════════════════════════"; echo "  $1"; echo "════════════════════════════════════════════════════════"; }

hdr "1. AUTHENTICATE — establish a Redfish session"
echo "POST /redfish/v1/SessionService/Sessions   (UserName/Password → X-Auth-Token)"
TOKEN=$(curl -s -D - -o /dev/null -X POST "$BASE/redfish/v1/SessionService/Sessions" \
  -H "Content-Type: application/json" -d '{"UserName":"admin","Password":"admin"}' \
  | awk 'tolower($1)=="x-auth-token:"{print $2}' | tr -d '\r')
echo "  got X-Auth-Token: ${TOKEN:0:12}…  (used on every call below)"
# All protected calls go through this wrapper, which presents the session token.
rf() { curl -s -H "X-Auth-Token: $TOKEN" "$@"; }
echo "  (a request with no token is rejected:)"
curl -s -o /dev/null -w "    GET /redfish/v1/Systems/1  without token → HTTP %{http_code}\n" "$BASE/redfish/v1/Systems/1"
pause

hdr "2. BASELINE — the platform is healthy"
echo "GET /redfish/v1/Systems/1"
rf "$BASE/redfish/v1/Systems/1" | jqf \
  "'  %s %s | Health: %s | %d CPUs, %g GiB RAM'%(d['Manufacturer'],d['Model'],d['Status']['HealthRollup'],d['ProcessorSummary']['Count'],d['MemorySummary']['TotalSystemMemoryGiB'])"
echo "GET .../Processors  (CPUs + GPU accelerators)"
rf "$BASE/redfish/v1/Systems/1/Processors" | jqf "'  %d processors inventoried'%d['Members@odata.count']"
echo "Run diagnostics:"
rf -X POST "$BASE/redfish/v1/Systems/1/Actions/Oem/RedfishEmulator.RunDiagnostics" | jqf "'  TaskStatus: %s — %s'%(d['TaskStatus'],d['Messages'][0]['Message'])"
pause

hdr "3. LIVE TELEMETRY — sensors move over time"
echo "GET /redfish/v1/Chassis/1/Power   (poll 1)"
rf "$BASE/redfish/v1/Chassis/1/Power" | jqf "'  platform power draw: %g W'%d['PowerControl'][0]['PowerConsumedWatts']"
sleep 2
rf "$BASE/redfish/v1/Chassis/1/Power" | jqf "'  platform power draw: %g W  (2s later — it moved)'%d['PowerControl'][0]['PowerConsumedWatts']"
pause

hdr "4. INJECT A FAULT — 'GPU fell off the bus'"
echo "POST .../FaultInjection/GpuOffBus/Activate"
rf -X POST "$BASE/redfish/v1/Oem/RedfishEmulator/FaultInjection/GpuOffBus/Activate" | jqf \
  "'  activated — affected component: %s'%d['AffectedComponentId']"
echo "GET .../Processors/GPU1"
rf "$BASE/redfish/v1/Systems/1/Processors/GPU1" | jqf \
  "'  GPU1 → Health: %s, State: %s'%(d['Status']['Health'],d['Status']['State'])"
echo "GET /redfish/v1/Systems/1"
rf "$BASE/redfish/v1/Systems/1" | jqf "'  system health rollup → %s'%d['Status']['HealthRollup']"
pause

hdr "5. DIAGNOSE UNDER FAULT — the failure is caught and explained"
rf -X POST "$BASE/redfish/v1/Systems/1/Actions/Oem/RedfishEmulator.RunDiagnostics" \
  | python3 -c "import sys,json;d=json.load(sys.stdin);print('  TaskStatus:',d['TaskStatus']);[print('   -',m['Message']) for m in d['Messages']]"
pause

hdr "6. RESET — back to health"
rf -o /dev/null -w "  Reset → HTTP %{http_code}\n" -X POST "$BASE/redfish/v1/Oem/RedfishEmulator/FaultInjection/Reset"
rf "$BASE/redfish/v1/Systems/1" | jqf "'  system health → %s'%d['Status']['HealthRollup']"
echo
echo "  Done. Full API explorer: $BASE/swagger"
