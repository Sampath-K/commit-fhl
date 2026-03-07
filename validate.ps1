$API = 'https://commit-api.gentlepond-c6124d62.eastus.azurecontainerapps.io'
$ALEX = 'f7a02de7-e195-4894-bc23-f7f74b696cbd'
$SARAH = '5659f687-9ea8-4dfe-95c7-2990356288af'
$MARCUS = 'c1c0037d-1b8c-4c34-bede-f6dadc38a8c6'
$FATIMA = '78a8c66f-2928-4edc-9230-d6a209e72f85'
$PRIYA = '8d0832a0-c586-4c41-b6ad-02a76c5b326c'
$DAVID = '6a638a9c-cad1-429d-8bc0-d435bf552e5a'

function Check($label, $test) {
    Write-Host "[$label]" -ForegroundColor Yellow -NoNewline
    Write-Host " " -NoNewline
    try { & $test } catch { Write-Host "ERROR: $_" -ForegroundColor Red }
}

Write-Host "=== COMMIT FHL VALIDATION ===" -ForegroundColor Cyan
Write-Host "Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')"
Write-Host ""

# 1. API Health
Check "1. API Health" {
    $h = (Invoke-WebRequest -Uri "$API/api/v1/health" -UseBasicParsing).Content | ConvertFrom-Json
    Write-Host "status=$($h.status) storage=$($h.storageConnected) graph=$($h.graphConnected)"
    if ($h.storageConnected) { Write-Host "  >> PASS: Storage connected" -ForegroundColor Green }
    else { Write-Host "  >> FAIL: Storage not connected" -ForegroundColor Red }
}

# 2. Alex cards
Check "2. Alex cards" {
    $d = (Invoke-WebRequest -Uri "$API/api/v1/commitments/$ALEX" -UseBasicParsing).Content | ConvertFrom-Json
    $n = $d.data.Count
    $ip = ($d.data | Where-Object { $_.status -eq 'in-progress' }).Count
    Write-Host "total=$n in-progress=$ip"
    if ($n -eq 9) { Write-Host "  >> PASS: 9 cards loaded" -ForegroundColor Green }
    else { Write-Host "  >> WARN: Expected 9, got $n" -ForegroundColor Yellow }
}

# 3. Sarah cards
Check "3. Sarah cards (Scheduling Skill)" {
    $d = (Invoke-WebRequest -Uri "$API/api/v1/commitments/$SARAH" -UseBasicParsing).Content | ConvertFrom-Json
    $n = $d.data.Count
    Write-Host "total=$n"
    if ($n -gt 0) { Write-Host "  >> PASS: Sarah board loaded (green pills active)" -ForegroundColor Green }
    else { Write-Host "  >> FAIL: No data for Sarah" -ForegroundColor Red }
}

# 4. Marcus cards
Check "4. Marcus cards (BizChat Platform)" {
    $d = (Invoke-WebRequest -Uri "$API/api/v1/commitments/$MARCUS" -UseBasicParsing).Content | ConvertFrom-Json
    $n = $d.data.Count
    Write-Host "total=$n"
    if ($n -gt 0) { Write-Host "  >> PASS: Marcus board loaded (purple pills active)" -ForegroundColor Green }
    else { Write-Host "  >> FAIL: No data for Marcus" -ForegroundColor Red }
}

# 5. Cascade - SEVAL (demo story 1)
Check "5. Cascade: SEVAL (5d slip)" {
    $c = (Invoke-WebRequest -Uri "$API/api/v1/graph/cascade?rootTaskId=rbs-seval-002&userId=$ALEX&slipDays=5" -Method POST -UseBasicParsing).Content | ConvertFrom-Json
    Write-Host "impact=$($c.impactScore) affected=$($c.affectedCount)"
    Write-Host "  NOTE: SEVAL chain crosses user partition - only root visible in cascade" -ForegroundColor Yellow
}

# 6. Cascade - Foundry (demo story 2 - full chain in Alex partition)
Check "6. Cascade: Foundry (14d slip - best demo)" {
    $c = (Invoke-WebRequest -Uri "$API/api/v1/graph/cascade?rootTaskId=rbs-foundry-002&userId=$ALEX&slipDays=14" -Method POST -UseBasicParsing).Content | ConvertFrom-Json
    Write-Host "impact=$($c.impactScore) affected=$($c.affectedCount)"
    $chain = $c.affectedTasks | ForEach-Object { "$($_.taskId)(+$($_.cumulativeSlipDays)d)" }
    Write-Host "  Chain: $($chain -join ' -> ')"
    if ($c.affectedCount -ge 4) { Write-Host "  >> PASS: 4-task chain visible in cascade" -ForegroundColor Green }
    else { Write-Host "  >> WARN: Shorter chain" -ForegroundColor Yellow }
}

# 7. Replan options
Check "7. Replan options" {
    $r = (Invoke-WebRequest -Uri "$API/api/v1/graph/replan?rootTaskId=rbs-foundry-002&userId=$ALEX&slipDays=14" -Method POST -UseBasicParsing).Content | ConvertFrom-Json
    foreach ($opt in $r.options) {
        $pct = [Math]::Round($opt.confidence * 100)
        Write-Host "  Option $($opt.optionId) '$($opt.label)': $pct% confidence"
    }
    if ($r.options.Count -eq 3) { Write-Host "  >> PASS: 3 replan options A/B/C" -ForegroundColor Green }
}

# 8. SWA
Check "8. SWA (frontend)" {
    $s = Invoke-WebRequest -Uri 'https://thankful-pond-0ba16370f.6.azurestaticapps.net' -UseBasicParsing
    Write-Host "HTTP $($s.StatusCode)"
    if ($s.StatusCode -eq 200) { Write-Host "  >> PASS: Frontend live" -ForegroundColor Green }
}

# 9. All 6 users have data
Check "9. All 6 users seeded" {
    $users = @{Alex=$ALEX; Priya=$PRIYA; Fatima=$FATIMA; David=$DAVID; Sarah=$SARAH; Marcus=$MARCUS}
    $allGood = $true
    foreach ($u in $users.GetEnumerator()) {
        $d = (Invoke-WebRequest -Uri "$API/api/v1/commitments/$($u.Value)" -UseBasicParsing).Content | ConvertFrom-Json
        $n = $d.data.Count
        if ($n -eq 0) { Write-Host "  $($u.Key): 0 cards (FAIL)" -ForegroundColor Red; $allGood = $false }
        else { Write-Host "  $($u.Key): $n cards" }
    }
    if ($allGood) { Write-Host "  >> PASS: All 6 users have commitments" -ForegroundColor Green }
}

Write-Host ""
Write-Host "=== END ===" -ForegroundColor Cyan
