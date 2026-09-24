# End-to-end smoke test against a running `docker compose up` stack (Windows PowerShell 5.1+ or PowerShell 7).
# Builds a chain root <- p1 <- p2 <- p3 <- owner, posts events under both schemes and checks
# commissions, idempotency, the payout and the wallet balance.
param(
    [string]$Partners = "http://localhost:5101",
    [string]$Activity = "http://localhost:5102",
    [string]$Commission = "http://localhost:5103",
    [string]$Wallet = "http://localhost:5104",
    [string]$Run = [string][DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Host "FAIL: $Message" -ForegroundColor Red
    exit 1
}

function Invoke-Api([string]$Method, [string]$Url, $Body = $null) {
    $params = @{ Method = $Method; Uri = $Url; UseBasicParsing = $true }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Compress)
        $params.ContentType = "application/json"
    }
    try {
        $response = Invoke-WebRequest @params
        $json = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Json = $json }
    }
    catch {
        $failed = $_.Exception.Response
        if ($null -eq $failed) { Fail "cannot reach $Url - is 'docker compose up' running? ($($_.Exception.Message))" }
        return [pscustomobject]@{ Status = [int]$failed.StatusCode; Json = $null }
    }
}

function Expect([int]$Status, [string]$Method, [string]$Url, $Body = $null) {
    $result = Invoke-Api $Method $Url $Body
    if ($result.Status -ne $Status) { Fail "expected $Status, got $($result.Status) for $Method $Url" }
    return $result.Json
}

function U([string]$Name) { "${Name}_$Run" }

function Get-Commissions([string]$EventId) {
    $detail = (Invoke-Api GET "$Activity/events/$EventId").Json
    if ($null -eq $detail) { return @() }
    return @($detail.commissions)
}

function Wait-Commissions([string]$EventId, [int]$Count) {
    for ($i = 0; $i -lt 30; $i++) {
        if ((Get-Commissions $EventId).Count -eq $Count) { return }
        Start-Sleep -Seconds 1
    }
    Fail "event $EventId did not get $Count commissions"
}

function Assert-Amounts([string]$EventId, [decimal[]]$Expected, [string]$Scheme) {
    $items = Get-Commissions $EventId | Sort-Object level
    $actual = @($items | ForEach-Object { [decimal]$_.amount })
    $same = $actual.Count -eq $Expected.Count
    for ($i = 0; $same -and $i -lt $actual.Count; $i++) { $same = $actual[$i] -eq $Expected[$i] }
    if (-not $same) { Fail "$Scheme amounts for ${EventId}: $($actual -join '; ') (expected $($Expected -join '; '))" }
    if (@($items | Where-Object { $_.schemeType -ne $Scheme }).Count -gt 0) { Fail "$EventId has commissions not calculated by $Scheme" }
}

Write-Host "Run id: $Run"

Write-Host "1. Build the partner chain"
Expect 201 POST "$Partners/users" @{ externalId = (U root) } | Out-Null
$previous = U root
foreach ($name in "p1", "p2", "p3", "owner") {
    Expect 201 POST "$Partners/users" @{ externalId = (U $name); parentExternalId = $previous } | Out-Null
    $previous = U $name
}
Expect 200 POST "$Partners/users" @{ externalId = (U owner); parentExternalId = (U p3) } | Out-Null
Expect 409 PUT "$Partners/users/$(U p1)/parent" @{ parentExternalId = (U owner) } | Out-Null
$levels = @((Expect 200 GET "$Partners/users/$(U owner)/ancestors").items | ForEach-Object { $_.level }) -join ","
if ($levels -ne "1,2,3,4") { Fail "ancestor levels: $levels" }

Write-Host "2. Linear scheme: profit 1000 -> 10, 20, 30, 40"
Expect 200 POST "$Commission/admin/scheme" @{ schemeType = "Linear" } | Out-Null
Expect 201 POST "$Activity/events" @{ externalEventId = (U lin); userExternalId = (U owner); profit = 1000 } | Out-Null
Expect 200 POST "$Activity/events" @{ externalEventId = (U lin); userExternalId = (U owner); profit = 1000 } | Out-Null
Expect 409 POST "$Activity/events" @{ externalEventId = (U lin); userExternalId = (U owner); profit = 5 } | Out-Null
Expect 201 POST "$Activity/events" @{ externalEventId = (U loss); userExternalId = (U owner); profit = -300 } | Out-Null
Wait-Commissions (U lin) 4
Assert-Amounts (U lin) @(10, 20, 30, 40) "Linear"

Write-Host "3. Switch to Fibonacci: profit 1000 -> 10, 10, 20, 30; old commissions keep Linear"
Expect 200 POST "$Commission/admin/scheme" @{ schemeType = "Fibonacci" } | Out-Null
Expect 201 POST "$Activity/events" @{ externalEventId = (U fib); userExternalId = (U owner); profit = 1000 } | Out-Null
Wait-Commissions (U fib) 4
Assert-Amounts (U fib) @(10, 10, 20, 30) "Fibonacci"
Assert-Amounts (U lin) @(10, 20, 30, 40) "Linear"
if ((Get-Commissions (U loss)).Count -ne 0) { Fail "loss produced commissions" }
if ((Expect 200 GET "$Activity/users/$(U owner)/events").total -ne 3) { Fail "owner events count" }

Write-Host "4. Wait for the periodic payout; balance = paid commissions only"
$paid = $false
for ($i = 0; $i -lt 90 -and -not $paid; $i++) {
    $paid = @(Get-Commissions (U fib) | Where-Object { -not $_.isPaid }).Count -eq 0
    if (-not $paid) { Start-Sleep -Seconds 1 }
}
if (-not $paid) { Fail "commissions were not paid out" }
$balance = [decimal](Expect 200 GET "$Wallet/users/$(U p3)/balance").balance
if ($balance -ne 20) { Fail "p3 balance: $balance (expected 10 + 10)" }
if ((Expect 200 GET "$Wallet/users/$(U root)/payouts").total -ne 2) { Fail "root payouts count" }
if ([decimal](Expect 200 GET "$Wallet/users/$(U owner)/balance").balance -ne 0) { Fail "event owner must not earn" }

Write-Host "OK: all checks passed" -ForegroundColor Green
