# Ensure Ollama has a model loaded
$ollamaRunning = $false
try {
    $ps = Invoke-RestMethod -Uri "http://localhost:11434/api/ps" -ErrorAction Stop
    if ($ps.models.Count -gt 0) { $ollamaRunning = $true }
} catch {}

if (-not $ollamaRunning) {
    Write-Host "Loading qwen3:14b..."
    Start-Process -FilePath "ollama" -ArgumentList "run qwen3:14b" -WindowStyle Hidden
    Start-Sleep -Seconds 30
}

$webhook = "https://discord.com/api/webhooks/1490590808603361291/SCVngVWu8BmQ87KBfwWZsKjk1nlwrOmSMcfy8F_tn2v2ELtJcDLGWKNhO3Zwy5pAMl_l"
$userId = "1356296581472718988"
$ghToken = $env:GITHUB_TOKEN
$headers = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3+json" }
$reviewedFile = "$HOME\.office-rag-db\reviewed-prs.json"

if (Test-Path $reviewedFile) {
    $reviewed = Get-Content $reviewedFile | ConvertFrom-Json
} else {
    $reviewed = @()
}

$repos = @("Koraji95-coder/Office", "Koraji95-coder/Suite")

foreach ($repo in $repos) {
    try {
        $prs = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls?state=open&per_page=100" -Headers $headers

        foreach ($pr in $prs) {
            if ($pr.user.login -ne "copilot-swe-agent[bot]" -and $pr.user.login -ne "Copilot") { continue }

            $repoShort = $repo.Split("/")[1]

            # ========== GATE 1: No code yet ==========
            if ($pr.additions -eq 0 -and $pr.deletions -eq 0) {
                Write-Host "SKIP: $repoShort#$($pr.number) - no code changes yet"
                continue
            }

            # ========== GATE 2: Still a draft ==========
            if ($pr.draft -eq $true) {
                Write-Host "SKIP: $repoShort#$($pr.number) - draft (Copilot still working)"
                continue
            }

            # ========== GATE 3: Get fresh PR state ==========
            try {
                $freshPr = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)" -Headers $headers
            } catch {
                Write-Host "SKIP: $repoShort#$($pr.number) - could not fetch PR details"
                continue
            }

            # ========== GATE 4: Check if CI checks are done ==========
            $headSha = $freshPr.head.sha
            try {
                $checkRuns = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/commits/$headSha/check-runs" -Headers $headers
                if ($checkRuns.total_count -gt 0) {
                    $pending = @($checkRuns.check_runs | Where-Object { $_.status -ne "completed" })
                    $failed = @($checkRuns.check_runs | Where-Object { $_.status -eq "completed" -and $_.conclusion -notin @("success", "neutral", "skipped") })

                    if ($pending.Count -gt 0) {
                        $names = ($pending | ForEach-Object { $_.name }) -join ", "
                        Write-Host "SKIP: $repoShort#$($pr.number) - checks running: $names"
                        continue
                    }
                    if ($failed.Count -gt 0) {
                        $names = ($failed | ForEach-Object { "$($_.name)=$($_.conclusion)" }) -join ", "
                        Write-Host "SKIP: $repoShort#$($pr.number) - checks failed: $names"
                        continue
                    }
                }
                # No checks or all checks passed - proceed
            } catch {
                # No checks configured - that's fine, proceed
                # No checks configured - proceed silently
            }

            # ========== GATE 5: Check mergeability ==========
            if ($freshPr.mergeable -eq $false) {
                Write-Host "SKIP: $repoShort#$($pr.number) - merge conflict, attempting branch update..."
                try {
                    Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/update-branch" -Method PUT -Headers $headers -ContentType "application/json" -Body ('{"expected_head_sha":"' + $headSha + '"}') | Out-Null
                    Write-Host "  -> Branch updated, will review next cycle"
                } catch {
                    Write-Host "  -> Branch update failed, needs manual resolution"
                }
                continue
            }

            # ========== GATE 6: Already reviewed this version ==========
            $prKey = "$repo#$($pr.number)@$($freshPr.updated_at)"
            if ($reviewed -contains $prKey) { continue }

            # ==========================================
            # ALL GATES PASSED - PR is ready for review
            # ==========================================
            Write-Host "`n--- Reviewing $repoShort#$($pr.number): $($pr.title) ---"

            $diffHeaders = @{ Authorization = "Bearer $ghToken"; Accept = "application/vnd.github.v3.diff" }
            $diff = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)" -Headers $diffHeaders

            if ($diff.Length -gt 6000) {
                $diff = $diff.Substring(0, 6000) + "`n... (truncated)"
            }

            $ragContext = ""
            try {
                $ragOutput = & python "C:\Users\koraj\OneDrive\Documents\GitHub\Office\scripts\rag\query.py" $pr.title 2>$null
                $ragContext = $ragOutput | Out-String
                if ($ragContext.Length -gt 2000) {
                    $ragContext = $ragContext.Substring(0, 2000)
                }
            } catch {}

            $reviewPrompt = @"
You are a senior code reviewer. Review this pull request and provide a brief assessment.

PR Title: $($pr.title)
PR Description: $($pr.body)

DIFF:
$diff

RELATED CODEBASE CONTEXT:
$ragContext

Provide a review with:
1. **Verdict**: APPROVE, REQUEST_CHANGES, or NEEDS_DISCUSSION
2. **Summary**: 2-3 sentences on what this PR does
3. **Concerns**: Any issues found (or "None")
4. **Quality**: Rate 1-10

Keep it concise. No fluff.
"@

            $chatBody = @{
                model    = "qwen3:14b"
                messages = @(@{ role = "user"; content = $reviewPrompt })
                stream   = $false
            } | ConvertTo-Json -Depth 3

            $response = Invoke-RestMethod -Uri "http://localhost:11434/api/chat" -Method POST -ContentType "application/json" -Body $chatBody
            $review = $response.message.content

            # Determine verdict
            $verdict = "UNKNOWN"
            if ($review -match "REQUEST_CHANGES") { $verdict = "REQUEST_CHANGES" }
            elseif ($review -match "NEEDS_DISCUSSION") { $verdict = "NEEDS_DISCUSSION" }
            elseif ($review -match "APPROVE") { $verdict = "APPROVE" }

            # Extract score
            $score = 0
            if ($review -match "(\d+)\s*/\s*10") { $score = [int]$Matches[1] }

            # Submit GitHub PR review
            $ghReviewEvent = "COMMENT"
            if ($verdict -eq "APPROVE") { $ghReviewEvent = "APPROVE" }
            elseif ($verdict -eq "REQUEST_CHANGES") { $ghReviewEvent = "REQUEST_CHANGES" }

            try {
                $reviewBody = @{
                    body  = "## Auto-Review (Ollama)`n`n$review`n`n---`n*Automated review powered by qwen3:14b + RAG context*"
                    event = $ghReviewEvent
                } | ConvertTo-Json -Compress
                Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/reviews" -Method POST -Headers $headers -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($reviewBody)) | Out-Null
                Write-Host "Submitted $ghReviewEvent review on $repoShort#$($pr.number)"
            } catch {
                Write-Host "Failed to submit review on $repoShort#$($pr.number): $($_.Exception.Message)"
            }

            # ---- MERGE (only if APPROVE + score >= 9) ----
            $mergeStatus = ""
            if ($verdict -eq "APPROVE" -and $score -ge 9) {
                $mergeBody = @{ commit_title = "auto-merge: $($pr.title) [score $score/10]"; merge_method = "squash" } | ConvertTo-Json -Compress
                try {
                    Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/pulls/$($pr.number)/merge" -Method PUT -Headers $headers -ContentType "application/json" -Body $mergeBody | Out-Null
                    Write-Host "AUTO-MERGED: $repoShort#$($pr.number) - score $score/10"
                    $mergeStatus = "Auto-merged"

                    # Log to decision memory
                    $memoryFile = "$HOME\.office-rag-db\decision-memory.json"
                    if (Test-Path $memoryFile) { $memory = Get-Content $memoryFile | ConvertFrom-Json } else { $memory = @() }
                    $memory = @($memory) + @{ decision = "auto-merged"; repo = $repoShort; pr_number = [int]$pr.number; title = $pr.title; score = $score; timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ") }
                    $memory | ConvertTo-Json -Depth 4 | Set-Content -Path $memoryFile -Encoding UTF8
                } catch {
                    $sc = $_.Exception.Response.StatusCode
                    if ($sc -eq 409) { $mergeStatus = "Merge conflict on final attempt" }
                    elseif ($sc -eq 405) { $mergeStatus = "Blocked by ruleset" }
                    else { $mergeStatus = "Merge failed - HTTP $sc" }
                    Write-Host "$mergeStatus on $repoShort#$($pr.number)"
                }
            } elseif ($verdict -eq "APPROVE" -and $score -lt 9) {
                $mergeStatus = "Approved but score $score/10 < 9 - needs manual merge"
            }

            # ---- DISCORD ----
            if ($mergeStatus -match "Auto-merged") {
                $color = 3066993; $statusLabel = "MERGED"
            } elseif ($mergeStatus -match "conflict|failed|Blocked") {
                $color = 15548997; $statusLabel = "MERGE FAILED"
            } elseif ($verdict -eq "REQUEST_CHANGES") {
                $color = 15548997; $statusLabel = "CHANGES REQUESTED"
            } elseif ($verdict -eq "NEEDS_DISCUSSION") {
                $color = 16776960; $statusLabel = "NEEDS DISCUSSION"
            } elseif ($verdict -eq "APPROVE") {
                $color = 5763719; $statusLabel = "APPROVED"
            } else {
                $color = 9807270; $statusLabel = "REVIEWED"
            }

            $mergeInfo = ""
            if ($mergeStatus -ne "") { $mergeInfo = "`n`n**Merge Status:** $mergeStatus" }

            $embedDesc = "$review$mergeInfo`n`n[View PR]($($pr.html_url))"
            if ($embedDesc.Length -gt 4000) {
                $embedDesc = $embedDesc.Substring(0, 3950) + "`n... (truncated)`n`n[View PR]($($pr.html_url))"
            }

            $payload = @{
                content = "<@$userId>"
                embeds = @(@{
                    title       = "$statusLabel | $($pr.title)"
                    description = $embedDesc
                    color       = $color
                    footer      = @{ text = "$repoShort | PR #$($pr.number) | Score: $score/10" }
                    timestamp   = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
                })
            }
            $jsonPayload = $payload | ConvertTo-Json -Depth 5 -Compress
            try {
                Invoke-RestMethod -Uri $webhook -Method POST -ContentType "application/json; charset=utf-8" -Body ([System.Text.Encoding]::UTF8.GetBytes($jsonPayload)) | Out-Null
            } catch {
                Write-Host "Discord failed for $repoShort#$($pr.number): $($_.Exception.Message)"
            }

            $reviewed += $prKey
            Start-Sleep -Seconds 5
        }
    } catch {
        Write-Host "Error reviewing $repo : $($_.Exception.Message)"
    }
}

$reviewed | ConvertTo-Json | Set-Content -Path $reviewedFile -Encoding UTF8
Write-Host "`n=== Review cycle complete ==="



