#!/usr/bin/env bash
# End-to-end smoke test against a running `docker compose up` stack.
# Builds a chain root <- p1 <- p2 <- p3 <- owner, posts events under both schemes and checks
# commissions, idempotency, the payout and the wallet balance.
set -euo pipefail
trap 'echo "FAIL: unexpected error at line $LINENO: $BASH_COMMAND" >&2' ERR

PARTNERS=${PARTNERS:-http://localhost:5101}
ACTIVITY=${ACTIVITY:-http://localhost:5102}
COMMISSION=${COMMISSION:-http://localhost:5103}
WALLET=${WALLET:-http://localhost:5104}
RUN=${RUN:-$(date +%s)}
JSON=(-H "Content-Type: application/json")

fail() { echo "FAIL: $*" >&2; exit 1; }
status() { curl -s -o /dev/null -w '%{http_code}' "$@" || true; }
expect() {
  local want=$1; shift
  local got; got=$(status "$@")
  if [[ "$got" == "000" ]]; then
    local url; for url in "$@"; do [[ "$url" == http* ]] && break; done
    fail "cannot reach $url - are all containers healthy? (docker compose ps)"
  fi
  [[ "$got" == "$want" ]] || fail "expected $want, got $got for $*"
}
PY=""
for candidate in python3 python py; do
  if command -v "$candidate" >/dev/null 2>&1 && "$candidate" -c "import json" >/dev/null 2>&1; then PY=$candidate; break; fi
done
[[ -n "$PY" ]] || fail "python is required (python3/python/py). On Windows use scripts/smoke-test.ps1 instead."
command -v curl >/dev/null 2>&1 || fail "curl is required"
json() { "$PY" -c "import json,sys; d=json.load(sys.stdin); print(eval(sys.argv[1]))" "$1" | tr -d '\r'; }

u() { echo "${1}_${RUN}"; }
echo "Run id: $RUN"

echo "1. Build the partner chain"
expect 201 "${JSON[@]}" -X POST "$PARTNERS/users" -d "{\"externalId\":\"$(u root)\"}"
prev=$(u root)
for n in p1 p2 p3 owner; do
  expect 201 "${JSON[@]}" -X POST "$PARTNERS/users" -d "{\"externalId\":\"$(u $n)\",\"parentExternalId\":\"$prev\"}"
  prev=$(u $n)
done
expect 200 "${JSON[@]}" -X POST "$PARTNERS/users" -d "{\"externalId\":\"$(u owner)\",\"parentExternalId\":\"$(u p3)\"}"
expect 409 "${JSON[@]}" -X PUT "$PARTNERS/users/$(u p1)/parent" -d "{\"parentExternalId\":\"$(u owner)\"}"
levels=$(curl -s "$PARTNERS/users/$(u owner)/ancestors" | json "[i['level'] for i in d['items']]")
[[ "$levels" == "[1, 2, 3, 4]" ]] || fail "ancestors levels: $levels"

echo "2. Linear scheme: profit 1000 -> 10, 20, 30, 40"
expect 200 "${JSON[@]}" -X POST "$COMMISSION/admin/scheme" -d '{"schemeType":"Linear"}'
expect 201 "${JSON[@]}" -X POST "$ACTIVITY/events" -d "{\"externalEventId\":\"$(u lin)\",\"userExternalId\":\"$(u owner)\",\"profit\":1000}"
expect 200 "${JSON[@]}" -X POST "$ACTIVITY/events" -d "{\"externalEventId\":\"$(u lin)\",\"userExternalId\":\"$(u owner)\",\"profit\":1000}"
expect 409 "${JSON[@]}" -X POST "$ACTIVITY/events" -d "{\"externalEventId\":\"$(u lin)\",\"userExternalId\":\"$(u owner)\",\"profit\":5}"
expect 201 "${JSON[@]}" -X POST "$ACTIVITY/events" -d "{\"externalEventId\":\"$(u loss)\",\"userExternalId\":\"$(u owner)\",\"profit\":-300}"

wait_commissions() {
  for _ in $(seq 1 30); do
    local n; n=$(curl -s "$ACTIVITY/events/$1" | json "len(d.get('commissions', []))" 2>/dev/null || echo 0)
    [[ "$n" == "$2" ]] && return 0
    sleep 1
  done
  fail "event $1 did not get $2 commissions"
}
wait_commissions "$(u lin)" 4
amounts=$(curl -s "$ACTIVITY/events/$(u lin)" | json "[(c['level'], float(c['amount']), c['schemeType']) for c in d['commissions']]")
[[ "$amounts" == "[(1, 10.0, 'Linear'), (2, 20.0, 'Linear'), (3, 30.0, 'Linear'), (4, 40.0, 'Linear')]" ]] || fail "linear: $amounts"

echo "3. Switch to Fibonacci: profit 1000 -> 10, 10, 20, 30; old commissions keep Linear"
expect 200 "${JSON[@]}" -X POST "$COMMISSION/admin/scheme" -d '{"schemeType":"Fibonacci"}'
expect 201 "${JSON[@]}" -X POST "$ACTIVITY/events" -d "{\"externalEventId\":\"$(u fib)\",\"userExternalId\":\"$(u owner)\",\"profit\":1000}"
wait_commissions "$(u fib)" 4
amounts=$(curl -s "$ACTIVITY/events/$(u fib)" | json "[(c['level'], float(c['amount']), c['schemeType']) for c in d['commissions']]")
[[ "$amounts" == "[(1, 10.0, 'Fibonacci'), (2, 10.0, 'Fibonacci'), (3, 20.0, 'Fibonacci'), (4, 30.0, 'Fibonacci')]" ]] || fail "fibonacci: $amounts"
schemes=$(curl -s "$ACTIVITY/events/$(u lin)" | json "sorted({c['schemeType'] for c in d['commissions']})")
[[ "$schemes" == "['Linear']" ]] || fail "old commissions were recalculated: $schemes"
[[ $(curl -s "$ACTIVITY/events/$(u loss)" | json "len(d['commissions'])") == 0 ]] || fail "loss produced commissions"
[[ $(curl -s "$ACTIVITY/users/$(u owner)/events" | json "d['total']") == 3 ]] || fail "owner events count"

echo "4. Wait for the periodic payout; balance = paid commissions only"
for _ in $(seq 1 90); do
  paid=$(curl -s "$ACTIVITY/events/$(u fib)" | json "all(c['isPaid'] for c in d['commissions'])")
  [[ "$paid" == "True" ]] && break
  sleep 1
done
[[ "$paid" == "True" ]] || fail "commissions were not paid out"
balance=$(curl -s "$WALLET/users/$(u p3)/balance" | json "float(d['balance'])")
[[ "$balance" == "20.0" ]] || fail "p3 balance: $balance (expected 10 + 10)"
payouts=$(curl -s "$WALLET/users/$(u root)/payouts" | json "d['total']")
[[ "$payouts" == "2" ]] || fail "root payouts: $payouts"
[[ $(curl -s "$WALLET/users/$(u owner)/balance" | json "float(d['balance'])") == "0.0" ]] || fail "event owner must not earn"

echo "OK: all checks passed"
